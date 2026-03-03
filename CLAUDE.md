# Tech Support RAG API

FastAPI + Haystack 2.x RAG service. Embeds docs into Qdrant, answers queries via local LLM (llama.cpp/vLLM) or Gemini.
See `docs/project_reference.md` for full architecture, env vars, and Docker details.

## Stack
Python 3.12, FastAPI, Haystack 2.x, Qdrant, llama.cpp (CPU/GGUF) or vLLM (GPU/HF), pydantic-settings, Typer CLI.

## Key paths
- `app/core/config.py` — all settings (env vars)
- `app/services/query_service.py` — embed → search → LLM pipeline
- `ingestion/cli.py` — document ingestion CLI
- `.env.example` — env var template

## Common commands
```bash
make compose-infra          # start Qdrant + llama.cpp (CPU)
docker compose -f docker-compose.vllm.yml up -d  # GPU stack
make run                    # uvicorn with hot-reload
make lint / format / test
python -m ingestion.cli ingest --path "data/**/*.md"
```

## LLM providers (`LLM_PROVIDER` env)
- `local` — llama.cpp or vLLM at `LLAMA_LLM_URL` (filters `<think>` blocks)
- `lmstudio` — LM Studio at `LM_STUDIO_URL`
- `gemini` — Google Gemini, requires `GEMINI_API_KEY`

## Ports
| Service | Port |
|---------|------|
| FastAPI | 8000 |
| LLM server | 8080 |
| Embedding server | 8081 |
| Qdrant | 6333 |
