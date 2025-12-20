from __future__ import annotations

import json
import logging
import re
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
        "IMPORTANT RULES: "
        "- Do NOT include reasoning, thoughts, analysis, or explanations."
        '- Do NOT include phrases like "Let\'s think", "Reasoning:", or similar.'
        "- Output ONLY the final answer."
        '- If unsure, say "I don\'t know".'
        "Violating these rules is incorrect."
        "Return a concise bullet list if the answer implies a list. "
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
                    "max_output_tokens": 512,
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

    # Default: local llama.cpp or LM Studio (OpenAI-compatible)
    use_lm_studio = settings.llm_provider.lower() == "lmstudio"
    base_url = settings.lm_studio_url if use_lm_studio else settings.llama_llm_url
    model_name = settings.lm_studio_model if use_lm_studio else settings.llama_llm_model
    logger.info(
        "Generating answer via %s model=%s",
        "lmstudio" if use_lm_studio else "llama.cpp",
        model_name,
    )
    payload = {
        "model": model_name,
        "messages": _to_openai_messages(messages),
        "temperature": 0,
        "max_tokens": 64000,
        "stream": True,
    }
    try:
        resp = requests.post(f"{base_url}/chat/completions", json=payload, timeout=180, stream=True)
        resp.raise_for_status()
    except requests.RequestException as exc:
        logger.error("LLM request failed", exc_info=exc)
        raise QueryError("LLM request failed") from exc

    content_chunks: list[str] = []
    reasoning_chunks: list[str] = []
    in_reasoning = False

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
            message = choice.get("message") or {}
            if "content" in delta and delta["content"]:
                text = delta["content"]
                if "<think>" in text:
                    in_reasoning = True
                    if "</think>" in text:
                        after = text.split("</think>", 1)[1]
                        in_reasoning = False
                        if after:
                            content_chunks.append(after)
                    continue
                if in_reasoning:
                    if "</think>" in text:
                        after = text.split("</think>", 1)[1]
                        in_reasoning = False
                        if after:
                            content_chunks.append(after)
                    else:
                        reasoning_chunks.append(text)
                    continue
                if "</think>" in text:
                    text = text.split("</think>", 1)[1]
                if text:
                    content_chunks.append(text)
            if "content" in message and message["content"]:
                content_chunks.append(message["content"])
            if "reasoning_content" in delta and delta["reasoning_content"]:
                reasoning_chunks.append(delta["reasoning_content"])

    content = "".join(content_chunks).strip()
    reasoning = "".join(reasoning_chunks).strip()

    logger.debug("Llama reasoning captured chars=%s", len(reasoning))

    if content:
        logger.info(
            "Llama generation completed duration_ms=%s",
            int((time.time() - start) * 1000),
        )
        return content

    # No usable content; keep reasoning only for debugging and return a safe fallback.
    logger.warning("LLM returned no content; reasoning chars=%s", len(reasoning))
    raise QueryError("LLM returned an empty message")


_STOPWORDS = {
    "the",
    "and",
    "for",
    "with",
    "from",
    "that",
    "this",
    "what",
    "are",
    "was",
    "were",
    "you",
    "your",
    "about",
    "into",
    "have",
    "has",
    "had",
    "does",
    "did",
    "not",
    "but",
    "can",
    "should",
    "would",
    "could",
    "how",
    "why",
    "when",
    "where",
}


def _normalize_query_terms(query: str) -> list[str]:
    query_lc = query.lower()
    terms = [t for t in re.findall(r"[A-Za-z0-9]+", query_lc) if len(t) >= 3]
    return [t for t in terms if t not in _STOPWORDS]


def _query_match_score(content: str, query: str) -> int:
    if not content:
        return 0
    content_lc = content.lower()
    terms = _normalize_query_terms(query)
    return sum(1 for term in terms if term in content_lc)


def _is_relevant_match(content: str, query: str) -> bool:
    if not content:
        return False
    terms = _normalize_query_terms(query)
    if not terms:
        return False
    content_lc = content.lower()
    required = max(2, len(terms) // 2)
    match_count = sum(1 for term in terms if term in content_lc)
    return match_count >= required


def _build_section_snippet(content: str, query: str, max_lines: int = 10) -> str | None:
    if not content:
        return None
    query_lc = query.lower().strip()
    if not query_lc:
        return None
    terms = _normalize_query_terms(query)
    if not terms:
        return None

    lines = [line.rstrip() for line in content.splitlines()]
    min_hits = max(2, len(terms) - 1)
    for idx, line in enumerate(lines):
        line_lc = line.lower()
        hits = sum(1 for term in terms if term in line_lc)
        if hits >= min_hits:
            section_lines: list[str] = []
            blank_allowed = True
            for next_line in lines[idx:]:
                stripped = next_line.strip()
                if not stripped:
                    if section_lines and not blank_allowed:
                        break
                    if section_lines and blank_allowed:
                        blank_allowed = False
                    continue
                section_lines.append(stripped)
                blank_allowed = False
                if len(section_lines) >= max_lines:
                    break
            snippet = "\n".join(section_lines).strip()
            return snippet or None
    return None


def _build_snippet_from_terms(
    content: str, terms: list[str], max_len: int = 600, max_lines: int = 10
) -> str:
    if not content:
        return ""

    if terms:
        # Find a line that matches most terms, then return it with nearby lines.
        lines = [line.rstrip() for line in content.splitlines()]
        min_hits = max(2, len(terms) - 1)
        for idx, line in enumerate(lines):
            line_lc = line.lower()
            hits = sum(1 for term in terms if term in line_lc)
            if hits >= min_hits:
                section_lines: list[str] = []
                blank_allowed = True
                for next_line in lines[idx:]:
                    stripped = next_line.strip()
                    if not stripped:
                        if section_lines and not blank_allowed:
                            break
                        if section_lines and blank_allowed:
                            blank_allowed = False
                        continue
                    section_lines.append(stripped)
                    blank_allowed = False
                    if len(section_lines) >= max_lines:
                        break
                snippet = "\n".join(section_lines).strip()
                if snippet:
                    return snippet[:max_len].strip()

    text = re.sub(r"\s+", " ", content).strip()
    if len(text) <= max_len:
        return text

    idx = -1
    for term in terms:
        if not term:
            continue
        pos = text.lower().find(term)
        if pos != -1:
            idx = pos
            break

    if idx == -1:
        return text[:max_len].strip()

    half = max_len // 2
    start = max(0, idx - half)
    end = min(len(text), start + max_len)
    snippet = text[start:end].strip()
    if start > 0:
        snippet = f"...{snippet}"
    if end < len(text):
        snippet = f"{snippet}..."
    return snippet


def _build_snippet(content: str, query: str, answer: str, max_len: int = 600) -> str:
    if not content:
        return ""

    answer_terms = _normalize_query_terms(answer)
    if answer_terms:
        return _build_snippet_from_terms(content, answer_terms, max_len=max_len)

    query_terms = _normalize_query_terms(query)
    if query_terms:
        return _build_snippet_from_terms(content, query_terms, max_len=max_len)

    return re.sub(r"\s+", " ", content).strip()[:max_len].strip()

    text = re.sub(r"\s+", " ", content).strip()
    if len(text) <= max_len:
        return text

    query_lc = query.lower()
    terms = _normalize_query_terms(query)
    candidates = [query_lc] + terms

    idx = -1
    for term in candidates:
        if not term:
            continue
        pos = text.lower().find(term)
        if pos != -1:
            idx = pos
            break

    if idx == -1:
        return text[:max_len].strip()

    half = max_len // 2
    start = max(0, idx - half)
    end = min(len(text), start + max_len)
    snippet = text[start:end].strip()
    if start > 0:
        snippet = f"...{snippet}"
    if end < len(text):
        snippet = f"{snippet}..."
    return snippet


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
        results = searcher.search(
            vector, top_k=settings.query_context_sources_max_count, filters=filters
        )
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
    sources_raw: list[dict[str, Any]] = []

    sources: list[dict[str, Any]] = []
    context_chunks: list[str] = []
    for point in points:
        payload: Payload = _extract_payload(point)
        meta = payload.get("meta") or payload
        source_val = meta.get("source") or meta.get("file_path") or "unknown"
        page = meta.get("page_number")
        offset = meta.get("split_idx_start")
        content = payload.get("content") or meta.get("content") or ""
        score = None
        if hasattr(point, "score"):
            score = point.score
        elif isinstance(point, dict):
            score = point.get("score")

        match_score = _query_match_score(content, query)
        sources_raw.append(
            {
                "file": str(source_val).split("\\")[-1].split("/")[-1],
                "page": page,
                "offset": offset,
                "relevance_score": score,
                "content": content,
                "match_score": match_score,
            }
        )

    if not sources_raw:
        return {
            "answer": "No matching content found in the vector store. Try ingesting documents.",
            "sources": [],
        }

    relevant_sources = [src for src in sources_raw if _is_relevant_match(src["content"], query)]
    selected_sources = relevant_sources or sources_raw
    selected_sources.sort(
        key=lambda src: (
            (
                src["relevance_score"]
                if isinstance(src.get("relevance_score"), int | float)
                else -1.0
            ),
            src["match_score"],
        ),
        reverse=True,
    )

    limit = max(0, int(settings.query_result_sources_max_count))
    for src in selected_sources[:limit] if limit else selected_sources:
        sources.append(
            {
                "file": src["file"],
                "page": src["page"],
                "offset": src["offset"],
                "relevance_score": src["relevance_score"],
                "brief_content": "",
            }
        )
        context_chunks.append(src["content"])

    answer = generate_answer(settings, query, context_chunks)
    if not answer or not answer.strip():
        answer = "I don't know based on the provided context."

    for idx, src in enumerate(selected_sources[: len(sources)]):
        sources[idx]["brief_content"] = _build_snippet(
            src["content"],
            query,
            answer,
            max_len=int(settings.query_result_brief_content_max_len),
        )
    logger.info("Total query finished duration_ms=%s", int((time.time() - total_start) * 1000))
    return {"answer": answer.strip(), "sources": sources}
