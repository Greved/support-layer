from __future__ import annotations

import json
import logging
import time
from typing import Any

import requests
from haystack.dataclasses import ChatMessage
from qdrant_client.http.models import Payload

from app.core.config import Settings
from app.services.embedding_service import EmbeddingService
from app.services.qdrant_service import QdrantService

logger = logging.getLogger(__name__)


class QueryError(Exception):
    """Raised when query processing fails."""


def build_prompt(query: str, contexts: list[str]) -> list[ChatMessage]:
    context_block = "\n\n".join(f"[{idx+1}] {c}" for idx, c in enumerate(contexts) if c)
    user_content = (
        "Use ONLY the provided context to answer. "
        "Return a concise bullet list if the answer implies a list. "
        "If the answer is not fully in the context, reply with "
        '"I don\'t know based on the provided context." '
        "Do not invent details. Keep answers short and complete.\n\n"
        f"Context:\n{context_block}\n\nQuestion: {query}"
    )
    return [
        ChatMessage.from_system("You are a concise assistant for document Q&A."),
        ChatMessage.from_user(user_content),
    ]


def generate_answer(settings: Settings, query: str, contexts: list[str]) -> str:
    messages = build_prompt(query, contexts)
    start = time.time()

    def _to_openai_messages(msgs: list[ChatMessage]) -> list[dict[str, str]]:
        openai_msgs: list[dict[str, str]] = []
        for msg in msgs:
            role = getattr(msg, "role", None)
            role_val = getattr(role, "value", None) or str(role) if role else "user"
            content_parts: list[str] = []
            try:
                for block in msg.content:  # type: ignore[attr-defined]
                    text_val = getattr(block, "text", None)
                    if text_val:
                        content_parts.append(text_val)
            except Exception:
                pass
            if not content_parts:
                content_parts.append(str(msg))
            openai_msgs.append({"role": role_val, "content": "".join(content_parts)})
        return openai_msgs

    if settings.llm_provider.lower() == "gemini":
        if not settings.gemini_api_key:
            raise QueryError("Gemini API key is not configured")
        logger.info("Generating answer via Gemini model=%s", settings.gemini_model)
        content_chunks: list[str] = []
        try:
            from haystack.dataclasses import StreamingChunk
            from haystack.utils import Secret
            from haystack_integrations.components.generators.google_genai import (
                GoogleGenAIChatGenerator,
            )

            def _stream_cb(chunk: StreamingChunk):
                if chunk and chunk.content:
                    content_chunks.append(chunk.content)

            generator = GoogleGenAIChatGenerator(
                model=settings.gemini_model,
                api_key=Secret.from_token(settings.gemini_api_key),
                streaming_callback=_stream_cb,
                generation_kwargs={
                    "temperature": 0.5,
                    "max_output_tokens": 500,
                },
            )
            result = generator.run(messages=messages)
            if not content_chunks:
                replies = result.get("replies") or []
                for reply in replies:
                    if not reply:
                        continue
                    if isinstance(reply, ChatMessage):
                        text_blocks = [blk.text for blk in reply.content if hasattr(blk, "text")]
                        if text_blocks:
                            content_chunks.append("".join(text_blocks))
                        else:
                            content_chunks.append(str(reply))
                    else:
                        content_chunks.append(str(reply))
        except Exception as exc:
            logger.error("Gemini request failed", exc_info=exc)
            raise QueryError("Gemini request failed") from exc

        content = "".join(content_chunks).strip()
        if content:
            logger.info(
                "Gemini generation completed duration_ms=%s",
                int((time.time() - start) * 1000),
            )
            return content
        raise QueryError("Gemini returned an empty message")

    # Default: local llama.cpp
    logger.info("Generating answer via llama.cpp model=%s", settings.llama_llm_model)
    payload = {
        "model": settings.llama_llm_model,
        "messages": _to_openai_messages(messages),
        "temperature": 0.2,
        "max_tokens": 1024,
        "stream": True,
    }
    try:
        resp = requests.post(
            f"{settings.llama_llm_url}/chat/completions", json=payload, timeout=180, stream=True
        )
        resp.raise_for_status()
    except requests.RequestException as exc:
        logger.error("LLM request failed", exc_info=exc)
        raise QueryError("LLM request failed") from exc

    content_chunks: list[str] = []
    reasoning_chunks: list[str] = []

    for line in resp.iter_lines(decode_unicode=True):
        if not line:
            continue
        if line.startswith("data:"):
            line = line.removeprefix("data:").strip()
        if line in ("[DONE]", ""):
            continue
        try:
            data = json.loads(line)
        except json.JSONDecodeError:
            continue
        for choice in data.get("choices", []):
            delta = choice.get("delta") or {}
            if "content" in delta and delta["content"]:
                content_chunks.append(delta["content"])
            if "reasoning_content" in delta and delta["reasoning_content"]:
                reasoning_chunks.append(delta["reasoning_content"])

    content = "".join(content_chunks).strip()
    reasoning = "".join(reasoning_chunks).strip()

    if content:
        logger.info(
            "Llama generation completed duration_ms=%s",
            int((time.time() - start) * 1000),
        )
        return content

    if reasoning:
        return reasoning
    raise QueryError("LLM returned an empty message")


def run_query(settings: Settings, query: str, filters: dict[str, Any] | None = None) -> dict:
    embedder = EmbeddingService(settings)
    searcher = QdrantService(settings)
    total_start = time.time()
    embed_start = time.time()
    try:
        vector = embedder.embed_query(query)
        logger.info(
            "Embedding stage finished duration_ms=%s", int((time.time() - embed_start) * 1000)
        )
        search_start = time.time()
        results = searcher.search(vector, filters=filters)
        logger.info(
            "Search stage finished duration_ms=%s", int((time.time() - search_start) * 1000)
        )
    except Exception as exc:  # pragma: no cover
        logger.error("Query pipeline failed unexpectedly", exc_info=exc)
        raise QueryError("Query pipeline failed") from exc

    def _extract_payload(point: Any) -> dict[str, Any]:
        if hasattr(point, "payload"):
            return point.payload or {}
        if isinstance(point, dict):
            return point
        if isinstance(point, tuple | list):
            # Legacy client returns tuples; assume payload is second element if dict-like
            if len(point) > 1 and isinstance(point[1], dict):
                return point[1]
            if len(point) > 0 and isinstance(point[0], dict):
                return point[0]
        if hasattr(point, "dict"):
            try:
                data = point.dict()
                return data.get("payload", {}) or data.get("meta", {}) or {}
            except Exception:
                return {}
        return {}

    points = results.points if hasattr(results, "points") else results

    sources: list[dict[str, Any]] = []
    context_chunks: list[str] = []
    for point in points:
        payload: Payload = _extract_payload(point)
        meta = payload.get("meta") or payload
        source_val = meta.get("source") or meta.get("file_path") or "unknown"
        page = meta.get("page_number")
        offset = meta.get("split_idx_start")
        content = payload.get("content") or meta.get("content") or ""
        sources.append(
            {
                "file": str(source_val).split("\\")[-1].split("/")[-1],
                "page": page,
                "offset": offset,
                "brief_content": content[:200].strip(),
            }
        )
        context_chunks.append(content)

    if not context_chunks:
        return {
            "answer": "No matching content found in the vector store. Try ingesting documents.",
            "sources": [],
        }

    answer = generate_answer(settings, query, context_chunks)
    if not answer or not answer.strip():
        answer = "I don't know based on the provided context."
    logger.info("Total query finished duration_ms=%s", int((time.time() - total_start) * 1000))
    return {"answer": answer.strip(), "sources": sources}
