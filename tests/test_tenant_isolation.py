"""Tests for multi-tenant isolation — collection naming and header enforcement."""

from __future__ import annotations

from unittest.mock import MagicMock, patch

import pytest
from fastapi.testclient import TestClient

from app.core.config import Settings
from app.main import create_app

# ---------------------------------------------------------------------------
# Collection name generation
# ---------------------------------------------------------------------------


def test_collection_name_from_tenant_id():
    """QdrantService.search resolves the collection as tenant_{id}."""
    from app.services.qdrant_service import QdrantService

    settings = Settings()
    svc = QdrantService.__new__(QdrantService)
    svc.settings = settings
    svc.client = MagicMock()
    svc.client.search = MagicMock(return_value=[])

    tenant_id = "acme"
    svc.search(vector=[0.1] * 4, tenant_id=tenant_id, top_k=1)

    call_kwargs = svc.client.search.call_args
    assert call_kwargs is not None
    collection_used = call_kwargs.kwargs.get("collection_name") or call_kwargs.args[0]
    assert collection_used == f"tenant_{tenant_id}"


# ---------------------------------------------------------------------------
# Header enforcement on /query
# ---------------------------------------------------------------------------


@pytest.fixture()
def client():
    app = create_app()
    return TestClient(app, raise_server_exceptions=False)


def test_query_missing_tenant_header(client: TestClient):
    """POST /api/query without X-Tenant-ID must return 422."""
    response = client.post(
        "/api/query",
        json={"query": "hello"},
    )
    assert response.status_code == 422


def test_query_with_tenant_header_reaches_pipeline(client: TestClient):
    """POST /api/query with X-Tenant-ID header passes header validation (may fail downstream)."""
    with patch("app.api.routes.run_query") as mock_run:
        mock_run.return_value = {"answer": "ok", "sources": []}
        response = client.post(
            "/api/query",
            json={"query": "hello"},
            headers={"X-Tenant-ID": "acme"},
        )
    # Header was accepted — run_query was called with the tenant id
    mock_run.assert_called_once()
    call_args = mock_run.call_args
    assert call_args.args[1] == "acme" or call_args.kwargs.get("tenant_id") == "acme"
    assert response.status_code == 200


# ---------------------------------------------------------------------------
# Internal health secret enforcement
# ---------------------------------------------------------------------------


def test_internal_health_wrong_secret(client: TestClient):
    """GET /internal/healthz with wrong secret must return 403."""
    response = client.get(
        "/internal/healthz",
        headers={"X-Internal-Secret": "wrong-secret"},
    )
    assert response.status_code == 403


def test_internal_health_missing_secret(client: TestClient):
    """GET /internal/healthz without X-Internal-Secret must return 422."""
    response = client.get("/internal/healthz")
    assert response.status_code == 422
