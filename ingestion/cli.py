from __future__ import annotations

import glob
import json
import sys
import time
from pathlib import Path

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
from haystack.components.writers import DocumentWriter
from haystack.dataclasses import Document
from haystack_integrations.document_stores.qdrant import QdrantDocumentStore

from app.core.config import Settings, get_settings
from ingestion.pipelines.components import LlamaCppEmbedding

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


def load_documents_from_paths(paths: list[str], base_dir: Path) -> list[Document]:
    documents: list[Document] = []
    pdf_converter = PyPDFToDocument()
    md_converter = MarkdownToDocument()
    html_converter = HTMLToDocument()
    txt_converter = TextFileToDocument()

    matched_files: list[Path] = []
    for pattern in paths:
        resolved_pattern = Path(pattern)
        if not resolved_pattern.is_absolute():
            resolved_pattern = (base_dir / resolved_pattern).resolve()
        for file_path in glob.glob(str(resolved_pattern), recursive=True):
            matched_files.append(Path(file_path))

    if not matched_files:
        typer.echo(f"[ingest] No files matched patterns: {paths}", err=True)
        return documents

    typer.echo(f"[ingest] Matched {len(matched_files)} files for conversion")

    for path in matched_files:
        suffix = path.suffix.lower()
        if suffix == ".pdf":
            converter = pdf_converter
        elif suffix in {".md", ".markdown"}:
            converter = md_converter
        elif suffix in {".html", ".htm"}:
            converter = html_converter
        else:
            converter = txt_converter

        try:
            result = converter.run(sources=[path])
        except Exception as exc:  # pragma: no cover - best-effort logging
            typer.echo(f"[ingest] Failed to convert {path}: {exc}", err=True)
            continue

        for doc in result.get("documents", []):
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
    split_length: int = 128,
    split_overlap: int = 32,
) -> Pipeline:
    document_store = QdrantDocumentStore(
        url=f"http://{settings.qdrant_host}:{settings.qdrant_port}",
        api_key=settings.qdrant_api_key,
        embedding_dim=vector_size,
        index=collection,
        prefer_grpc=False,
    )

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
        DocumentWriter(document_store=document_store),
    )

    pipeline.connect("splitter", "embedder")
    pipeline.connect("embedder", "writer")
    return pipeline


@app.command()
def ingest(
    paths: str | None = typer.Option(
        None,
        "--path",
        "-p",
        help="Glob pattern for PDFs/HTML/Markdown to ingest (overrides config sources)",
        show_default="from config",
    ),
    config: Path = typer.Option(  # noqa: B008
        DEFAULT_CONFIG_PATH,
        "--config",
        "-c",
        help="Path to YAML config defining sources/chunking settings",
        exists=True,
        dir_okay=False,
        readable=True,
        resolve_path=True,
    ),
    base_dir: Path | None = typer.Option(  # noqa: B008
        None,
        "--base-dir",
        "-b",
        help="Base directory for resolving relative glob patterns (defaults to config file dir)",
        dir_okay=True,
        file_okay=False,
        resolve_path=True,
    ),
    collection: str | None = typer.Option(  # noqa: B008
        None,
        "--collection",
        "-n",
        help="Qdrant collection name (overrides config when provided)",
    ),
    artifacts_dir: Path | None = typer.Option(  # noqa: B008
        None,
        "--artifacts-dir",
        help="Optional directory to dump cleaned documents before splitting",
        dir_okay=True,
        file_okay=False,
        resolve_path=True,
    ),
):
    settings = get_settings()
    typer.echo(f"[ingest] Loading config from {config}")
    config_data = load_yaml_config(config)

    config_base_dir = Path(config_data.get("base_dir")) if config_data.get("base_dir") else None
    active_base_dir = (base_dir or config_base_dir or config.parent).resolve()

    source_globs = [paths] if paths else config_data.get("sources", [])
    selected_collection = collection or config_data.get("collection", "documents")
    chunking = config_data.get("chunking", {})
    split_by = chunking.get("split_by", "word")
    split_length = int(chunking.get("split_length", 200))
    split_overlap = int(chunking.get("split_overlap", 40))

    if not source_globs:
        typer.echo("[ingest] No sources provided via CLI or config; nothing to ingest", err=True)
        raise typer.Exit(code=1)

    typer.echo(f"[ingest] Using base_dir={active_base_dir}")
    typer.echo(f"[ingest] Using sources: {source_globs}")
    docs = load_documents_from_paths(source_globs, base_dir=active_base_dir)
    typer.echo(f"[ingest] Loaded {len(docs)} documents before chunking")
    if not docs:
        typer.echo("[ingest] No documents found for provided paths", err=True)
        raise typer.Exit(code=1)

    typer.echo(
        "[ingest] Chunking with "
        f"split_by='{split_by}', length={split_length}, overlap={split_overlap}"
    )
    pipeline = build_filesystem_ingestion_pipeline(
        settings=settings,
        collection=selected_collection,
        split_by=split_by,
        split_length=split_length,
        split_overlap=split_overlap,
    )
    typer.echo(f"[ingest] Running pipeline into collection '{selected_collection}'")
    result = pipeline.run({"splitter": {"documents": docs}})
    writer_stats = result.get("writer", {})
    written = writer_stats.get("documents_written") or writer_stats.get("count") or len(docs)
    typer.echo(f"[ingest] Pipeline finished; wrote {written} chunks")

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
        typer.echo(f"[ingest] Dumped cleaned documents to {output_file}")

    typer.echo(
        f"[ingest] Completed ingestion into '{selected_collection}' with {written} total chunks"
    )


if __name__ == "__main__":
    # Allow invoking with an optional "ingest" alias: `python -m ingestion.cli ingest ...`
    if len(sys.argv) > 1 and sys.argv[1] == "ingest":
        sys.argv.pop(1)
    app()
