# Tech Support RAG API

Local-first Retrieval-Augmented Generation stack powered by FastAPI, Haystack, llama.cpp-hosted Qwen 3 4B, and Qdrant. Use docker-compose to bring up dependencies or run the app locally with your preferred IDE/venv.

## Ingestion (exported Confluence/Markdown/PDF/HTML)
Use the Haystack-driven ingestion CLI to index exported files into Qdrant while calling the local llama.cpp embedding server:

```bash
py -3.12 -m ingestion.cli ingest --path "data/**/*.pdf" --path "data/**/*.html" --path "data/**/*.md"
```

To index Markdown files you drop into `data/`:

```bash
py -3.12 -m ingestion.cli ingest --path "data/**/*.md"
```

You can also drive ingestion from YAML config (sources, chunking, artifacts) using the example at `ingestion/config/filesystem.example.yaml`:

```bash
py -3.12 -m ingestion.cli ingest --config ingestion/config/filesystem.example.yaml
```

If the CLI reports no documents found, ensure your glob patterns resolve locally (Windows example: `dir data -Recurse | findstr .pdf`) and rerun with `--path "data/**/*.pdf"`. When `--path` is used, relative patterns resolve from your current working directory unless you set `--base-dir`.

If the embedding server crashes on large batches, reduce chunk size in config (e.g., `split_length: 128`, `split_overlap: 32` as in the example).

Re-ingesting a file path will purge existing chunks for that path from Qdrant and then write the updated content.

Configuration is read from environment variables defined in `app/core/config.py` (Qdrant host/port/API key and llama embedding endpoint).

LLM selection:
- Local llama.cpp (default): set `LLM_PROVIDER=local`, `LLAMA_LLM_URL`, `LLAMA_LLM_MODEL`.
- Google Gemini: set `LLM_PROVIDER=gemini`, `GEMINI_API_KEY`, and optional `GEMINI_MODEL` (default `gemini-2.0-pro`).

## Secrets-based startup (Phase 5)
- Prefer file-backed secrets in production-like runs (`*_FILE` vars) instead of plaintext env vars.
- Create local secret files from templates in `secrets/templates/` and store actual values in `secrets/*.txt`.
- Start with compose override:
  - `docker compose -f docker-compose.infra.yml -f docker-compose.secrets.yml up -d`
  - `docker compose -f docker-compose.full.yml -f docker-compose.secrets.yml up -d`
- `.NET` APIs also support file-backed keys:
  - `ConnectionStrings__Default_FILE`, `Jwt__Key_FILE`, `AdminJwt__Key_FILE`,
    `RagCore__InternalSecret_FILE`, `Redis__ConnectionString_FILE`
- Python app supports:
  - `DATABASE_URL_FILE`, `INTERNAL_SECRET_FILE`, `GEMINI_API_KEY_FILE`,
    `QDRANT_API_KEY_FILE`, `REDIS_URL_FILE`

## OWASP ZAP gate (Phase 5)
- CI workflow: `.github/workflows/zap-baseline.yml`
- Trigger options:
  - `workflow_dispatch` with `target_url`
  - weekly schedule (uses repository secret `ZAP_TARGET_URL`)
- Gate rule: workflow fails when ZAP reports any High or Medium findings.
- Local run command:
  - `powershell -ExecutionPolicy Bypass -File deploy/security/run-zap-baseline.ps1 -TargetUrl http://host.docker.internal:8088`
  - Optional hard cap: `-TotalMaxMinutes` (maps to ZAP `-T`, total runtime cap)
- Output artifacts:
  - `artifacts/zap/zap-baseline.json`
  - `artifacts/zap/zap-baseline.html`
  - `artifacts/zap/zap-baseline.md`

## Dockerfiles
- `Dockerfile.app`: builds the FastAPI/Haystack app image (defaults to uvicorn on port 8000).
- `Dockerfile.llama`: thin wrapper over `ghcr.io/ggml-org/llama.cpp:server`; pass model/args at runtime.
- `.env.example`: template for local/env vars (`QDRANT_HOST`, `LLAMA_*`, etc.).

## Useful URLs (default ports)
- FastAPI: http://localhost:8000/api/healthz, docs at http://localhost:8000/api/docs
- Qdrant: http://localhost:6333/dashboard (if dashboard enabled) / health at http://localhost:6333/health
- llama.cpp LLM: http://localhost:8080/health
- llama.cpp embeddings: http://localhost:8081/health

See `agents.md` and `docs/development_plan.md` for detailed architecture and delivery plan.

## Code quality
- Install dev tooling: `py -3.12 -m pip install .[dev]`
- Install git hooks: `py -3.12 -m pre-commit install`
- Lint: `py -3.12 -m ruff check . && py -3.12 -m black --check .`
- Auto-format + fix lint: `py -3.12 -m black . && py -3.12 -m ruff check --fix .`
- Run all pre-commit hooks: `py -3.12 -m pre-commit run --all-files`
