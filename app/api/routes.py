import logging
import time
from typing import Annotated

from fastapi import APIRouter, Depends
from pydantic import BaseModel

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
    brief_content: str


class QueryResponse(BaseModel):
    answer: str
    sources: list[SourceInfo]


@router.get("/healthz", response_model=HealthResponse, tags=["meta"])
def health(settings: Annotated[Settings, Depends(get_settings)]) -> HealthResponse:
    return HealthResponse(name=settings.app_name, status="ok")


@router.post("/query", response_model=QueryResponse, tags=["query"])
def query(payload: QueryRequest) -> QueryResponse:
    settings = get_settings()
    start = time.time()
    logger.info("Received query request filters=%s", payload.filters)
    try:
        result = run_query(settings, payload.query, payload.filters)
        logger.info(
            "Query answered sources=%s duration_ms=%s provider=%s",
            len(result.get("sources", [])),
            int((time.time() - start) * 1000),
            settings.llm_provider,
        )
        return QueryResponse(**result)
    except QueryError as exc:
        logger.error("Query failed", exc_info=exc)
        raise
