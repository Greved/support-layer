from __future__ import annotations

import glob
from pathlib import Path
from typing import Annotated

import typer
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

app = typer.Typer(help="Ingestion CLI for filesystem-based documents")


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
                documents.append(doc)

    return documents


def build_filesystem_ingestion_pipeline(
    settings: Settings,
    collection: str = "documents",
    vector_size: int = 1024,
) -> Pipeline:
    pipeline = Pipeline()
    pipeline.add_component(
        "splitter",
        DocumentSplitter(
            split_by="word",
            split_length=200,
            split_overlap=40,
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
        list[str],
        typer.Argument(..., help="Glob patterns for PDFs/HTML/Markdown to ingest"),
    ],
    collection: Annotated[str, typer.Option("documents", help="Qdrant collection name")],
):
    settings = get_settings()
    docs = load_documents_from_paths(paths)
    if not docs:
        typer.echo("No documents found for provided paths", err=True)
        raise typer.Exit(code=1)

    pipeline = build_filesystem_ingestion_pipeline(settings=settings, collection=collection)
    result = pipeline.run({"splitter": {"documents": docs}})
    written = result.get("writer", {}).get("count", 0)
    typer.echo(f"Ingested {written} document chunks into Qdrant collection '{collection}'")


if __name__ == "__main__":
    app()
