[← Index](index.md) · [← Overview](overview.md) · [Phase 1 →](phase-1.md)

---

## Phase 0 — Multi-tenancy Foundation
**Goal:** Everything the platform needs to exist before building any feature.

### Deliverables
- PostgreSQL schema + EF Core migrations
- Tenant isolation in Qdrant (collection-per-tenant, naming: `tenant_{slug}`)
- Python RAG service refactored as internal service: all endpoints require `X-Tenant-ID` header
- .NET skeleton project structure (solution with three API projects + shared library)
- JWT auth middleware in all .NET services
- Docker Compose updated with PostgreSQL + Redis

### PostgreSQL schema (core tables)
```sql
tenants        (id, slug, name, plan, status, created_at, deleted_at)
users          (id, tenant_id nullable, email, role, password_hash, created_at)
               -- role: super_admin | tenant_admin | tenant_member
api_keys       (id, tenant_id, key_hash, label, scopes, last_used_at, expires_at)
tenant_configs (id, tenant_id, llm_provider, llm_model, system_prompt,
                widget_title, widget_color, max_tokens, temperature, updated_at)
documents      (id, tenant_id, filename, status, chunk_count, size_bytes,
                qdrant_collection, ingested_at, error_message)
billing_events (id, tenant_id, event_type, tokens_in, tokens_out, query_count, ts)
chat_sessions  (id, tenant_id, external_session_id, created_at)
chat_messages  (id, session_id, role, content, sources_json, latency_ms, created_at)
plan_limits    (plan_name, max_documents, max_queries_per_month, max_file_size_mb,
                max_team_members, max_storage_gb)
audit_logs     (id, tenant_id nullable, user_id, action, resource_type, resource_id,
                diff_json, ip_address, user_agent, created_at)
```

### Tasks
- [ ] Add PostgreSQL + Redis to `docker-compose.infra.yml`
- [ ] Write EF Core migrations for schema above (`dotnet ef migrations add`)
- [ ] Verify migrations apply cleanly and roll back cleanly (`dotnet ef database update` / `database drop`)
- [ ] Refactor Python `Settings` to require `tenant_id` on all ingestion/query calls
- [ ] Update `QdrantService` to resolve collection from `tenant_{id}`
- [ ] Scaffold .NET solution: `Platform.sln` with `Api.Public`, `Api.Portal`, `Api.Admin`, `Core` (shared models/EF)
- [ ] JWT middleware in .NET: validate token, populate `TenantContext`
- [ ] Internal service auth between .NET → Python (`X-Internal-Secret` header or mTLS)
- [ ] Seed `plan_limits` rows for each plan tier (hobby, starter, pro, business, enterprise)
- [ ] Add PgBouncer or configure EF Core connection pool limits for production load
- [ ] Python `rag-core`: implement `/internal/health` readiness endpoint (checks Qdrant reachability + embedding server ping)

### Tests (Phase 0)

#### Unit tests
- Qdrant collection name generation: `tenant_{slug}` formatting, special character rejection
- Tenant slug validation: length limits, allowed characters, uniqueness check

#### Integration tests (Testcontainers — real Postgres + real Qdrant)
- **Tenant isolation:** Tenant A ingests document → Tenant B queries same topic → result set does not include Tenant A chunks; verified by asserting Qdrant payload `tenant_id` filter is applied
- **Missing header:** `POST /api/query` without `X-Tenant-ID` → 422 (FastAPI validation error)
- **Wrong tenant header:** `X-Tenant-ID` header set to unknown slug → Qdrant returns empty results, response answer indicates no information found (does not 500)
- **Internal secret guard:** `POST /internal/healthz` without `X-Internal-Secret` → 403; with wrong secret → 403; with correct secret → 200
- **Collection creation:** Ingest first document for a new tenant slug → verify Qdrant collection `tenant_{slug}` is created automatically
- **Multi-tenant collection isolation:** Tenant A and Tenant B both ingest docs → `GET /collections` shows two separate collections with correct names
- **Migration rollback:** `dotnet ef database update <previous>` completes without error and re-applying brings schema back to current state

### Quality Gate ✅
- Tenant isolation test passes: Tenant A cannot retrieve Tenant B documents
- PostgreSQL schema deployed and migrations run cleanly
- .NET solution builds and passes health check
- All existing RAG functionality preserved (regression)

---
