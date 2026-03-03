[← Index](index.md) · [← Phase 0](phase-0.md) · [Phase 2 →](phase-2.md)

---

## Phase 1 — Customer Portal API
**Goal:** A client can self-service: manage documents, configure their bot, invite team members.

### Deliverables
- `api-portal` (.NET): full CRUD for tenant's own resources
- Document upload endpoint → enqueues Hangfire job
- Supported file types: PDF, DOCX, TXT, Markdown, HTML, CSV
- File size and document count enforced against `plan_limits` on every upload
- Bot configuration API
- API key management (create / revoke keys for widget)
- Test-chat endpoint (proxies to `rag-core` with tenant context)
- Email notification (background job) on ingestion completion or failure
- Document versioning: re-upload of same filename purges old chunks before re-ingesting
- OpenAPI spec published at `/portal/openapi.json`

### Endpoints (api-portal)
```
Auth
  POST /portal/auth/login
  POST /portal/auth/refresh
  POST /portal/auth/logout

Documents
  GET    /portal/documents
  POST   /portal/documents          (upload file → queues job)
  DELETE /portal/documents/{id}
  GET    /portal/documents/{id}/status

Configuration
  GET    /portal/config
  PUT    /portal/config

API Keys
  GET    /portal/api-keys
  POST   /portal/api-keys
  DELETE /portal/api-keys/{id}

Team
  GET    /portal/users
  POST   /portal/users/invite
  DELETE /portal/users/{id}

Chat (test)
  POST   /portal/chat              (single-turn, authenticated)
```

### Tasks
- [ ] Implement JWT login/refresh with ASP.NET Identity or custom (bcrypt passwords)
- [ ] `DocumentsController`: upload to temp storage → enqueue Hangfire job
- [ ] `ConfigController`: GET/PUT `tenant_configs` row
- [ ] `ApiKeysController`: generate HMAC key, store hash, return plaintext once
- [ ] Hangfire `worker` service: job handlers call `rag-core` `POST /internal/ingest` → poll for completion → update `documents` table status
- [ ] Portal test-chat endpoint: forward to `rag-core` internal `/internal/query`
- [ ] File storage: local volume for dev, S3-compatible (MinIO) for prod
- [ ] File type validation on upload (allowlist: pdf, docx, txt, md, html, csv); reject others with 415
- [ ] File size check: reject uploads exceeding `plan_limits.max_file_size_mb` for tenant's plan
- [ ] Document count check: reject upload if tenant is at `plan_limits.max_documents` limit
- [ ] Document versioning: on upload, if a `documents` row with the same `filename` exists, purge its Qdrant chunks before re-ingesting
- [ ] Email notification job: on `status` transition to `ready` or `error`, send email to tenant admin

### Tests (Phase 1)
- **Unit:** Document status state machine, API key hash comparison
- **Integration (httpx / .NET TestServer):**
  - Upload → job queued → status transitions `pending → processing → ready`
  - Config update reflected in subsequent query responses
  - API key created → used in widget auth → revoked → rejected
  - Tenant A cannot access Tenant B documents (403)
- **E2E (Playwright .NET against running stack):** Full ingest-then-query flow via portal API

### Quality Gate ✅
- A new tenant can register, upload a PDF, wait for ingestion, then successfully query it via test-chat
- Tenant isolation enforced on every endpoint (tested with two tenants)
- Document status correctly transitions through all states including error case
- OpenAPI spec validated against implementation

---
