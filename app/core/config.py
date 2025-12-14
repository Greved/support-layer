from functools import lru_cache

from pydantic import AnyHttpUrl, Field
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    app_name: str = Field("Tech Support RAG API", description="Human readable service name")
    api_prefix: str = Field("/api", description="REST router prefix")
    debug: bool = Field(False, description="Enable FastAPI debug mode")

    qdrant_host: str = Field("localhost", description="Qdrant hostname")
    qdrant_port: int = Field(6333, description="Qdrant HTTP port")
    qdrant_api_key: str | None = Field(default=None, description="Optional Qdrant API key")
    qdrant_collection: str = Field(
        "documents", description="Qdrant collection name for RAG content"
    )

    llm_provider: str = Field(
        "local",
        description="LLM provider to use: 'local' for llama.cpp or 'gemini' for Google Gemini",
    )
    llama_llm_url: AnyHttpUrl = Field(
        "http://localhost:8080/v1",
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

    log_level: str = Field("INFO", description="Logging level for the application")
    log_file: str = Field("logs/app.log", description="Path to the application log file")

    ingest_bucket: str | None = Field(
        default=None,
        description="Optional bucket/root directory where exported Confluence/PDF docs are stored",
    )
    telemetry_sink: str | None = Field(
        default=None,
        description="Optional Prometheus pushgateway URI",
    )

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """Return cached application settings."""

    return Settings()
