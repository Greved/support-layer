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
- **Unit:** Quota enforcement logic, usage aggregation SQL
- **Integration:** Stripe webhook handler processes `invoice.paid` → tenant plan updated → limits refreshed
- **Integration:** Request at 100% query quota → 429 returned; plan upgrade → requests succeed again
- **E2E:** Tenant upgrades plan via portal → Stripe Checkout (test mode) → subscription active → quota raised

### Quality Gate ✅
- End-to-end payment flow works in Stripe test mode
- Quota enforcement blocks requests correctly on all limit types (documents, queries, storage)
- Trial auto-conversion fires correctly at expiry date
- Stripe webhook processed idempotently (replay same event twice → no duplicate state change)

---
