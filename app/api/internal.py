from __future__ import annotations

import logging
import tempfile
from pathlib import Path
from typing import Annotated

import requests
from fastapi import APIRouter, Form, Header, HTTPException, UploadFile
from pydantic import BaseModel
from qdrant_client import QdrantClient

from app.core.config import get_settings
from app.services.query_service import run_query

router = APIRouter()
logger = logging.getLogger(__name__)


def _check_secret(x_internal_secret: str) -> None:
    settings = get_settings()
    if x_internal_secret != settings.internal_secret:
        raise HTTPException(status_code=403, detail="Forbidden")


class InternalQueryRequest(BaseModel):
    tenant_id: str
    query: str
    filters: dict | None = None


@router.post("/ingest", tags=["internal"])
async def internal_ingest(
    file: UploadFile,
    tenant_id: Annotated[str, Form()],
    document_id: Annotated[str, Form()] = "",
    x_internal_secret: str = Header(...),
) -> dict:
    _check_secret(x_internal_secret)
    settings = get_settings()

    suffix = Path(file.filename or "upload").suffix or ".tmp"
    content = await file.read()

    with tempfile.NamedTemporaryFile(suffix=suffix, delete=False) as tmp:
        tmp.write(content)
        tmp_path = Path(tmp.name)

    try:
        from ingestion.cli import (  # noqa: PLC0415
            build_filesystem_ingestion_pipeline,
            load_documents_from_paths,
            purge_existing_documents,
        )

        collection = f"tenant_{tenant_id}"
        docs, matched_files = load_documents_from_paths([str(tmp_path)], base_dir=tmp_path.parent)
        if docs:
            purge_existing_documents(settings, collection, matched_files)
            pipeline = build_filesystem_ingestion_pipeline(settings, collection=collection)
            result = pipeline.run({"splitter": {"documents": docs}})
            writer_stats = result.get("writer", {})
            chunks_written = int(
                writer_stats.get("documents_written") or writer_stats.get("count") or 0
            )
        else:
            chunks_written = 0
    finally:
        tmp_path.unlink(missing_ok=True)

    return {"chunks_written": chunks_written, "document_id": document_id}


@router.post("/query", tags=["internal"])
def internal_query(
    payload: InternalQueryRequest,
    x_internal_secret: str = Header(...),
) -> dict:
    _check_secret(x_internal_secret)
    settings = get_settings()
    result = run_query(settings, payload.tenant_id, payload.query, payload.filters)
    return result


@router.get("/healthz", tags=["internal"])
def internal_health(x_internal_secret: str = Header(...)) -> dict:
    _check_secret(x_internal_secret)
    settings = get_settings()

    qdrant_status = "ok"
    try:
        client = QdrantClient(
            host=settings.qdrant_host,
            port=settings.qdrant_port,
            api_key=settings.qdrant_api_key,
            timeout=5.0,
        )
        client.get_collections()
    except Exception as exc:
        logger.warning("Qdrant ping failed: %s", exc)
        qdrant_status = "error"

    embedding_status = "ok"
    try:
        resp = requests.get(f"{settings.llama_embedding_url}/health", timeout=5)
        resp.raise_for_status()
    except Exception as exc:
        logger.warning("Embedding server ping failed: %s", exc)
        embedding_status = "error"

    overall = "ok" if qdrant_status == "ok" and embedding_status == "ok" else "degraded"
    return {"status": overall, "qdrant": qdrant_status, "embedding": embedding_status}
