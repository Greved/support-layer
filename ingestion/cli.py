from __future__ import annotations

import glob
import json
import time
from pathlib import Path
from typing import Annotated

import typer
import yaml
from haystack import Pipeline
from haystack.components.converters import (
    HTMLToDocument,
    MarkdownToDocument,
    PyPDFToDocument,
    TextFileToDocument,
)
from haystack.components.preprocessors import DocumentSplitter
from haystack.dataclasses import Document

from app.core.config import Settings, get_settings
from ingestion.pipelines.components import LlamaCppEmbedding, QdrantWriter

DEFAULT_CONFIG_PATH = Path("ingestion/config/filesystem.example.yaml")

app = typer.Typer(help="Ingestion CLI for filesystem-based documents")


def load_yaml_config(path: Path) -> dict:
    if not path.exists():
        return {}

    with path.open("r", encoding="utf-8") as f:
        return yaml.safe_load(f) or {}


def normalize_document(doc: Document) -> Document:
    """Lightweight normalization to trim whitespace and ensure meta exists."""

    content = doc.content or ""
    normalized_lines = [line.strip() for line in content.splitlines()]
    doc.content = "\n".join(normalized_lines).strip()
    if doc.meta is None:
        doc.meta = {}
    return doc


def load_documents_from_paths(paths: list[str]) -> list[Document]:
    documents: list[Document] = []
    pdf_converter = PyPDFToDocument()
    md_converter = MarkdownToDocument()
    html_converter = HTMLToDocument()
    txt_converter = TextFileToDocument()

    for pattern in paths:
        for file_path in glob.glob(pattern, recursive=True):
            path = Path(file_path)
            suffix = path.suffix.lower()
            converter = None
            if suffix == ".pdf":
                converter = pdf_converter
            elif suffix in {".md", ".markdown"}:
                converter = md_converter
            elif suffix in {".html", ".htm"}:
                converter = html_converter
            else:
                converter = txt_converter

            result = converter.run(sources=[path])
            for doc in result["documents"]:
                if doc.meta is None:
                    doc.meta = {}
                doc.meta["source"] = str(path)
                documents.append(normalize_document(doc))

    return documents


def build_filesystem_ingestion_pipeline(
    settings: Settings,
    collection: str = "documents",
    vector_size: int = 1024,
    split_by: str = "word",
    split_length: int = 200,
    split_overlap: int = 40,
) -> Pipeline:
    pipeline = Pipeline()
    pipeline.add_component(
        "splitter",
        DocumentSplitter(
            split_by=split_by,
            split_length=split_length,
            split_overlap=split_overlap,
        ),
    )
    pipeline.add_component(
        "embedder",
        LlamaCppEmbedding(endpoint=str(settings.llama_embedding_url), model="bge-large-en-v1.5"),
    )
    pipeline.add_component(
        "writer",
        QdrantWriter(
            host=settings.qdrant_host,
            port=settings.qdrant_port,
            api_key=settings.qdrant_api_key,
            collection=collection,
            vector_size=vector_size,
        ),
    )

    pipeline.connect("splitter", "embedder")
    pipeline.connect("embedder", "writer")
    return pipeline


@app.command()
def ingest(
    paths: Annotated[
        list[str] | None,
        typer.Argument(None, help="Glob patterns for PDFs/HTML/Markdown to ingest"),
    ] = None,
    config: Annotated[
        Path,
        typer.Option(
            "--config",
            "-c",
            help="Path to YAML config defining sources/chunking settings",
            exists=True,
            dir_okay=False,
            readable=True,
            resolve_path=True,
        ),
    ] = DEFAULT_CONFIG_PATH,
    collection: Annotated[
        str | None,
        typer.Option(None, help="Qdrant collection name (overrides config when provided)"),
    ] = None,
    artifacts_dir: Annotated[
        Path | None,
        typer.Option(
            "--artifacts-dir",
            help="Optional directory to dump cleaned documents before splitting",
            dir_okay=True,
            file_okay=False,
            resolve_path=True,
        ),
    ] = None,
):
    settings = get_settings()
    config_data = load_yaml_config(config)

    source_globs = paths or config_data.get("sources", [])
    selected_collection = collection or config_data.get("collection", "documents")
    chunking = config_data.get("chunking", {})
    split_by = chunking.get("split_by", "word")
    split_length = int(chunking.get("split_length", 200))
    split_overlap = int(chunking.get("split_overlap", 40))

    if not source_globs:
        typer.echo("No sources provided via CLI or config; nothing to ingest", err=True)
        raise typer.Exit(code=1)

    docs = load_documents_from_paths(source_globs)
    if not docs:
        typer.echo("No documents found for provided paths", err=True)
        raise typer.Exit(code=1)

    pipeline = build_filesystem_ingestion_pipeline(
        settings=settings,
        collection=selected_collection,
        split_by=split_by,
        split_length=split_length,
        split_overlap=split_overlap,
    )
    result = pipeline.run({"splitter": {"documents": docs}})
    written = result.get("writer", {}).get("count", 0)
    artifact_root = artifacts_dir or config_data.get("artifacts_dir")
    if artifact_root:
        artifact_path = Path(artifact_root)
        artifact_path.mkdir(parents=True, exist_ok=True)
        output_file = artifact_path / f"ingestion_{int(time.time())}.ndjson"
        with output_file.open("w", encoding="utf-8") as f:
            for doc in docs:
                json.dump(
                    {
                        "id": doc.id,
                        "content": doc.content,
                        "meta": doc.meta or {},
                    },
                    f,
                    ensure_ascii=False,
                )
                f.write("\n")
        typer.echo(f"Dumped cleaned documents to {output_file}")

    typer.echo(f"Ingested {written} document chunks into Qdrant collection '{selected_collection}'")


if __name__ == "__main__":
    app()
