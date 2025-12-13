from __future__ import annotations

import uuid

import requests
from haystack import component
from haystack.dataclasses import Document
from qdrant_client import QdrantClient
from qdrant_client.http.models import Distance, PointStruct, VectorParams


@component
class LlamaCppEmbedding:
    """Haystack component that calls a llama.cpp embedding server (OpenAI-compatible).

    Expects the server to expose `/v1/embeddings` and return the OpenAI response schema.
    """

    def __init__(self, endpoint: str, model: str | None = None, timeout: float = 30.0):
        self.endpoint = endpoint.rstrip("/")
        self.model = model
        self.timeout = timeout

    @component.output_types(documents=list[Document])
    def run(self, documents: list[Document]):
        if not documents:
            return {"documents": []}

        payload = {
            "model": self.model,
            "input": [doc.content for doc in documents],
        }
        response = requests.post(f"{self.endpoint}/embeddings", json=payload, timeout=self.timeout)
        response.raise_for_status()
        data = response.json()["data"]

        for doc, embedding in zip(documents, data, strict=False):
            doc.embedding = embedding["embedding"]

        return {"documents": documents}


@component
class QdrantWriter:
    """Haystack component to upsert documents with embeddings into Qdrant."""

    def __init__(
        self,
        host: str,
        port: int,
        api_key: str | None = None,
        collection: str = "documents",
        vector_size: int = 1024,
    ):
        self.client = QdrantClient(host=host, port=port, api_key=api_key)
        self.collection = collection
        self.vector_size = vector_size
        self._ensure_collection()

    def _ensure_collection(self):
        if self.collection in [c.name for c in self.client.get_collections().collections]:
            return

        self.client.recreate_collection(
            collection_name=self.collection,
            vectors_config=VectorParams(size=self.vector_size, distance=Distance.COSINE),
        )

    @component.output_types(count=int)
    def run(self, documents: list[Document]):
        points: list[PointStruct] = []
        for doc in documents:
            if doc.embedding is None:
                continue

            payload: dict[str, object] = {"source": doc.meta.get("source", "")}
            payload.update(doc.meta)
            payload["content"] = doc.content

            point_id = doc.id
            if point_id is None:
                point_id = str(uuid.uuid4())

            points.append(PointStruct(id=point_id, vector=doc.embedding, payload=payload))

        if points:
            self.client.upsert(collection_name=self.collection, points=points)

        return {"count": len(points)}
