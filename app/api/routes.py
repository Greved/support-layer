import logging
import time
from typing import Annotated

from fastapi import APIRouter, Depends, Header, HTTPException
from pydantic import BaseModel

from app.api.metrics import RAG_LATENCY, RAG_LLM_ERRORS, RAG_REQUESTS, RAG_TOKENS
from app.core.config import Settings, get_settings
from app.services.query_service import QueryError, run_query

router = APIRouter()
logger = logging.getLogger(__name__)


class HealthResponse(BaseModel):
    name: str
    status: str


class QueryRequest(BaseModel):
    query: str
    filters: dict[str, str] | None = None


class SourceInfo(BaseModel):
    file: str
    page: int | None = None
    offset: int | None = None
    relevance_score: float | None = None
    brief_content: str


class QueryResponse(BaseModel):
    answer: str
    sources: list[SourceInfo]


@router.get("/healthz", response_model=HealthResponse, tags=["meta"])
def health(settings: Annotated[Settings, Depends(get_settings)]) -> HealthResponse:
    return HealthResponse(name=settings.app_name, status="ok")


@router.post("/query", response_model=QueryResponse, tags=["query"])
def query(payload: QueryRequest, x_tenant_id: str = Header(...)) -> QueryResponse:
    settings = get_settings()
    start = time.time()
    logger.info("Received query request tenant=%s filters=%s", x_tenant_id, payload.filters)
    try:
        result = run_query(settings, x_tenant_id, payload.query, payload.filters)
        duration = time.time() - start
        sources_count = len(result.get("sources", []))
        logger.info(
            "Query answered sources=%s duration_ms=%s provider=%s",
            sources_count,
            int(duration * 1000),
            settings.llm_provider,
        )
        RAG_REQUESTS.labels(tenant_id=x_tenant_id, status="success").inc()
        RAG_LATENCY.labels(tenant_id=x_tenant_id).observe(duration)
        token_count = result.get("token_count", 0) or 0
        if token_count:
            RAG_TOKENS.labels(tenant_id=x_tenant_id).inc(token_count)
        return QueryResponse(**result)
    except QueryError as exc:
        logger.error("Query failed", exc_info=exc)
        RAG_REQUESTS.labels(tenant_id=x_tenant_id, status="error").inc()
        RAG_LLM_ERRORS.labels(tenant_id=x_tenant_id, provider=settings.llm_provider).inc()
        raise HTTPException(status_code=503, detail="Query pipeline unavailable") from exc
