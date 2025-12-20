import logging
import time
from collections.abc import Iterable

import requests
from haystack import component
from haystack.dataclasses import Document

logger = logging.getLogger(__name__)


@component
class LlamaCppEmbedding:
    """Haystack component that calls a llama.cpp embedding server (OpenAI-compatible).

    Expects the server to expose `/v1/embeddings` and return the OpenAI response schema.
    """

    def __init__(
        self,
        endpoint: str,
        model: str | None = None,
        timeout: float = 30.0,
        batch_size: int = 1,
        max_retries: int = 2,
    ):
        self.endpoint = endpoint.rstrip("/")
        self.model = model
        self.timeout = timeout
        self.batch_size = max(1, batch_size)
        self.max_retries = max(0, max_retries)

    def _post_embeddings(self, batch: list[Document]) -> list[list[float]]:
        payload = {
            "model": self.model,
            "input": [doc.content for doc in batch],
        }
        response = requests.post(f"{self.endpoint}/embeddings", json=payload, timeout=self.timeout)
        try:
            response.raise_for_status()
        except requests.RequestException as exc:
            details = ""
            try:
                details = response.text[:500]
            except Exception:  # pragma: no cover - best-effort logging
                details = ""
            logger.error(
                "Embedding request failed status=%s batch_size=%s details=%s",
                response.status_code,
                len(batch),
                details,
                exc_info=exc,
            )
            raise
        data = response.json().get("data", [])
        return [item.get("embedding", []) for item in data]

    def _embed_with_fallback(self, batch: list[Document]) -> Iterable[list[float]]:
        try:
            return self._post_embeddings(batch)
        except requests.HTTPError as exc:
            status = getattr(exc.response, "status_code", None)
            if status == 500 and len(batch) > 1:
                mid = max(1, len(batch) // 2)
                logger.warning(
                    "Embedding batch failed; retrying with smaller batches size=%s",
                    len(batch),
                )
                left = list(self._embed_with_fallback(batch[:mid]))
                right = list(self._embed_with_fallback(batch[mid:]))
                return [*left, *right]
            raise

    @component.output_types(documents=list[Document])
    def run(self, documents: list[Document]):
        if not documents:
            return {"documents": []}

        for start in range(0, len(documents), self.batch_size):
            batch = documents[start : start + self.batch_size]
            attempts = 0
            while True:
                try:
                    embeddings = list(self._embed_with_fallback(batch))
                    for doc, embedding in zip(batch, embeddings, strict=False):
                        doc.embedding = embedding
                    break
                except requests.RequestException:
                    attempts += 1
                    if attempts > self.max_retries:
                        raise
                    backoff = 0.5 * (2 ** (attempts - 1))
                    logger.warning(
                        "Embedding retry attempt=%s batch_size=%s backoff_s=%s",
                        attempts,
                        len(batch),
                        backoff,
                    )
                    time.sleep(backoff)

        return {"documents": documents}
