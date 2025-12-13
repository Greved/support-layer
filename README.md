# Tech Support RAG API

Local-first Retrieval-Augmented Generation stack powered by FastAPI, Haystack, llama.cpp-hosted Qwen 3 4B, and Qdrant. Use docker-compose to bring up dependencies or run the app locally with your preferred IDE/venv.

## Ingestion (exported Confluence/Markdown/PDF/HTML)
Use the Haystack-driven ingestion CLI to index exported files into Qdrant while calling the local llama.cpp embedding server:

```bash
py -3.12 -m ingestion.cli ingest --path "data/**/*.pdf" --path "data/**/*.html" --path "data/**/*.md"
```

You can also drive ingestion from YAML config (sources, chunking, artifacts) using the example at `ingestion/config/filesystem.example.yaml`:

```bash
py -3.12 -m ingestion.cli ingest --config ingestion/config/filesystem.example.yaml
```

If the CLI reports no documents found, ensure your glob patterns resolve locally (Windows example: `dir data -Recurse | findstr .pdf`) and rerun with `--path "data/**/*.pdf"`. Paths are resolved relative to the config file by default; use `--base-dir` to override.

If the embedding server crashes on large batches, reduce chunk size in config (e.g., `split_length: 128`, `split_overlap: 32` as in the example).

Configuration is read from environment variables defined in `app/core/config.py` (Qdrant host/port/API key and llama embedding endpoint).

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
- Install dev tooling: `make install`
- Install git hooks: `make hooks`
- Lint: `make lint` (ruff + black check)
- Auto-format + fix lint: `make format`
- Run all pre-commit hooks: `make pre-commit`
