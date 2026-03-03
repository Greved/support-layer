"""Tests for /internal/ingest and /internal/query auth guards."""

from __future__ import annotations

from io import BytesIO

import pytest
from fastapi.testclient import TestClient

from app.main import create_app

GOOD_SECRET = "test-secret"


@pytest.fixture()
def client(monkeypatch):
    monkeypatch.setenv("INTERNAL_SECRET", GOOD_SECRET)
    monkeypatch.setenv("LLM_PROVIDER", "local")
    monkeypatch.setenv("LLAMA_LLM_URL", "http://localhost:8080")
    monkeypatch.setenv("LLAMA_EMBEDDING_URL", "http://localhost:8081")
    return TestClient(create_app())


def test_ingest_missing_secret(client):
    """POST /internal/ingest without X-Internal-Secret header → 422."""
    response = client.post(
        "/internal/ingest",
        data={"tenant_id": "test-tenant"},
        files={"file": ("test.txt", BytesIO(b"hello"), "text/plain")},
    )
    assert response.status_code == 422


def test_ingest_wrong_secret(client):
    """POST /internal/ingest with wrong secret → 403."""
    response = client.post(
        "/internal/ingest",
        headers={"X-Internal-Secret": "wrong-secret"},
        data={"tenant_id": "test-tenant"},
        files={"file": ("test.txt", BytesIO(b"hello"), "text/plain")},
    )
    assert response.status_code == 403


def test_internal_query_wrong_secret(client):
    """POST /internal/query with wrong secret → 403."""
    response = client.post(
        "/internal/query",
        headers={"X-Internal-Secret": "wrong-secret"},
        json={"tenant_id": "test-tenant", "query": "what?"},
    )
    assert response.status_code == 403


def test_internal_query_missing_secret(client):
    """POST /internal/query without X-Internal-Secret header → 422."""
    response = client.post(
        "/internal/query",
        json={"tenant_id": "test-tenant", "query": "what?"},
    )
    assert response.status_code == 422
