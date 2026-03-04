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

### Frontend Design
> Reference: `docs/references/design/stitch_stark_fintech_prd/chat_widget_embed/`

**Widget appearance:**
- Floating launcher: filled circle button (color from `data-color`) anchored to the corner set by `data-position` (`bottom-right` default · `bottom-left` · `inline`); toggles to an X icon when the window is open
- Chat window: ≈360 px wide popup anchored above the launcher; white background; rounded corners with shadow
- Header row: small bot avatar (circle, ≈32 px) + bot name (`data-title`) + green "ONLINE" status dot; minimize/close button top-right
- Message bubbles: user messages — dark/black background, white text, right-aligned; bot messages — white/light-gray card, left-aligned with bot avatar beside the first bubble in a group; 12–16 px border-radius
- Source Context card: rendered below each bot response; bordered card showing source document name + page/line reference in monospace; collapsible
- Streaming: bot bubble renders tokens incrementally with a blinking cursor animation until `[DONE]`
- Input bar: full-width textarea + Send icon button; Enter to submit, Shift+Enter for newline
- Theme: CSS custom properties driven by `data-color`; no external font dependency; shadow DOM or sandboxed iframe to prevent host-page style bleed

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

#### Unit tests
- Rate limit counter logic: sliding window adds request, removes expired entries, count over limit triggers 429
- API key hash lookup: SHA-256 match returns tenant context; no match returns 401
- SSE frame formatting: `data:` prefix, `\n\n` terminator, JSON structure of token/sources/done events

#### Integration tests (.NET TestServer + Testcontainers Postgres + Testcontainers Redis)
- **Valid API key:** `POST /v1/chat` with `X-Api-Key: sl_live_*` → 200 + `{answer, sessionId, sources[]}`
- **Invalid API key:** unknown key hash → 401 with error body; no key header → 401
- **Expired API key:** key with past `ExpiresAt` → 401
- **Rate limiting:** exceed `MaxRequestsPerMinute` for tenant plan → 429 + `Retry-After` header; wait 60s window → requests succeed again
- **Session persistence:** `POST /v1/session` → 201 + session ID; `POST /v1/chat` with `sessionId` → message stored; `GET /v1/session/{id}` → returns full message history
- **Session tenant isolation:** `GET /v1/session/{id}` with API key from different tenant → 403
- **Streaming endpoint:** `POST /v1/chat/stream` → `Content-Type: text/event-stream`; response body contains `data: {"type":"token",...}` lines; last line is `data: [DONE]`; after stream completes `ChatMessage` row exists in DB
- **Billing event written:** after successful chat query → `BillingEvent` row with `EventType="query"` exists for tenant
- **CORS:** request from allowed origin → `Access-Control-Allow-Origin` header present; `OPTIONS` preflight → 204
- **Python stream endpoint:** `POST /internal/stream` without secret → 403; with correct secret and mock query → `Content-Type: text/event-stream` and token events in body
- **E2E (Playwright .NET):**
  - Load test HTML page with embedded widget script → click launcher button → verify chat window opens with header showing bot name and ONLINE status
  - Type question in input → press Enter → verify streaming tokens appear progressively in bot bubble → verify cursor animation → verify `[DONE]` ends stream and cursor disappears
  - Send question about ingested document → verify Source Context card appears below answer with filename, page/line, and excerpt
  - Send two messages → reload page → open widget again → verify session history is restored (both messages visible)
  - Send messages until rate limit is hit → verify error message rendered in chat bubble (not a silent failure)
  - Embed widget with `data-position="bottom-left"` → verify launcher renders in bottom-left corner
  - Embed widget with `data-auto-open-delay-seconds="2"` → wait 3 seconds → verify widget opens automatically without user interaction
  - Test on 375×667 mobile viewport → verify launcher is fully visible → open widget → verify chat window fits screen without overflow → type and send message
  - Embed widget with invalid API key → send message → verify user-facing error message appears in the chat (no raw stack trace exposed)
- **Load test (k6):** 50 concurrent chat sessions, p95 latency < 5s

### Quality Gate ✅
- Widget embeds on external page and completes a full chat exchange
- Streaming works end-to-end with no buffering glitch
- Rate limiting enforced and returns correct headers
- Load test: p95 < 5s at 50 concurrent users
- Billing event written for every successful query

---
