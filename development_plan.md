# Development Plan For Haystack-Based Local RAG Platform

## Vision & Success Criteria
- Deliver a self-hosted Retrieval-Augmented Generation application that never calls external LLM APIs.
- Support FastAPI-based chat/QA endpoints that rely on Qwen 3 4B (int4) via llama.cpp server and embeddings via `bge-large-en-v1.5-gguf` served the same way.
- Provide a docker-compose setup with two modes: (1) infrastructure-only for IDE debugging, (2) full stack including FastAPI service.
- Prepare the codebase for running the entire stack in Kubernetes with GitOps-friendly manifests and easy llama.cpp hosting.

## Functional Requirements
1. Upload & ingest documents from PDFs, exported Confluence HTML/PDF files, and Markdown repositories.
2. Perform preprocessing (clean, chunk, normacize metadata) before generating embeddings.
3. Store vectors + payloads in Qdrant; support metadata filtering (doc type, space, updated_at, ACL).
4. Serve chat + question answering endpoints with streamed responses and citations.
5. Provide health, readiness, and metrics endpoints for every containerized component.

## Non-Functional Requirements
- **Local-only inference:** All LLM + embedding inference must happen inside llama.cpp servers with no outbound calls.
- **Reproducible environments:** Compose files + `.env` + make targets for deterministic setup.
- **Observability:** Prometheus metrics, structured logging, and optional tracing for ingestion + query paths.
- **Security:** API auth via API keys initially, pluggable OIDC later; enforce filesystem permissions for exported Confluence bundles and other ingestion artifacts.
- **Scalability:** Components deployable separately inside Kubernetes, enabling horizontal scale for ingestion vs. serving.

## High-Level Architecture
1. **FastAPI Application:** Hosts REST & WebSocket endpoints, wires Haystack pipelines, runs Retrieval and Generation agents.
2. **Haystack Ingestion Workers:** Declarative Haystack Ingestion pipelines (CLI + worker pods) that cover acquisition from filesystem/S3 exports, preprocessing, chunking, and embeddings while delegating model inference to local llama.cpp servers.
3. **Qdrant Vector Store:** Stateful service storing vectors/metadata with replication-ready configuration.
4. **llama.cpp Servers:** Two logical servers (embeddings + LLM) possibly sharing binaries but isolated for tuning; accessible through HTTP.
5. **Supporting Services:** Redis (rate limiting + caching), Postgres (ingestion manifests), Prometheus/Grafana stack.

## Detailed Workstreams & Tasks

### 1. Repository Bootstrapping
- [ ] Initialize Python project with uv/poetry/pip-tools (decide) targeting Python 3.11.
- [ ] Define core folders: `app/`, `ingestion/`, `infrastructure/`, `deploy/`.
- [ ] Configure linting/formatting (ruff, black, isort, mypy) + pre-commit hooks.
- [ ] Add Makefile (or `justfile`) with commands: `install`, `lint`, `test`, `compose:infra`, `compose:full`, `ingest:run`.

### 2. FastAPI + Haystack Application
- [ ] Scaffold FastAPI project (`app/main.py`, routers for `/query`, `/chat`, `/health`, `/ingest/status`).
- [ ] Implement dependency injection for Haystack pipelines (`Pipeline`/`Graph`) configured via YAML or Python factory.
- [ ] Add retrieval pipeline: Qdrant retriever + optional BM25 + cross-encoder reranker.
- [ ] Add generation pipeline: PromptBuilder -> Qwen invocation -> Response postprocessor.
- [ ] Support streaming responses (Server-Sent Events or WebSocket) with token-by-token output.
- [ ] Implement doc feedback endpoint writing to Postgres/Redis for evaluation.

### 3. llama.cpp Integration
- [ ] Package `llama.cpp` binaries (submodule or download script) for embeddings + LLM servers.
- [ ] Provide configuration for `bge-large-en-v1.5-gguf` embedding server (HTTP port, concurrency, batching) and `qwen 3 4B int4` generation server.
- [ ] Write thin Python clients (or reuse `llama_cpp_python` HTTP mode) to call each server with retries and timeout metrics.
- [ ] Add health checks verifying models are loaded before FastAPI serves traffic.
- [ ] Document GPU/CPU requirements and fallback instructions (e.g., `LLAMA_ARG` tuning, NUMA pinning).

- [ ] Configure Haystack Ingestion YAML defining sources (filesystem globs for PDFs, exported Confluence HTML/PDF bundles, Markdown repos, optional S3 buckets) with change-detection policies.
- [ ] Provide CLI wrappers (`haystack ingestion run ...`) plus helper scripts to schedule CronJobs and capture run manifests in Postgres.
- [ ] Embed preprocessing/normalization/chunking transformers inside the ingestion graph with tunable chunk params (e.g., 512 tokens / 64 overlap) and OCR fallbacks.
- [ ] Implement custom Haystack Ingestion embedding component that proxies to the llama.cpp embedding server hosting `bge-large-en-v1.5-gguf`, ensuring batching + retries.
- [ ] Leverage Haystack Ingestion writers for Qdrant upserts so vectors + metadata are persisted without leaving the ingestion pipeline.
- [ ] Store intermediate artifacts (clean text, metadata) in a debug-friendly blob store or local disk according to ingestion config, keeping track of exported Confluence versions.

### 5. Retrieval & Qdrant Enhancements
- [ ] Wrap Qdrant operations (collection creation, schema updates) inside management scripts invoked post-ingestion.
- [ ] Implement automatic retries/backoff and caching for embeddings at the ingestion level (hash normalized text + parameters) to avoid duplicate llama.cpp calls.
- [ ] Explore hybrid retrieval options (dense + sparse) leveraging Qdrant sparse vectors or local BM25 index exposed via Haystack retriever nodes.
- [ ] Provide CLI/management commands for ingestion maintenance: rebuild collection, purge doc IDs, re-run embeddings after prompt/parameter changes.

### 6. Docker & Compose Strategy
- [ ] Author base Dockerfiles:
  - `Dockerfile.app` for FastAPI + workers.
  - `Dockerfile.llama` parameterized for embeddings vs. LLM servers.
  - `Dockerfile.ingestion` (optional) if workers run separately.
- [ ] Compose Mode 1 (`docker-compose.infra.yml`): bring up Qdrant, llama embedding server, llama LLM server, Redis, Postgres, Prometheus/Grafana.
- [ ] Compose Mode 2 (`docker-compose.full.yml` or profile): extend Mode 1 by adding FastAPI app, ingestion worker, evaluator service.
- [ ] Provide `.env` templates with ports, data paths, and mount points for models (bind host directories for gguf files).
- [ ] Document workflows for PyCharm remote interpreter vs. dockerized execution.

### 7. Observability, Testing & QA
- [ ] Integrate structured logging (loguru or stdlib) with correlation IDs per request.
- [ ] Expose Prometheus metrics from FastAPI, ingestion workers, and llama servers (via exporters or sidecars).
- [ ] Add tracing hooks (OpenTelemetry) for query path.
- [ ] Write unit tests for loaders, chunkers, pipeline wiring; add integration test that mocks llama server responses.
- [ ] Set up regression harness using Haystack `EvalDocuments` comparing retrieved docs vs. labeled answers.
- [ ] Automate nightly synthetic evaluation that writes reports (Markdown/JSON) tracked in repo or object store.

### 8. Kubernetes & Deployment
- [ ] Generate Helm charts or Kustomize bases for each service (FastAPI, workers, Qdrant, llama servers, Redis, Postgres, Prometheus stack).
- [ ] Define PVCs for model weights + Qdrant snapshots.
- [ ] Add HorizontalPodAutoscaler configs for FastAPI + workers keyed off CPU/latency metrics.
- [ ] Document deployment steps (kind/minikube for dev, managed cluster for prod) including secrets management (ExternalSecrets/SealedSecrets).
- [ ] Provide GitOps-ready manifests for compose parity.

## Milestones & Sequencing
1. **Week 1:** Repo scaffolding, docker compose baseline (Mode 1), llama.cpp servers operational.
2. **Week 2:** Ingestion pipeline (filesystem + exported Confluence bundles) and preprocessing finalize; embeddings stored in Qdrant.
3. **Week 3:** FastAPI endpoints + Haystack pipelines with streaming responses; Compose Mode 2 demo-ready.
4. **Week 4:** Observability, evaluation harness, Kubernetes manifests, documentation polish.

## Risk Register & Mitigations
| Risk | Impact | Mitigation |
| --- | --- | --- |
| llama.cpp model load time too slow | Delays startup | Preload models, mount weights via hostPath/PVC, add readiness gating |
| Stale exported Confluence content | Knowledge drift | Document export cadence, add checksums + alerts when exports are older than threshold |
| Embedding throughput bottleneck | Slow ingestion | Enable batching, consider quantized bge model, scale worker pods |
| Qdrant disk usage growth | Storage exhaustion | Scheduled compaction + snapshot rotation |
| Prompt injection via Confluence content | Hallucinations/leaks | Sanitize inputs, run guardrail checks before indexing |

## Documentation Deliverables
- `README.md` with quickstart, compose instructions, debugging tips.
- `docs/architecture.md` referencing `agents.md` and diagrams (PlantUML/Mermaid).
- `docs/ingestion.md`, `docs/deployment.md`, `docs/operations.md` for runbooks.
- ADRs covering model choices (Qwen, BGE) and infrastructure decisions (Qdrant, llama.cpp hosting).

## Open Points For Stakeholder Review
1. Confirm target hardware (CPU vs. GPU) for llama servers in production.
2. Define operational cadence/ownership for refreshing exported Confluence files and automating their placement into ingestion directories.
3. Choose persistence layer for ingestion manifests (Postgres vs. SQLite) given deployment constraints.
4. Clarify authentication strategy for external consumers (API key, OAuth, or internal-only to start).
5. Define SLA/SLI targets (latency, availability) to size infrastructure appropriately.
