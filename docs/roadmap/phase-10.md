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
- **E2E:** Zapier Zap triggers on `ingestion.complete` event → action runs successfully in test mode

### Quality Gate ✅
- Webhooks delivered with correct HMAC signatures; delivery log accurate; retries exhausted correctly
- Zapier app passes Zapier review criteria (test in developer account)
- Zendesk integration creates ticket with session transcript in integration test
- Developer hub publicly accessible with a working quickstart example

---
