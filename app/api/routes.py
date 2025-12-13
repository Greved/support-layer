from typing import Annotated

from fastapi import APIRouter, Depends
from pydantic import BaseModel

from app.core.config import Settings, get_settings

router = APIRouter()


class HealthResponse(BaseModel):
    name: str
    status: str


class QueryRequest(BaseModel):
    query: str
    filters: dict[str, str] | None = None


class QueryResponse(BaseModel):
    answer: str
    sources: list[str]


@router.get("/healthz", response_model=HealthResponse, tags=["meta"])
def health(settings: Annotated[Settings, Depends(get_settings)]) -> HealthResponse:
    return HealthResponse(name=settings.app_name, status="ok")


@router.post("/query", response_model=QueryResponse, tags=["query"])
def query(payload: QueryRequest) -> QueryResponse:
    # TODO: wire to Haystack retrieval + generation pipeline.
    return QueryResponse(answer="Pipeline not wired yet", sources=[])
