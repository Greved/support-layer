from prometheus_client import Counter, Histogram

RAG_REQUESTS = Counter(
    "rag_requests_total",
    "Total RAG query requests",
    ["tenant_id", "status"],
)

RAG_LATENCY = Histogram(
    "rag_request_duration_seconds",
    "RAG query end-to-end duration",
    ["tenant_id"],
    buckets=[0.5, 1, 2, 4, 8, 16, 30],
)

RAG_TOKENS = Counter(
    "rag_tokens_total",
    "Total tokens consumed by RAG queries",
    ["tenant_id"],
)

RAG_LLM_ERRORS = Counter(
    "rag_llm_errors_total",
    "Total LLM errors during RAG queries",
    ["tenant_id", "provider"],
)

INGEST_REQUESTS = Counter(
    "rag_ingest_requests_total",
    "Total ingestion requests",
    ["tenant_id", "status"],
)

INGEST_DURATION = Histogram(
    "rag_ingest_duration_seconds",
    "Ingestion pipeline duration",
    ["tenant_id"],
)
