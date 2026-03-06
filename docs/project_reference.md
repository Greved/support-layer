# Project Reference

## Directory Structure

```
.
├── app/
│   ├── main.py                        # FastAPI app factory + lifespan
│   ├── api/routes.py                  # GET /healthz, POST /query
│   ├── core/
│   │   ├── config.py                  # pydantic-settings Settings (all env vars)
│   │   └── logging_config.py
│   └── services/
│       ├── embedding_service.py       # calls /v1/embeddings
│       ├── qdrant_service.py          # vector search (multi-version qdrant-client)
│       └── query_service.py           # run_query(), generate_answer(), snippet helpers
├── ingestion/
│   ├── cli.py                         # Typer CLI: python -m ingestion.cli ingest
│   ├── config/filesystem.example.yaml # sources / chunking / artifacts config
│   └── pipelines/components.py        # LlamaCppEmbedding Haystack component
├── models/
│   ├── embedding/                     # GGUF embedding models (llama.cpp only)
│   └── llm/                           # GGUF LLM models (llama.cpp only)
├── data/                              # Drop source documents here
├── artifacts/ingestion/               # NDJSON chunk dumps (debug)
├── Dockerfile.app                     # python:3.12-slim, uvicorn on 8000
├── Dockerfile.llama                   # thin wrapper over llama.cpp:server
├── docker-compose.infra.yml           # Qdrant + llama-embed + llama-llm (CPU)
├── docker-compose.full.yml            # Infra + FastAPI app (CPU)
└── docker-compose.vllm.yml            # Qdrant + vLLM LLM + vLLM embed + app (GPU)
```

## Configuration (env vars)

All settings in `app/core/config.py`. Copy `.env.example` → `.env`.

| Variable | Default | Description |
|----------|---------|-------------|
| `QDRANT_HOST` | `localhost` | Qdrant hostname |
| `QDRANT_PORT` | `6333` | Qdrant HTTP port |
| `QDRANT_API_KEY` | — | Optional auth key |
| `QDRANT_COLLECTION` | `documents` | Collection name |
| `LLM_PROVIDER` | `local` | `local`, `lmstudio`, or `gemini` |
| `LLAMA_LLM_URL` | `http://localhost:8080/v1` | LLM endpoint (llama.cpp or vLLM) |
| `LLAMA_LLM_MODEL` | `qwen2.5-4b-instruct` | Model name for chat endpoint |
| `LLAMA_EMBEDDING_URL` | `http://localhost:8081/v1` | Embedding endpoint |
| `LLAMA_EMBEDDING_MODEL` | `bge-large-en-v1.5` | Embedding model name |
| `GEMINI_API_KEY` | — | Google Gemini API key |
| `GEMINI_MODEL` | `gemini-2.5-flash` | Gemini model |
| `LM_STUDIO_URL` | `http://127.0.0.1:1234/v1` | LM Studio endpoint |
| `LM_STUDIO_MODEL` | `qwen2.5-4b-instruct` | LM Studio model |
| `LOG_LEVEL` | `INFO` | Logging level |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | — | OTLP HTTP traces endpoint (e.g. `http://tempo:4318/v1/traces`) |

For production-like deployments, prefer file-backed secrets over plaintext values:
- Python app: `DATABASE_URL_FILE`, `INTERNAL_SECRET_FILE`, `GEMINI_API_KEY_FILE`, `QDRANT_API_KEY_FILE`, `REDIS_URL_FILE`.
- .NET APIs: `ConnectionStrings__Default_FILE`, `Jwt__Key_FILE`, `AdminJwt__Key_FILE`, `RagCore__InternalSecret_FILE`, `Redis__ConnectionString_FILE`.
- Compose secret wiring is defined in `docker-compose.secrets.yml`; local examples are in `secrets/templates/`.

## Docker Compose

| File | Services | GPU |
|------|----------|-----|
| `docker-compose.infra.yml` | Qdrant + llama-embed + llama-llm | No |
| `docker-compose.full.yml` | Infra + FastAPI app | No |
| `docker-compose.vllm.yml` | Qdrant + vLLM LLM + vLLM embed + app | Yes (NVIDIA) |

vLLM requirements: NVIDIA GPU (CUDA 12+), `nvidia-container-toolkit`, Docker `--gpus` support.
Overridable via env: `VLLM_LLM_MODEL`, `VLLM_EMBED_MODEL`, `HUGGING_FACE_HUB_TOKEN`.

## Models

### llama.cpp (CPU, GGUF)
- LLM: `models/llm/Qwen3-4B-Q4_K_M.gguf`
- Embedding: `models/embedding/bge-large-en-v1.5-q8_0.gguf`

### vLLM (GPU, HuggingFace — auto-downloaded)
- LLM: `Qwen/Qwen2.5-3B-Instruct`
- Embedding: `BAAI/bge-large-en-v1.5`
- Weights cached in `hf_cache` Docker volume

## API Endpoints

| Method | Path | Body |
|--------|------|------|
| GET | `/api/healthz` | — |
| POST | `/api/query` | `{"query": "...", "filters": null}` |

Docs at `http://localhost:8000/api/docs`.

## Ingestion CLI

```bash
# Ingest files
python -m ingestion.cli ingest --path "data/**/*.md" --path "data/**/*.pdf"

# From YAML config
python -m ingestion.cli ingest --config ingestion/config/filesystem.example.yaml

# Single file
python -m ingestion.cli ingest path/to/file.md
```

Re-ingesting purges existing chunks for that source before writing new ones.
Set `artifacts_dir` in config to dump NDJSON chunks to `artifacts/ingestion/`.

## Local Development

```bash
python -m venv .venv && source .venv/bin/activate
pip install -e ".[dev]"
make compose-infra   # start Qdrant + llama.cpp
make run             # uvicorn --reload on :8000
make hooks           # install pre-commit hooks
```

## Code Quality

```bash
make lint        # ruff check + black --check
make format      # black + ruff --fix
make test        # pytest
make pre-commit  # all hooks
```
