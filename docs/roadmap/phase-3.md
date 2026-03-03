[← Index](index.md) · [← Phase 2](phase-2.md) · [Phase 4 →](phase-4.md)

---

## Phase 3 — Internal Admin API + Dashboard
**Goal:** You can see all clients, their usage, documents, config, billing, and infrastructure health.

### Deliverables
- `api-admin` (.NET): super-admin API (separate auth, not shared with portal)
- Internal admin React SPA
- Per-tenant statistics aggregation
- Infrastructure health aggregation (Qdrant, LLM servers, worker queue depth)

### Endpoints (api-admin)
```
Tenants
  GET    /admin/tenants               (list + search + pagination)
  GET    /admin/tenants/{id}
  POST   /admin/tenants               (create tenant)
  PATCH  /admin/tenants/{id}          (update plan, status)
  DELETE /admin/tenants/{id}          (soft-delete + purge Qdrant collection)

Statistics
  GET    /admin/tenants/{id}/stats    (queries/day, tokens, documents, latency p50/p95)
  GET    /admin/stats/global          (platform-wide aggregates)

Documents
  GET    /admin/tenants/{id}/documents
  DELETE /admin/tenants/{id}/documents/{docId}

Billing
  GET    /admin/tenants/{id}/billing  (events, usage totals, cost estimate)

Infrastructure
  GET    /admin/infra/health          (Qdrant, LLM, embed, worker queue, Redis, PG)
  GET    /admin/infra/collections     (Qdrant collection sizes per tenant)

Audit
  GET    /admin/audit-logs            (filter by tenant_id, action, date range, paginated)

Tenants (additions)
  POST   /admin/tenants/{id}/impersonate  (issue short-lived portal JWT as tenant admin; always audited)
  GET    /admin/tenants/{id}/export       (stream ZIP: document metadata, configs, chat history, eval sets)
```

### Admin SPA features
- Tenant table with search/filter (plan, status, last active)
- Tenant detail: tabs for Overview / Documents / Config / Billing / API Keys / Users
- Global stats dashboard: charts (queries/day, active tenants, token usage, error rate)
- Infrastructure health panel: live status of all services
- Log viewer: recent errors per tenant (Loki query via Grafana)

### Tasks
- [ ] `api-admin` with separate super-admin JWT issuer (not shared with portal)
- [ ] Stats aggregation: query `billing_events` and `chat_messages` with window functions
- [ ] Tenant hard-delete: purge Qdrant collection + delete PG rows + revoke keys
- [ ] Infrastructure health endpoint: HTTP-check each internal service, return status map
- [ ] Admin SPA: React + TypeScript + Zustand + shadcn/ui + Tailwind + Axios + Recharts
- [ ] Tenant detail drill-down with all tabs
- [ ] Audit log middleware (.NET): intercept every mutation request, write to `audit_logs` with diff, actor, and IP
- [ ] Impersonation endpoint: generates short-lived JWT (15 min), writes mandatory `audit_logs` entry; separate `impersonate` scope that cannot be silently granted
- [ ] Data export: stream ZIP of all tenant-scoped PG rows + document metadata; exclude raw Qdrant vectors

### Tests (Phase 3)
- **Unit:** Stats aggregation SQL queries, cost estimate calculation
- **Integration:**
  - Super-admin token cannot access `/portal/*` and vice versa (role isolation)
  - Tenant deletion cascades correctly (PG rows + Qdrant collection gone)
  - Infrastructure health returns degraded when a service is down
- **E2E (Playwright .NET):**
  - Log in as super-admin, create tenant, view stats, delete tenant
  - Infrastructure panel shows correct status when a service is intentionally stopped

### Quality Gate ✅
- All tenant data visible in admin with correct stats
- Role isolation: portal token rejected on admin routes and vice versa
- Tenant delete fully cleans up all resources (verified by integration test)
- Admin SPA loads and navigates without JS errors

---
