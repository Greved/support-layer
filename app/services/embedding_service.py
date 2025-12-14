from __future__ import annotations

import logging
import time

import requests

from app.core.config import Settings

logger = logging.getLogger(__name__)


class EmbeddingService:
    def __init__(self, settings: Settings):
        self.settings = settings

    def embed_query(self, query: str) -> list[float]:
        start = time.time()
        logger.info("Embedding query model=%s", self.settings.llama_embedding_model)
        payload = {"model": self.settings.llama_embedding_model, "input": [query]}
        try:
            resp = requests.post(
                f"{self.settings.llama_embedding_url}/embeddings", json=payload, timeout=30
            )
            resp.raise_for_status()
        except requests.RequestException as exc:
            logger.error("Embedding request failed", exc_info=exc)
            raise

        data = resp.json().get("data", [])
        if not data:
            logger.error("Embedding response missing data")
            raise ValueError("Embedding response missing data")

        duration = time.time() - start
        logger.info("Embedding completed duration_ms=%s", int(duration * 1000))
        return data[0]["embedding"]
