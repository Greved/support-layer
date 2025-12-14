from __future__ import annotations

import logging
import time
from typing import Any

from qdrant_client import QdrantClient
from qdrant_client.http.models import Filter, ScoredPoint

from app.core.config import Settings

logger = logging.getLogger(__name__)


def build_filter(filters: dict[str, Any] | None) -> Filter | None:
    # Minimal passthrough: currently returns None. Extend to map filters into Qdrant conditions.
    return None


class QdrantService:
    def __init__(self, settings: Settings):
        self.settings = settings
        self.client = QdrantClient(
            host=settings.qdrant_host,
            port=settings.qdrant_port,
            api_key=settings.qdrant_api_key,
            timeout=30.0,
        )

    def search(
        self, vector: list[float], top_k: int = 3, filters: dict[str, Any] | None = None
    ) -> list[ScoredPoint]:
        start = time.time()
        logger.info(
            "Searching Qdrant host=%s collection=%s top_k=%s",
            self.settings.qdrant_host,
            self.settings.qdrant_collection,
            top_k,
        )
        try:
            if hasattr(self.client, "search"):
                return self.client.search(
                    collection_name=self.settings.qdrant_collection,
                    query_vector=vector,
                    limit=top_k,
                    query_filter=build_filter(filters),
                    with_payload=True,
                )
            if hasattr(self.client, "search_points"):
                return self.client.search_points(
                    collection_name=self.settings.qdrant_collection,
                    query=vector,
                    limit=top_k,
                    query_filter=build_filter(filters),
                    with_payload=True,
                )
            if hasattr(self.client, "query_points"):
                return self.client.query_points(
                    collection_name=self.settings.qdrant_collection,
                    query=vector,
                    limit=top_k,
                    query_filter=build_filter(filters),
                    with_payload=True,
                )
            raise RuntimeError("Qdrant client does not support search/query on this version")
        except Exception as exc:  # pragma: no cover
            logger.error("Qdrant search failed", exc_info=exc)
            raise
        finally:
            duration = time.time() - start
            logger.info("Qdrant search completed duration_ms=%s", int(duration * 1000))
