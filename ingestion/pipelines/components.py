import requests
from haystack import component
from haystack.dataclasses import Document


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
    ):
        self.endpoint = endpoint.rstrip("/")
        self.model = model
        self.timeout = timeout
        self.batch_size = max(1, batch_size)

    @component.output_types(documents=list[Document])
    def run(self, documents: list[Document]):
        if not documents:
            return {"documents": []}

        for start in range(0, len(documents), self.batch_size):
            batch = documents[start : start + self.batch_size]
            payload = {
                "model": self.model,
                "input": [doc.content for doc in batch],
            }
            response = requests.post(
                f"{self.endpoint}/embeddings", json=payload, timeout=self.timeout
            )
            response.raise_for_status()
            data = response.json()["data"]

            for doc, embedding in zip(batch, data, strict=False):
                doc.embedding = embedding["embedding"]

        return {"documents": documents}
