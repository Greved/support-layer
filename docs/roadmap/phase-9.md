[← Index](index.md) · [← Phase 8](phase-8.md) · [Phase 10 →](phase-10.md)

---

## Phase 9 — Billing & Monetization
**Goal:** Self-sustaining revenue infrastructure. Tenants subscribe, upgrade, and pay without manual intervention; usage is metered and enforced automatically.

### Deliverables
- Stripe integration (subscriptions + usage-based metering)
- Plan tier enforcement at the API layer (reject requests over quota)
- Tenant-facing billing portal (invoices, payment method, upgrade/downgrade)
- Usage alerts and quota warnings
- Trial period support with automatic conversion

### Plan tiers (example)

| Tier | Price | Documents | Queries/month | Team members | Max file size |
|------|-------|-----------|---------------|--------------|---------------|
| Hobby | $19/mo ($190/yr) | 25 | 500 | 1 | 5 MB |
| Starter | $29/mo ($290/yr) | 100 | 2,000 | 3 | 10 MB |
| Pro | $89/mo ($890/yr) | 500 | 10,000 | 10 | 50 MB |
| Business | $249/mo ($2,490/yr) | 2,000/workspace | 50,000 pooled | unlimited | 100 MB |
| Enterprise | from $599/mo (annual) | unlimited | unlimited | unlimited | custom |

### Endpoints (api-portal additions)
```
Billing
  GET    /portal/billing/subscription      (current plan, status, next renewal date)
  GET    /portal/billing/invoices          (list past invoices)
  GET    /portal/billing/invoices/{id}/pdf (download invoice PDF)
  POST   /portal/billing/portal            (redirect to Stripe Customer Portal for self-service)
  POST   /portal/billing/checkout          (create Stripe Checkout session for plan upgrade)

Usage
  GET    /portal/billing/usage             (queries this period vs quota, documents vs limit, storage used)
```

### Frontend Design
> Reference: `docs/references/design/stitch_stark_fintech_prd/usage_billing_stark_blue_theme/`

**Usage & Billing page (portal sidebar "Usage" and "Billing" entries):**
- Current plan card: plan name + billing period + "X% Used" circular or linear progress bar for primary quota; "Upgrade Plan" CTA button
- 3 metric cards in a row: Total Queries this period · Tokens Consumed · Avg Latency — each with a +/- trend badge vs previous period
- DAILY QUERY PERFORMANCE stacked bar chart: automated queries = blue bars, escalated queries = orange bars; 30-day x-axis; y-axis = query count
- Recent query log table below the chart: timestamp · query preview · latency · resolution status
- Usage progress bars for secondary limits (documents, storage, team members) shown in the plan card or a sub-section
- Warning state: progress bars turn amber at 80% and red at 100%; banner alert shown at 100%

**Plan Upgrade flow:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/plan_upgrade_flow_stark/`
- Full-page layout (replaces portal content area or modal); dark/navy left nav sidebar with Portal / Upgrade Plan nav items
- Title: "UPGRADE YOUR PLAN" + subtitle; plan cards in a row of 3
- Each plan card: name · price/month · feature list with checkmark rows
- Middle card (RECOMMENDED) visually highlighted: larger scale, blue border, "RECOMMENDED" label badge; "UPGRADE NOW" blue filled button
- Other cards: "CURRENT PLAN" (if applicable, outlined) · "CONTACT SALES" for Enterprise
- "FULL PLAN COMPARISON" expandable table below cards: FEATURE rows vs plan columns (checkmark/cross/text per cell)
- Two FAQ accordion items at bottom: plan change policy + overage behavior

**Invoices List page:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/invoices_list_stark/`
- Title: "Invoices" + description + "↓ Export CSV" + "▼ Filter List" buttons
- Tabs: All Invoices · Paid · Pending · Overdue · Drafts
- Table columns: checkbox · DATE · INVOICE ID · CUSTOMER (colored initials avatar + name) · AMOUNT · STATUS · ACTIONS
- Status badges: + PAID (green) · + PENDING (amber) · + OVERDUE (red)
- Actions column: "↓ Download PDF" link per row
- Pagination: "SHOWING N TO N OF N ENTRIES" + numbered pages
- "+ New Invoice" button bottom-left (for manual invoice creation)

### Tasks
- [ ] Integrate Stripe SDK: products, prices, subscriptions, webhooks (`invoice.paid`, `customer.subscription.updated`, `customer.subscription.deleted`)
- [ ] Map Stripe plan → `plan_limits` row; enforce on every upload and query
- [ ] Trial period: new tenants get 14-day trial at Pro limits; auto-downgrade to Starter on expiry if no payment method added
- [ ] Usage metering: Stripe Billing meters for query count (report daily from `billing_events` aggregates)
- [ ] Portal billing page: current plan card, usage progress bars (with 80% warning state), invoices table, upgrade/downgrade flow
- [ ] Quota enforcement middleware: return `HTTP 429` with `X-Quota-Exceeded: documents|queries|storage` header when limit reached
- [ ] Usage alert emails: send at 80% and 100% of monthly quota via background job
- [ ] Admin billing view: per-tenant MRR, plan tier, Stripe subscription status, outstanding invoices

### Tests (Phase 9)

#### Unit tests
- Quota enforcement: `CheckQuotaAsync(tenantId, "queries")` with usage=100/limit=100 → throws `QuotaExceededException`; usage=99/limit=100 → passes
- Usage aggregation: SQL query summing `BillingEvent` rows for last 30 days returns correct total across timezone boundaries
- Stripe webhook signature verification: valid `Stripe-Signature` header → event parsed; tampered payload → `SignatureVerificationException`

#### Integration tests (.NET TestServer + Testcontainers Postgres + Stripe test mode / mock)
- **Stripe webhook — invoice.paid:** POST `/portal/billing/webhook` with `invoice.paid` payload (Stripe test event) → tenant plan updated to paid tier; `plan_limits` refreshed; `billing_events` row inserted
- **Stripe webhook — subscription.deleted:** `customer.subscription.deleted` payload → tenant plan downgraded to free; usage limits updated
- **Stripe webhook — idempotency:** replay same `invoice.paid` event twice with same `event_id` → plan updated only once (second call is no-op)
- **Quota enforcement — queries:** seed tenant at 100% query quota; `POST /v1/chat` → 429 + `X-Quota-Exceeded: queries` header; no `BillingEvent` created for blocked request
- **Quota enforcement — documents:** seed tenant at max document count; `POST /portal/documents` → 429 + `X-Quota-Exceeded: documents`
- **Quota enforcement — storage:** seed tenant with storage at limit; upload file that would exceed → 429 + `X-Quota-Exceeded: storage`
- **Plan upgrade restores quota:** tenant at 100% query quota; `POST /portal/billing/checkout` with plan upgrade → after webhook `invoice.paid` processed → quota raised; `POST /v1/chat` succeeds
- **Usage API:** `GET /portal/billing/usage` → 200 with `{queriesUsed, queriesLimit, documentsUsed, documentsLimit, storageUsedBytes, storageLimitBytes}`; counts match seeded `BillingEvent` and `Document` rows
- **Invoice list API:** `GET /portal/billing/invoices` → returns paged list from Stripe (mocked); each row has `id`, `amount`, `status`, `pdfUrl`
- **Trial expiry:** seed tenant with `trial_ends_at = now() - 1 day`; nightly job runs → plan downgraded; `GET /portal/billing/subscription` shows `status=trial_expired`
- **Usage alert at 80%:** seed tenant at 80% query quota; background job runs → `usage_alerts` row created with `resource=queries, percent=80`; alert email enqueued
- **Admin billing view:** `GET /admin/tenants/{id}/billing` → returns MRR, Stripe subscription status, and `recentEvents` matching `billing_events` rows

#### E2E tests (Playwright .NET — portal)
- [ ] **Usage page renders:** Log in as tenant → navigate to Usage & Billing in sidebar → verify current plan card shows plan name + billing period + usage percentage → verify 3 metric cards render (Total Queries / Tokens Consumed / Avg Latency) each with trend badge → verify DAILY QUERY PERFORMANCE chart renders with blue and orange bars over 30-day axis
- [ ] **Usage progress bars:** Verify secondary quota progress bars render for documents, storage, and team members → verify each shows used/limit values → navigate away and back → verify values persist (not zero)
- [ ] **80% quota warning:** Seed a tenant at 85% query quota usage → navigate to Usage page → verify the relevant progress bar renders in amber color → verify a warning banner or indicator is visible → verify no 100% error banner shown
- [ ] **100% quota enforcement:** Seed a tenant at 100% query quota → attempt to call `POST /v1/chat` via the widget → verify `429` response → verify `X-Quota-Exceeded: queries` header present → navigate to portal Usage page → verify red 100% indicator and banner alert visible
- [ ] **Invoice list:** Navigate to Invoices section → verify tabs render: All Invoices · Paid · Pending · Overdue → verify table columns: DATE · INVOICE ID · CUSTOMER · AMOUNT · STATUS · ACTIONS → verify status badges: PAID=green, PENDING=amber, OVERDUE=red → click "Paid" tab → verify only paid invoices shown
- [ ] **Invoice PDF download:** Click "↓ Download PDF" action on a paid invoice row → verify file download triggered (PDF MIME type) → verify file is non-empty
- [ ] **Export CSV:** Click "↓ Export CSV" button on Invoices page → verify CSV file downloads → verify CSV contains expected columns (DATE, INVOICE ID, AMOUNT, STATUS)
- [ ] **Plan upgrade flow:** Click "Upgrade Plan" button on current plan card → verify Plan Upgrade page renders with 3 plan cards → verify RECOMMENDED card visually distinct (blue border, larger scale) → verify current plan card shows "CURRENT PLAN" label → click "UPGRADE NOW" on a higher plan → verify redirect to Stripe Checkout (test mode) → complete Stripe test payment → verify redirect back to portal → verify plan card now shows upgraded plan name → verify quota limits reflect new plan
- [ ] **Plan comparison table:** On Plan Upgrade page → click "FULL PLAN COMPARISON" expandable section → verify feature rows expand with checkmarks/crosses per plan column → verify FAQ accordion items expand with correct policy text
- [ ] **Stripe portal redirect:** Click "Manage Billing" or equivalent → verify `POST /portal/billing/portal` called → verify redirect to Stripe Customer Portal URL (test mode) → verify portal page loads with subscription and payment method info
- [ ] **Trial expiry state:** Log in as a tenant on trial with 0 days remaining → navigate to Usage page → verify trial expiry notice rendered → verify plan card shows trial expired state → verify "Upgrade Plan" CTA prominent

### Quality Gate ✅
- End-to-end payment flow works in Stripe test mode
- Quota enforcement blocks requests correctly on all limit types (documents, queries, storage)
- Trial auto-conversion fires correctly at expiry date
- Stripe webhook processed idempotently (replay same event twice → no duplicate state change)

---
