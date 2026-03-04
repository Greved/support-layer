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

### Frontend Design
> References: `docs/references/design/stitch_stark_fintech_prd/`
> `internal_ops_dashboard/` · `tenants_management_stark_refined/` · `audit_log_stark_admin_dark/` · `audit_log_stark_admin_light/`

**Design system — Admin SPA:**
- Dark theme throughout: near-black sidebar and top bar (`≈#0f1117`), white body text, orange brand accent (visually distinct from portal's blue)
- Branding: "MISSION CONTROL" header text; global `● SYSTEMS HEALTHY / DEGRADED` status badge top-right of top bar
- Left sidebar: icon + ALL-CAPS label navigation — DASHBOARD · TENANTS · API KEYS · USERS · AUDIT LOG · SETTINGS · SUPPORT
- Global search input in top bar; no page-level breadcrumb needed

**Internal Ops Dashboard:**
- 4 KPI cards in a row: Active Tenants · Global RPS · Error Rate (%) · Platform MRR — each with a numeric trend delta indicator
- Infrastructure panels: LLM Gateway (active request count + latency sparkline), Vector DB Cluster (per-collection storage progress bars), Job Queue (pending / failed / processed counts)
- Tenant activity table at bottom: avatar · tenant name · slug · plan badge · status dot · last activity timestamp

**Tenant Management page:**
- Filter row: search input + "Plan: All" dropdown + "Status: Active" dropdown
- 4 KPI summary cards: Total Tenants · Active Queries (30D) · Avg Quality Score · Suspended count
- Table columns: NAME/SLUG · PLAN · STATUS · QUERIES (30D) · QUALITY SCORE · ACTIONS
  - Plan badges: Enterprise = filled orange pill · Pro = outlined pill · Free = plain text
  - Status dot: Active = green · Trial = amber · Suspended = red
  - QUERIES column: inline mini bar-chart sparkline showing 30-day distribution
  - Quality score: `x.x / 10` numeric text
  - Actions: pencil/edit icon
- "+ Create Tenant" primary button top-right; pagination row at footer

**Audit Log page:**
- Filter row: date range picker + Tenant context dropdown + Event type dropdown
- Table columns: TIMESTAMP · ACTOR · TENANT · EVENT · DETAILS · ACTIONS
- EVENT column: monospace fixed-width codes, color-coded — neutral bold (e.g. CONFIG_UPDT), success green (AUTH_SUCCESS), security violation red (SEC_VIO_04)
- Row-level expand or modal for full diff/context JSON

**Tenant Detail page** (drill-down from Tenants list):
> Reference: `docs/references/design/stitch_stark_fintech_prd/admin_tenant_detail_stark/`
- Light theme (not dark) — tenant detail uses standard light layout despite admin being dark overall
- Breadcrumb: Tenants › Active Tenants › {Tenant Name}
- Title row: tenant name + ACTIVE/SUSPENDED status badge + Tenant ID; "Manage Status" secondary button + "Edit Tenant" primary button
- Horizontal tabs: Overview · Documents · Config · Billing · API Keys · Users (underline active indicator)
- Overview tab: 3 metric cards (Total Queries / Active API Keys with "N expiring soon" warning / Average Latency with "Within SLA" green badge); "Transaction Trends" bar chart with 7D/30D/90D toggle; "Recent System Events" table (EVENT / STATUS / USER / TIMESTAMP) with status chips SYSTEM/SUCCESS/PENDING
- Right panel (sticky): "TENANT PROFILE" card — legal entity name, primary contact (avatar + name + email), subscription tier (icon + plan name), infrastructure region; "USAGE QUOTAS" — labelled progress bars for API Requests/Month + User Seats + Storage Capacity with used/limit values; quick-action links (Generate Master API Key, View Full Audit Trail); "Suspend Tenant" danger button (red, bottom of panel)

**User Management page:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/global_user_management_stark_refined/`
- Left sidebar label "NAVIGATION"; items: Overview · User Management · Tenant Control · Risk & Compliance · Audit Logs · API Keys; logged-in admin name + role bottom-left
- Title: "USER MANAGEMENT" (all-caps) + description "Monitor and manage users across N active tenants globally."
- Actions: "+ Invite User" primary button + "↓ Export CSV" secondary button top-right
- Filter row: search input + quick-filter role chips (All Roles / Super Admin / Tenant Admin / Member / Suspended)
- Table columns: USER (avatar + name + email) · TENANT SLUG (monospace pill) · ROLE (colored badge: Super Admin=blue, Tenant Admin=teal, Member=gray) · LAST LOGIN · STATUS (Active=green pill, Suspended=red pill) · ACTIONS (⋮ menu)
- Pagination: showing N-N of total; numbered page buttons
- Bottom summary cards: New Users (30D) · Daily Active · Suspended count

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
