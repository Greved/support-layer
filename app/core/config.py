from functools import lru_cache
from typing import Any

from pydantic import AnyHttpUrl, Field
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    app_name: str = Field("Tech Support RAG API", description="Human readable service name")
    api_prefix: str = Field("/api", description="REST router prefix")
    debug: bool = Field(False, description="Enable FastAPI debug mode")

    qdrant_host: str = Field("qdrant", description="Qdrant hostname")
    qdrant_port: int = Field(6333, description="Qdrant HTTP port")
    qdrant_api_key: str | None = Field(default=None, description="Optional Qdrant API key")

    llama_llm_url: AnyHttpUrl = Field(
        "http://llm:8080/v1",
        description="HTTP endpoint of llama.cpp server hosting the chat/generation model",
    )
    llama_embedding_url: AnyHttpUrl = Field(
        "http://embeddings:8081/v1",
        description="HTTP endpoint of llama.cpp server hosting the embedding model",
    )

    ingest_bucket: str | None = Field(
        default=None,
        description="Optional bucket/root directory where exported Confluence/PDF docs are stored",
    )
    telemetry_sink: str | None = Field(default=None, description="Optional Prometheus pushgateway URI")

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """Return cached application settings."""

    return Settings()
