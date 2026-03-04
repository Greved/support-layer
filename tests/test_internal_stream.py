"""Tests for POST /internal/stream auth guards and SSE response."""

from __future__ import annotations

from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient

from app.core.config import get_settings
from app.main import create_app

GOOD_SECRET = "test-secret"


@pytest.fixture()
def client(monkeypatch):
    monkeypatch.setenv("INTERNAL_SECRET", GOOD_SECRET)
    monkeypatch.setenv("LLM_PROVIDER", "local")
    monkeypatch.setenv("LLAMA_LLM_URL", "http://localhost:8080")
    monkeypatch.setenv("LLAMA_EMBEDDING_URL", "http://localhost:8081")
    get_settings.cache_clear()
    yield TestClient(create_app())
    get_settings.cache_clear()


def _stream_payload():
    return {"tenant_id": "test-tenant", "query": "hello"}


def test_stream_wrong_secret(client):
    """POST /internal/stream with wrong secret → 403."""
    response = client.post(
        "/internal/stream",
        json=_stream_payload(),
        headers={"X-Internal-Secret": "wrong-secret"},
    )
    assert response.status_code == 403


def test_stream_missing_secret(client):
    """POST /internal/stream without X-Internal-Secret header → 422."""
    response = client.post(
        "/internal/stream",
        json=_stream_payload(),
    )
    assert response.status_code == 422


def test_stream_returns_sse_content_type(client):
    """POST /internal/stream with correct secret → Content-Type: text/event-stream."""

    def _mock_stream_query(*args, **kwargs):
        yield 'data: {"type": "sources", "sources": []}\n\n'
        yield 'data: {"type": "token", "chunk": "hello"}\n\n'
        yield 'data: {"type": "done", "answer": "hello"}\n\n'

    with patch("app.api.internal.stream_query", side_effect=_mock_stream_query):
        response = client.post(
            "/internal/stream",
            json=_stream_payload(),
            headers={"X-Internal-Secret": GOOD_SECRET},
        )

    assert response.status_code == 200
    assert "text/event-stream" in response.headers["content-type"]
