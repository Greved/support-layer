# Tech Support RAG API

Local-first Retrieval-Augmented Generation stack powered by FastAPI, Haystack, llama.cpp-hosted Qwen 3 4B, and Qdrant. Use docker-compose to bring up dependencies or run the app locally with your preferred IDE/venv.

## Ingestion (exported Confluence/Markdown/PDF/HTML)
Use the Haystack-driven ingestion CLI to index exported files into Qdrant while calling the local llama.cpp embedding server:

```bash
python -m ingestion.cli ingest \"data/**/*.pdf\" \"data/**/*.html\" \"data/**/*.md\"
```

Configuration is read from environment variables defined in `app/core/config.py` (Qdrant host/port/API key and llama embedding endpoint).

See `agents.md` and `docs/development_plan.md` for detailed architecture and delivery plan.

## Code quality
- Install dev tooling: `make install`
- Install git hooks: `make hooks`
- Lint: `make lint` (ruff + black check)
- Auto-format + fix lint: `make format`
- Run all pre-commit hooks: `make pre-commit`
