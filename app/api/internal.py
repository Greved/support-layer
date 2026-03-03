from __future__ import annotations

import logging

import requests
from fastapi import APIRouter, Header, HTTPException
from qdrant_client import QdrantClient

from app.core.config import get_settings

router = APIRouter()
logger = logging.getLogger(__name__)


@router.get("/healthz", tags=["internal"])
def internal_health(x_internal_secret: str = Header(...)) -> dict:
    settings = get_settings()
    if x_internal_secret != settings.internal_secret:
        raise HTTPException(status_code=403, detail="Forbidden")

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
