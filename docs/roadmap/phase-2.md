[← Index](index.md) · [← Phase 1](phase-1.md) · [Phase 3 →](phase-3.md)

---

## Phase 2 — Public Chat API + Embeddable Widget
**Goal:** Clients embed a JS widget on their site; visitors chat with the bot.

### Deliverables
- `api-public` (.NET): public chat API authenticated by API key
- SSE streaming endpoint for real-time responses
- CORS, rate limiting per API key
- Session management (optional visitor session continuity)
- Chat widget: React component bundled as UMD script tag
- Widget configurability via `data-*` attributes or JS config object

### Endpoints (api-public)
```
POST   /v1/chat                  (API key auth, returns full response)
POST   /v1/chat/stream           (API key auth, SSE streaming)
POST   /v1/session               (create anonymous session)
GET    /v1/session/{id}          (retrieve session history)
```

### Widget embed example
```html
<script src="https://platform.example.com/widget.js"
        data-api-key="pk_live_..."
        data-title="Support Bot"
        data-color="#2563eb">
</script>
```

### Tasks
- [ ] `api-public`: API key auth middleware (lookup hash in DB, attach tenant context)
- [ ] Rate limiting: Redis sliding window per API key (configurable per tenant plan)
- [ ] CORS: allow origins configured per tenant (stored in `tenant_configs`)
- [ ] SSE streaming: proxy stream from `rag-core` → client
- [ ] Session storage: `chat_sessions` + `chat_messages` in PostgreSQL
- [ ] Widget: React component using **AI SDK** (elements.ai-sdk.dev) chat primitives, SSE streaming, configurable theme, minimal bundle (<50KB gzipped)
- [ ] Widget build pipeline: Vite, UMD output, CDN-ready
- [ ] Record `billing_events` per query (token counts from `rag-core` response)
- [ ] Widget i18n: support `data-locale` attribute + per-label string overrides (`data-strings-placeholder`, `data-strings-send`, etc.)
- [ ] Widget position/trigger config: `data-position="bottom-right|bottom-left|inline"` attribute
- [ ] Configurable fallback message: `tenant_configs.no_answer_message` shown when retrieval confidence is below threshold (avoids hallucination on low-relevance context)
- [ ] Proactive greeting: `data-auto-open-delay-seconds` attribute to auto-open widget after N seconds on page

### Tests (Phase 2)
- **Unit:** Rate limit counter logic, API key validation, SSE frame formatting
- **Integration:**
  - Valid API key → 200 with answer + sources
  - Invalid/expired API key → 401
  - Exceeded rate limit → 429 with `Retry-After`
  - Streaming: all SSE frames received, `[DONE]` received, session persisted
  - CORS: origin not in allowlist returns 403
- **E2E (Playwright .NET):**
  - Widget loads on test HTML page, user types question, streaming response appears
  - Widget works on mobile viewport
- **Load test (k6):** 50 concurrent chat sessions, p95 latency < 5s

### Quality Gate ✅
- Widget embeds on external page and completes a full chat exchange
- Streaming works end-to-end with no buffering glitch
- Rate limiting enforced and returns correct headers
- Load test: p95 < 5s at 50 concurrent users
- Billing event written for every successful query

---
