import re
from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient

from app.core.config import get_settings


@pytest.fixture
def client():
    get_settings.cache_clear()
    with patch("app.api.routes.run_query") as mock_run:
        mock_run.return_value = {"answer": "Test answer", "sources": [], "token_count": 42}
        from app.main import create_app

        app = create_app()
        with TestClient(app) as c:
            yield c
    get_settings.cache_clear()


def test_metrics_endpoint_exposed(client):
    r = client.get("/metrics")
    assert r.status_code == 200
    assert "rag_requests_total" in r.text


def test_metrics_content_type(client):
    r = client.get("/metrics")
    assert "text/plain" in r.headers.get("content-type", "")


def test_metrics_ingest_counter_present(client):
    r = client.get("/metrics")
    assert r.status_code == 200
    assert "rag_ingest_requests_total" in r.text


def test_query_records_token_metrics(client):
    response = client.post(
        "/api/query",
        headers={"X-Tenant-Id": "tenant-metrics"},
        json={"query": "How do I reset my password?"},
    )
    assert response.status_code == 200

    metrics = client.get("/metrics")
    assert metrics.status_code == 200
    match = re.search(
        r'rag_tokens_total\{tenant_id="tenant-metrics"\}\s+([0-9]+(?:\.[0-9]+)?)',
        metrics.text,
    )
    assert match is not None
    assert float(match.group(1)) >= 42
