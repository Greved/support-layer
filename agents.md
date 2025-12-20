# Agents For Haystack RAG Platform

## Agent Roster Snapshot
| Agent | Responsibility | Key Technologies | Run Context |
| --- | --- | --- | --- |
| FastAPI Orchestrator | Owns public API, session state, routing through Haystack pipelines, health/readiness endpoints | FastAPI, Haystack Pipeline API, Pydantic, uvicorn | Container `app` (compose mode 2) and as K8s Deployment |
| Haystack Ingestion Agent | Uses Haystack Ingestion pipelines to pull PDFs, exported Confluence HTML/PDF bundles, Markdown repos; snapshots raw docs with version metadata | Haystack Ingestion orchestrator, filesystem + S3-compatible blob store | CLI + optional K8s CronJob |
| Preprocessing & Chunking Agent | Runs inside Haystack Ingestion to normalize text (OCR fallback, HTML to Markdown), chunk, enrich metadata | Haystack `Document`, `PreProcessor`, Tika/PyMuPDF, trafilatura | Same ingestion container or dedicated `preprocess` pod |
| Embedding Agent | Haystack Ingestion embedding stage calling llama.cpp server hosting `bge-large-en-v1.5-gguf`, caches vectors | Haystack `SentenceTransformersDocumentEmbedder` via HTTP/S adapter to llama.cpp server, Redis cache | Container `embedding` (compose modes 1 & 2) and as statefulset |
| Vector DB Agent | Persists vectors + metadata, handles filters, snapshots for migration | Qdrant (Docker container or managed helm), REST + gRPC APIs | Container `qdrant` (both modes) |
| Retrieval Agent | Combines dense + sparse search, reranks, returns enriched docs | Haystack `Retriever` nodes, optional SPLADE/BM25, reranker `CrossEncoder` (local) | Lives inside FastAPI process |
| Generation Agent | Streams promp<br/>ts to `qwen2.5-4b-instruct-int4` on llama.cpp server; enforces guardrails | Haystack `PromptBuilder`, custom `LlamaCppInvocationLayer`, guardrails middleware | Container `llm` (compose modes 1 & 2) |
| Evaluation & Monitoring Agent | Synthetic queries, latency/quality tracking, alerts | Haystack `EvalDocuments`, `prometheus_client`, Grafana | Optional service `evaluator` |

## Detailed Agent Descriptions

### FastAPI Orchestrator Agent
- **Inputs:** HTTPS requests from clients (chat, doc QA, admin). Auth context (JWT/OIDC), query payload, chat history.
- **Outputs:** Streaming JSON responses, suggested sources, telemetry events.
- **Responsibilities:**
  - Compose Haystack graph nodes (retriever, generator, fallback) and expose them via `/query`, `/chat`, `/ingest` endpoints.
  - Manage session cache (Redis) and enforce rate limits.
  - Surface readiness probes that depend on Qdrant + llama servers.
- **Interfaces:** Talks to Retrieval Agent inside the same process, Vector DB Agent via REST, Embedding/Generation Agents through HTTP clients pointed at llama servers.

- **Inputs:** Source configuration (filesystem globs pointing to PDFs, exported Confluence HTML/PDF bundles, Markdown dirs, S3 buckets) specified via Haystack Ingestion YAML/CLI plus scheduling metadata.
- **Outputs:** Structured Haystack `Document` stream with manifests tracked in PostgreSQL/Redis for resumable ingestion.
- **Responsibilities:**
  - Configure and run Haystack Ingestion graph nodes (fetchers, transformers, writers) so loaders, preprocessing, chunking, and embedding execute in a unified declarative pipeline.
  - Monitor shared folders or object storage buckets that receive manually exported Confluence pages/PDFs and persist snapshots for deterministic re-ingestion without calling Confluence APIs.
  - Coordinate ingestion jobs via CLI or CronJob; emit status updates and failure hooks.
- **Interfaces:** Haystack Ingestion CLI/API, filesystem/S3 holding exported artifacts, Redis/Postgres for manifests, and downstream Embedding Agent stage inside the same pipeline.

### Preprocessing & Chunking Agent
- **Inputs:** Streaming documents from Haystack Ingestion graph.
- **Outputs:** Cleaned Haystack `Document` objects annotated with metadata and chunk payloads ready for embeddings.
- **Responsibilities:**
  - Embed preprocessing logic inside Haystack Ingestion transformers to apply MIME-specific loaders (PyMuPDF, Markdown reader, HTML2Text) and optional OCR (Tesseract) automatically.
  - Normalize metadata (source URI, author, timestamps, ACL tags) and chunk text with overlapping windows tuned to Qwen context (e.g., 512 tokens, 64 stride) using Haystack `DocumentSplitter` components.
  - Persist intermediate data for reproducibility and hand off to the Embedding Agent stage without leaving the ingestion pipeline.

### Embedding Agent
- **Inputs:** Preprocessed Haystack `Document` objects emitted by the ingestion pipeline.
- **Outputs:** Vector embeddings + document IDs stored into Qdrant.
- **Responsibilities:**
  - Run as a Haystack Ingestion embedding component that calls a custom HTTP client pointed at the llama.cpp server hosting `bge-large-en-v1.5-gguf`.
  - Provide retry/backoff and TTL-based caching for deterministic embeddings while remaining compliant with Haystack Ingestion abstractions.
  - Publish metrics (through Monitoring Agent) about throughput and llama.cpp resource utilization.
- **Interfaces:** Communicates with llama.cpp HTTP API for embeddings, writes to Vector DB Agent using Qdrant writer nodes, exposes gRPC/HTTP metrics.

### Vector DB Agent
- **Inputs:** Upserts from Embedding Agent, search queries from Retrieval Agent.
- **Outputs:** Dense vector hits with payload metadata.
- **Responsibilities:**
  - Configure optimized HNSW/multi-segment indexes for `bge-large` embeddings with payload-based filtering (ACL, doc type, freshness).
  - Manage snapshotting/restore for local dev vs. production.
  - Provide hybrid retrieval via Qdrant Sparse Vectors or bridging to BM25 service.

### Retrieval Agent
- **Inputs:** User queries, optional chat history, structured filters.
- **Outputs:** Ranked list of context passages for the Generation Agent.
- **Responsibilities:**
  - Compose dense retrieval (Qdrant) with optional BM25 fallback + reranking.
  - Enforce guardrails (max tokens) and deduplicate overlapping chunks.
  - Log feature attribution for evaluation.

### Generation Agent
- **Inputs:** Prompt template (instruction + retrieved context + chat state).
- **Outputs:** Streaming tokens/responses, cited sources, safety flags.
- **Responsibilities:**
  - Connect FastAPI to llama.cpp server running `qwen 3 4b int4`, handle streaming SSE/WebSocket.
  - Format prompts (system + user + context) and tune decoding (temperature, max tokens, stop sequences).
  - Run lightweight safety classifier to block obvious prompt injection or PII leakage.

### Evaluation & Monitoring Agent
- **Inputs:** Synthetic/curated questions, production telemetry, doc change events.
- **Outputs:** Quality dashboards, alerts, regression reports.
- **Responsibilities:**
  - Schedule nightly eval runs comparing new snapshots vs. baselines.
  - Emit Prometheus metrics (latency, retrieval hit rate, hallucination score) and plug into Grafana.
  - Feed feedback loop to adjust chunking, prompt templates, or thresholds.

## Local Ingestion Runbook (data/ folder)
- Ensure Qdrant + llama.cpp embedding server are up (`docker compose -f docker-compose.infra.yml up -d`).
- Place Markdown files under `data/` (e.g., `data/notes/*.md`).
- Run ingestion: `py -3.12 -m ingestion.cli ingest --path "data/**/*.md"`.
- Confirm output shows "wrote N chunks" and collection name; re-run with updated files as needed.

## Deployment Modes Alignment
- **Compose Mode 1 (Dependencies Only):** Bring up `qdrant`, llama.cpp embedding server, llama.cpp generation server, optional Redis/Postgres/Qdrant dashboard. Agents running inside FastAPI stay offline so developers can attach PyCharm debugger locally.
- **Compose Mode 2 (Full Stack):** Adds FastAPI Orchestrator + workers so the entire agent suite is runnable with a single command. Evaluation Agent remains optional but can be toggled via profile.

## Open Questions
1. Should Acquisition Agent push documents directly into message broker or rely on filesystem triggers?
2. Where to persist intermediate Markdown/HTML snapshots (local disk vs. object store)?
3. Do we run llama.cpp servers on the same node as FastAPI in production, or dedicate GPU nodes with in-cluster services?
4. What guardrails are required before exposing the Generation Agent externally (moderation, redaction)?
5. Which telemetry backend (Prometheus self-hosted vs. cloud) is preferred for Monitoring Agent?
