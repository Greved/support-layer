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

#### Unit tests
- Document status state machine: valid transitions `pending→processing→ready`, `pending→processing→error`; invalid direct `pending→ready` rejected
- API key hash comparison: SHA-256 of plaintext matches stored hash; timing-safe comparison used
- File size validation: boundary conditions at plan limit (exactly at limit passes, one byte over rejects)
- Ingestion job: mock RAG client returns success → document status set to `ready`; mock returns error → status set to `error` with error message stored

#### Integration tests (.NET TestServer + Testcontainers Postgres)
- **Auth flow:** `POST /portal/auth/login` with valid credentials → 200 + `accessToken` + `refreshToken`; use access token on protected endpoint → 200; expired/invalid token → 401; `POST /portal/auth/refresh` with valid refresh token → new token pair; revoked refresh token → 401
- **Document upload lifecycle:** `POST /portal/documents` with PDF → 202 + document ID; `GET /portal/documents/{id}` → status `processing`; after Hangfire job runs → status `ready` with non-zero `chunkCount`
- **Document upload guards:** unsupported MIME type → 415; file exceeding `MaxFileSizeMb` for tenant plan → 413; monthly document quota reached → 429 with `X-Quota-Exceeded: documents`
- **Config CRUD:** `GET /portal/config` returns current settings; `PUT /portal/config` with new system prompt → 200; subsequent `GET` returns updated value
- **API key lifecycle:** `POST /portal/api-keys` → 201 + plaintext key (one-time); `GET /portal/api-keys` → lists key with masked value; `DELETE /portal/api-keys/{id}` → 204; use deleted key → 401
- **User invite:** `POST /portal/users/invite` → 201; invited user appears in `GET /portal/users`; `DELETE /portal/users/{id}` → 204; deleted user no longer in list
- **Tenant isolation:** Tenant A JWT on `GET /portal/documents` returns only Tenant A's documents; Tenant B's document ID not accessible via Tenant A's token → 404
- **RAG query:** `POST /portal/chat` with question → 200 with non-empty `answer` and `sources` (mocked RAG client returns canned response)
- **E2E (Playwright .NET against running stack):**
  - Login with valid credentials → JWT stored → access protected endpoint → logout → re-request protected endpoint → 401
  - Upload PDF → poll `GET /portal/documents/{id}/status` until `ready` → `POST /portal/chat` with a question answered by the doc → verify answer is non-empty and contains relevant content
  - Upload unsupported file type (.exe) → verify 415 response with error body
  - Upload file exceeding plan size limit → verify 413 response
  - Upload document → delete it → verify `GET /portal/documents` no longer lists it and Qdrant collection no longer returns its chunks
  - Create API key → use key to call `POST /v1/chat` → verify 200 → revoke key → repeat call → verify 401
  - Tenant A uploads doc → Tenant B attempts `GET /portal/documents` with Tenant A token → verify 403

### Quality Gate ✅
- A new tenant can register, upload a PDF, wait for ingestion, then successfully query it via test-chat
- Tenant isolation enforced on every endpoint (tested with two tenants)
- Document status correctly transitions through all states including error case
- OpenAPI spec validated against implementation

---
