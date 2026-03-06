import os
from functools import lru_cache
from pathlib import Path

from pydantic import AnyHttpUrl, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")

    app_name: str = Field("Tech Support RAG API", description="Human readable service name")
    api_prefix: str = Field("/api", description="REST router prefix")
    debug: bool = Field(False, description="Enable FastAPI debug mode")

    qdrant_host: str = Field("localhost", description="Qdrant hostname")
    qdrant_port: int = Field(6335, description="Qdrant HTTP port")
    qdrant_api_key: str | None = Field(default=None, description="Optional Qdrant API key")
    qdrant_collection: str = Field(
        "documents", description="Qdrant collection name for RAG content"
    )

    llm_provider: str = Field(
        "local",
        description="LLM provider to use: 'local' for llama.cpp or 'gemini' for Google Gemini",
    )
    llama_llm_url: AnyHttpUrl = Field(
        "http://localhost:8082/v1",
        description="HTTP endpoint of llama.cpp server hosting the chat/generation model",
    )
    llama_llm_model: str = Field(
        "qwen2.5-4b-instruct",
        description="Model name to send to llama.cpp chat completion endpoint",
    )
    lm_studio_url: AnyHttpUrl = Field(
        "http://127.0.0.1:1234/v1",
        description="Base URL for LM Studio OpenAI-compatible endpoint",
    )
    lm_studio_model: str = Field(
        "qwen2.5-4b-instruct",
        description="Model name to send to LM Studio chat completion endpoint",
    )
    gemini_api_key: str | None = Field(default=None, description="Google Gemini API key")
    gemini_model: str = Field("gemini-2.5-flash", description="Gemini model name")
    llama_embedding_url: AnyHttpUrl = Field(
        "http://localhost:8081/v1",
        description="HTTP endpoint of llama.cpp server hosting the embedding model",
    )
    llama_embedding_model: str = Field(
        "bge-large-en-v1.5", description="Model name to send to llama.cpp embedding endpoint"
    )

    query_result_sources_max_count: int = Field(
        5, description="Max number of source items to return"
    )
    query_context_sources_max_count: int = Field(
        20, description="Max number of retrieved source items to pass to the LLM"
    )
    query_result_brief_content_max_len: int = Field(
        400, description="Max length of each source brief_content"
    )

    log_level: str = Field("INFO", description="Logging level for the application")
    log_file: str = Field("logs/app.log", description="Path to the application log file")

    internal_secret: str = Field(
        "change-me-secret", description="Shared secret for X-Internal-Secret header"
    )
    database_url: str | None = Field(default=None, description="PostgreSQL connection URL")
    redis_url: str | None = Field(default=None, description="Redis connection URL")

    ingest_bucket: str | None = Field(
        default=None,
        description="Optional bucket/root directory where exported Confluence/PDF docs are stored",
    )
    telemetry_sink: str | None = Field(
        default=None,
        description="Optional Prometheus pushgateway URI",
    )


_FILE_BACKED_FIELDS: tuple[tuple[str, str], ...] = (
    ("qdrant_api_key", "QDRANT_API_KEY_FILE"),
    ("gemini_api_key", "GEMINI_API_KEY_FILE"),
    ("internal_secret", "INTERNAL_SECRET_FILE"),
    ("database_url", "DATABASE_URL_FILE"),
    ("redis_url", "REDIS_URL_FILE"),
)


def _apply_file_backed_overrides(settings: Settings) -> None:
    for field_name, file_env in _FILE_BACKED_FIELDS:
        path = os.getenv(file_env)
        if not path:
            continue

        secret_file = Path(path)
        if not secret_file.exists():
            raise RuntimeError(f"Secret file from {file_env} was not found: {secret_file}")

        setattr(settings, field_name, secret_file.read_text(encoding="utf-8").strip())


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """Return cached application settings."""

    settings = Settings()
    _apply_file_backed_overrides(settings)
    return settings
