[← Index](index.md) · [← Phase 9](phase-9.md) · [Cross-cutting →](cross-cutting.md)

---

## Phase 10 — Integrations & Ecosystem
**Goal:** Meet customers where they already work. Extend the platform's reach via native integrations, a public developer API, and a full webhook/event framework enabling third-party automation.

### Deliverables
- First-class webhook system (per-tenant subscriptions, HMAC-signed payloads, delivery log with retries)
- Zapier app (public listing on Zapier marketplace)
- Native integrations: Zendesk, HubSpot, Slack
- Public developer API (portal API promoted to versioned, externally documented API)
- Developer hub: API reference, quickstart guides, SDK examples

### Webhook system

```
POST   /portal/webhooks              (create webhook subscription)
GET    /portal/webhooks              (list subscriptions)
DELETE /portal/webhooks/{id}         (delete subscription)
GET    /portal/webhooks/{id}/logs    (delivery log: status, response code, retry count)
POST   /portal/webhooks/{id}/test    (send test payload to endpoint)
```

| Event | Payload |
|-------|---------|
| `ingestion.complete` | `{document_id, filename, chunk_count, status}` |
| `ingestion.failed` | `{document_id, filename, error_message}` |
| `query.low_confidence` | `{session_id, query, max_score, threshold}` |
| `escalation.triggered` | `{session_id, trigger_reason, procedure_id}` |
| `plan.quota_warning` | `{resource, used, limit, percent}` |
| `chat.session_ended` | `{session_id, turns, resolution_state}` |

All events include: `HMAC-SHA256` signature in `X-SupportLayer-Signature` header, `event_id` for deduplication, ISO 8601 timestamp.

### Native integrations

**Zendesk:** Install via OAuth2 from portal. Unanswered / low-confidence queries automatically create Zendesk tickets with full session transcript. Resolved queries log as Zendesk knowledge suggestions.

**HubSpot:** Install via OAuth2 from portal. Chat sessions linked to HubSpot contact by email (if collected during session). Conversation summary pushed to HubSpot contact timeline.

**Slack:** Install via Slack OAuth from portal. Bot joins designated channel; answers questions using tenant knowledge base. `/ask <question>` slash command for internal team use. Low-confidence queries escalate to a human review thread.

### Frontend Design
> References: `docs/references/design/stitch_stark_fintech_prd/`
> `webhook_management_stark/` · `integrations_marketplace_stark_with_logos/`

**Webhooks page (portal sidebar "Integrations" entry):**
> Reference: `webhook_management_stark/`
- Portal light theme; "Integrations" active in sidebar
- Title: "Webhooks" + description; "+ Create Webhook" primary button top-right
- Subscriptions table: WEBHOOK URL · EVENTS (monospace event-name badge) · STATUS (LIVE=green pill, PAUSED=gray pill) · CREATED DATE · ACTIONS
  - Actions per row: EDIT (blue) · PAUSE / RESUME (amber) · DELETE (red) — as text links
- "Delivery Log" section with "LIVE UPDATES ●" badge top-right
- Delivery log table: TIMESTAMP · EVENT · STATUS CODE (200 OK=green badge, 500 Error=red badge) · RETRY COUNT · ACTION ("SEND TEST" button per row)
- "VIEW ALL DELIVERIES ↓" text link at bottom of log

**Integrations Marketplace page:**
> Reference: `integrations_marketplace_stark_with_logos/`
- Portal light theme; "Integrations" active in sidebar; "Search apps..." search input + bell icon in top bar
- Title: "Integrations Marketplace"
- Tabs: All Integrations · Installed · Pending · Collections
- Integration cards in a 3-column grid:
  - Each card: brand icon (colored square) · INSTALLED or AVAILABLE badge (top-right corner) · integration name (bold) · short description; CTA button: "Configure" (blue filled, for installed) or "Install" (outlined, for available)
  - Integrations: Zendesk (teal) · HubSpot (orange) · Slack (purple) · Mattermost (blue) · Telegram (light blue)
  - Final card: dashed border + "+" icon + "Request Integration" + "Can't find what you need? Let us know." — links to a request form

### Tasks
- [ ] Webhook engine: `webhooks` + `webhook_delivery_logs` tables; HMAC signing; retry with exponential backoff (3 attempts max); delivery log with status + response body
- [ ] Zapier app: `New Query`, `New Ingestion`, `New Escalation` triggers; `Ingest Document`, `Query Bot` actions — submit to Zapier app marketplace
- [ ] Zendesk integration: OAuth2 app, ticket creation on escalation, knowledge suggestion sync
- [ ] HubSpot integration: OAuth2, contact linking, timeline events
- [ ] Slack app: `slack_bots` table per tenant, slash command handler calls `rag-core` query, formatted response with sources, escalation thread routing
- [ ] Developer hub: OpenAPI docs site (Scalar or Redocly), quickstart guide (curl + Python + JS), authentication walkthrough
- [ ] Public API keys: separate `developer_api_keys` scope allowing external apps to call portal API on behalf of a tenant (OAuth2 client credentials flow)

### Tests (Phase 10)
- **Unit:** HMAC signature generation/verification, webhook retry backoff schedule
- **Integration:** Webhook delivery to test endpoint; retry on 5xx; delivery log entry written correctly
- **Integration:** Zendesk ticket created on escalation (mock Zendesk API)
- **Integration:** Slack bot responds in channel with formatted sources (mock Slack API)

#### E2E tests (Playwright .NET — portal)
- [ ] **Webhooks page renders:** Navigate to Integrations → Webhooks → verify title "Webhooks" + description + "+ Create Webhook" button → verify subscriptions table with columns WEBHOOK URL / EVENTS / STATUS / CREATED DATE / ACTIONS → verify Delivery Log section with "LIVE UPDATES ●" badge
- [ ] **Create webhook:** Click "+ Create Webhook" → fill in endpoint URL (test receiver) → select events: `ingestion.complete`, `escalation.triggered` → save → verify new row appears in subscriptions table with correct URL and event badges → verify STATUS shows LIVE (green pill)
- [ ] **Webhook event badges:** Verify EVENTS column shows monospace badge(s) for each subscribed event type per webhook row
- [ ] **Pause and resume webhook:** Click PAUSE on an active webhook row → verify STATUS changes to PAUSED (gray pill) → click RESUME → verify STATUS returns to LIVE (green pill) → verify state persists after page reload
- [ ] **Delete webhook:** Click DELETE on a webhook row → confirm dialog → verify row removed from table → verify webhook no longer listed after reload
- [ ] **Send test delivery:** Click "SEND TEST" action on a delivery log row (or on the webhook row) → verify new delivery log entry appears with current timestamp → verify STATUS CODE 200 OK (green badge) for a healthy test endpoint → verify delivery log updates without full page reload ("LIVE UPDATES" behavior)
- [ ] **Delivery log status codes:** Seed webhook deliveries with 200 and 500 responses → navigate to Delivery Log → verify 200 OK rows show green badge → verify 500 Error rows show red badge → verify RETRY COUNT column shows non-zero for failed deliveries
- [ ] **View all deliveries:** Click "VIEW ALL DELIVERIES ↓" link → verify full paginated delivery log page renders with all historical entries
- [ ] **Integrations Marketplace renders:** Navigate to Integrations Marketplace tab → verify title "Integrations Marketplace" → verify tabs: All Integrations · Installed · Pending → verify integration cards in grid: Zendesk (teal) · HubSpot (orange) · Slack (purple) present → verify "Request Integration" dashed card at end of grid
- [ ] **Install integration (OAuth flow):** Click "Install" on Zendesk card → verify OAuth2 redirect to Zendesk auth URL → complete OAuth in test/stub → verify redirect back to portal → verify Zendesk card now shows INSTALLED badge and "Configure" button → click Installed tab → verify Zendesk appears
- [ ] **Configure installed integration:** Click "Configure" on an installed integration → verify configuration panel or modal opens with integration-specific settings → save settings → verify saved without error
- [ ] **HMAC signature verification:** Create a webhook to a test endpoint that reads `X-SupportLayer-Signature` → trigger a real event (ingest a document) → capture the delivery → verify signature header present → verify HMAC-SHA256 computed correctly using the webhook secret
- [ ] **Tenant isolation (webhooks):** Tenant A creates a webhook → Tenant B logs in → navigate to Webhooks → verify Tenant B sees only their own webhooks → attempt to call `GET /portal/webhooks/{tenantA_id}` with Tenant B JWT → verify 403

### Quality Gate ✅
- Webhooks delivered with correct HMAC signatures; delivery log accurate; retries exhausted correctly
- Zapier app passes Zapier review criteria (test in developer account)
- Zendesk integration creates ticket with session transcript in integration test
- Developer hub publicly accessible with a working quickstart example

---
