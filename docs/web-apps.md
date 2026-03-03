# Web Applications — UI Design Reference

> **Purpose:** Comprehensive screen-by-screen description of every web application in the platform.
> Intended as source material for AI UI generation tools (Google Stitch, v0, Lovable, etc.).
> Every screen includes layout, components, data, interactions, and backend connection points.

---

## Design System

### Visual language
Clean B2B SaaS aesthetic. The customer portal is light, professional, and approachable.
The admin dashboard is denser, data-heavy, slightly dark-accented.
The chat widget is minimal and unobtrusive — it lives on someone else's website.

### Color palette

| Token | Value | Usage |
|-------|-------|-------|
| `--brand-primary` | `#2563EB` (blue-600) | Primary buttons, links, active states |
| `--brand-light` | `#EFF6FF` (blue-50) | Backgrounds, badges |
| `--success` | `#16A34A` (green-600) | Status: ready, pass, live |
| `--warning` | `#D97706` (amber-600) | Status: processing, warning |
| `--danger` | `#DC2626` (red-600) | Status: error, fail, destructive actions |
| `--neutral-50` | `#F9FAFB` | Page backgrounds |
| `--neutral-100` | `#F3F4F6` | Card backgrounds, table stripes |
| `--neutral-600` | `#4B5563` | Secondary text |
| `--neutral-900` | `#111827` | Primary text |

### Typography
- Font: Inter (sans-serif)
- Page titles: `text-2xl font-semibold`
- Section headings: `text-lg font-medium`
- Body: `text-sm text-neutral-700`
- Monospace (API keys, code): `font-mono text-xs`

### Layout conventions
- **Portal / Admin:** Fixed left sidebar (240px) + top header (56px) + scrollable main content area
- **Sidebar:** Logo top, nav items with icons, user menu bottom
- **Content max-width:** 1280px, centered
- **Cards:** `rounded-xl border border-neutral-200 shadow-sm bg-white p-6`
- **Tables:** full-width, `border-b` row separators, sticky header
- **Empty states:** centered illustration + heading + CTA button
- **Loading states:** skeleton loaders matching content shape

### Component library: shadcn/ui
All form controls, dialogs, dropdowns, tooltips, badges, tabs, and toasts from shadcn/ui.
Charts from Recharts. State via Zustand. HTTP via Axios.

---

## App 1 — Customer Portal (`portal-web`)

**Audience:** Tenant admins and team members (the paying clients).
**URL base:** `https://portal.platform.example.com`
**Auth:** JWT cookie, login via `/portal/auth/login`
**Tech:** React + TypeScript + Vite + Zustand + Axios + shadcn/ui + Tailwind + Recharts + AI SDK

### Navigation sidebar

```
┌─────────────────────────┐
│  ◈ BotPlatform          │  ← brand logo
├─────────────────────────┤
│  ⌂  Dashboard           │
│  ☁  Documents           │
│  ⚙  Configuration       │
│  💬 Test Chat           │
│  ✦  Procedures          │
│  📊 Quality             │
│  🔑 API Keys            │
│  👥 Team                │
│  📈 Usage               │
├─────────────────────────┤
│  [Avatar] Acme Corp ▾   │  ← tenant name + user menu
└─────────────────────────┘
```

Role visibility:
- `tenant_admin`: all pages
- `tenant_member`: Dashboard, Test Chat, Documents (read-only)

---

### P1 — Dashboard

**Purpose:** At-a-glance health of the bot this month.

**Layout:** 4 stat cards top row, 2 charts below, recent activity list bottom.

**Stat cards (row of 4):**
| Card | Value | Subtext |
|------|-------|---------|
| Total Queries | `1,284` | `↑ 12% vs last month` |
| Avg Quality Score | `0.84` | `Faithfulness · Answer Relevancy` |
| Documents Indexed | `47` | `3 processing` |
| Escalation Rate | `8%` | `↓ 2% vs last month` |

**Charts (row of 2, Recharts):**
- Left: `AreaChart` — Queries per day (last 30 days), x-axis dates, y-axis count
- Right: `LineChart` — Quality score trend (Faithfulness + Answer Relevancy lines), last 10 eval runs

**Recent activity list:**
Chronological feed of events: document ingested, eval run completed, procedure triggered, API key created.
Each item: icon + description + relative timestamp (e.g., "2 hours ago").

**Empty state (no data yet):** Illustration of a robot with documents.
Heading: "Your bot isn't set up yet". Two CTAs: "Upload documents" → `/documents`, "Configure bot" → `/config`.

**Backend connections:**
```
GET /portal/stats/summary
  → { total_queries, avg_quality, document_count, escalation_rate,
      queries_per_day: [{date, count}],
      quality_trend: [{run_date, faithfulness, answer_relevancy}],
      recent_activity: [{type, description, timestamp}] }
```

---

### P2 — Documents

**Purpose:** Upload, track, and manage knowledge base documents.

**Layout:** Top toolbar + full-width table.

**Top toolbar:**
- Left: page title "Documents" + count badge (`47 total, 3 processing`)
- Right: `[+ Upload documents]` button (primary) — opens drag-and-drop modal

**Upload modal:**
- Drag-and-drop zone: dashed border, cloud-upload icon, "Drag PDFs, Markdown, HTML or TXT here" subtitle
- File list below zone: each file shows name + size + remove × button
- On upload: progress bar per file, then modal closes and row appears in table
- Supported formats badge: `PDF · MD · HTML · TXT`

**Documents table:**
| Column | Content |
|--------|---------|
| Name | File icon (by type) + filename |
| Status | Badge: `Ready` (green) / `Processing` (amber, spinner) / `Error` (red) / `Queued` (gray) |
| Chunks | Number of indexed chunks |
| Size | Human-readable file size |
| Uploaded | Relative date |
| Actions | `•••` menu: Re-ingest / Delete |

Status polling: while any row has `Processing` or `Queued` status, poll `GET /portal/documents/{id}/status` every 3s.

**Empty state:** "No documents yet. Upload your first document to start answering questions."

**Delete confirmation dialog:**
"Delete 'filename.pdf'? This will remove all indexed content from the knowledge base. This cannot be undone."
Buttons: `Cancel` / `Delete` (destructive red).

**Error row expansion:** Click on `Error` badge → inline error message expands below the row.

**Backend connections:**
```
GET    /portal/documents
         → [{ id, filename, status, chunk_count, size_bytes, ingested_at, error_message }]
POST   /portal/documents          multipart/form-data
         → { id, filename, status: "queued" }
GET    /portal/documents/{id}/status
         → { id, status, chunk_count, error_message }
DELETE /portal/documents/{id}
         → 204
```

---

### P3 — Configuration

**Purpose:** Tune bot behavior and customize the chat widget appearance.

**Layout:** Two-column — left 60% form, right 40% live widget preview.

**Left column — form sections (accordion or flat):**

**Section 1: AI Behavior**
- System prompt: `<Textarea>` 4 rows, placeholder "You are a helpful support assistant for Acme Corp. Only answer questions about our products."
- LLM Provider: `<Select>` — `Local (fast)` / `Gemini Pro` / `GPT-4o`
- Model: `<Select>` — populated based on provider
- Temperature: `<Slider>` 0.0–1.0, step 0.1, current value shown (`0.3`)
- Max response tokens: `<Input number>` default `512`
- Language: `<Select>` — `Auto-detect` / `English` / `Spanish` / `French` / etc.

**Section 2: Retrieval**
- Top-K results: `<Slider>` 1–20, default 5 — "Number of document chunks to retrieve"
- Min relevance score: `<Slider>` 0.0–1.0, default 0.6 — "Minimum similarity threshold"

**Section 3: Widget Appearance**
- Widget title: `<Input>` default "Support Chat"
- Welcome message: `<Textarea>` 2 rows
- Brand color: `<ColorPicker>` (hex input + color swatch)
- Bot avatar: `<ImageUpload>` (50×50 circle, shows current or initials fallback)
- Position: `<RadioGroup>` — Bottom right / Bottom left
- Show sources: `<Switch>` — toggle show/hide source citations in responses

**[Save changes]** button (primary, sticky bottom of form, disabled until dirty).
Unsaved changes indicator: amber dot on tab title.

**Right column — live widget preview:**
Rendered preview of the widget window (non-interactive) reflecting current form values in real time.
Shows: header with title + color + avatar, sample conversation bubble, input bar.
Label above: "Preview — updates as you type".

**Backend connections:**
```
GET /portal/config
      → { llm_provider, llm_model, system_prompt, temperature, max_tokens,
          top_k, min_relevance_score, language,
          widget_title, welcome_message, widget_color, widget_position,
          show_sources, bot_avatar_url }
PUT /portal/config   body: same shape
      → 200 updated config
```

---

### P4 — Test Chat

**Purpose:** Live test of the bot using the current configuration, authenticated as a team member.

**Layout:** Full-height chat interface, similar to the widget but full-page and labeled as test mode.

**Header bar:**
- "Test Chat" title + amber "Test mode" badge
- Right: `[Reset conversation]` button + current config summary chip ("Gemini · bge-large")

**Chat area (AI SDK `useChat` hook):**
- Message bubbles: user messages right-aligned (brand color bg), bot messages left-aligned (white card)
- Bot messages include:
  - Answer text with streaming cursor (typewriter effect while streaming)
  - Sources accordion below the answer: collapsed "3 sources", expand to show source cards
  - Source card: filename + chunk excerpt + relevance score badge
  - 👍 / 👎 feedback buttons (right of each bot message)
  - Latency chip: `340ms` (shown after response completes)
- Typing indicator: three animated dots while waiting

**Input area (bottom):**
- `<Textarea>` auto-growing, placeholder "Ask a question..."
- Send button (→ icon) or Enter to submit (Shift+Enter for newline)
- Character count if > 200 chars

**Quality sidebar (collapsible, right side):**
- Eval metrics for this response (if available): Faithfulness, Relevancy shown as mini gauges
- Retrieved chunks list: raw chunks that were used as context

**Empty state (no messages):** Suggested questions as clickable chips, e.g., "How do I reset my password?", "What are your pricing plans?", "How do I contact support?"

**Backend connections:**
```
POST /portal/chat
      body:  { query, session_id? }
      → { answer, sources: [{file, page, relevance_score, brief_content}],
          latency_ms, faithfulness?, answer_relevancy? }

POST /portal/chat/stream     (SSE)
      body:  { query, session_id? }
      → SSE: data: {"delta": "..."} ... data: [DONE]
             final: data: {"sources": [...], "latency_ms": 340}

POST /portal/chat/feedback
      body: { message_id, rating: "up"|"down", comment? }
      → 204
```

---

### P5 — Procedures

**Purpose:** Define structured multi-step workflows the bot follows for specific scenarios.

#### P5a — Procedure List

**Layout:** Top toolbar + card grid.

**Top toolbar:**
- Title "Procedures" + count
- Right: `[+ New procedure]` (primary) / `[Create from template ▾]` (secondary, dropdown)

**Template dropdown options:**
- Handle refund request
- Troubleshoot connectivity issue
- Collect bug report
- Verify customer identity
- Escalate to human agent

**Procedure cards (grid, 2 columns):**
```
┌──────────────────────────────────┐
│ 🔄 Process Refund Request  [Live]│  ← status badge
│                                  │
│ "Activate when customer asks     │  ← trigger preview
│  about refund or return"         │
│                                  │
│  ↑ 47 triggers   ✓ 89% resolved │  ← 7-day stats
│  ✗ 5% escalated  ∅ 6% abandoned │
│                                  │
│ [Edit] [Simulate] [•••]          │
└──────────────────────────────────┘
```
Status badge colors: `Live` green, `Draft` gray, `Paused` amber.
`[•••]` menu: Duplicate / Pause / Delete.

**Empty state:** "No procedures yet. Procedures let your bot handle complex multi-step workflows." CTA: `[Create your first procedure]`.

**Backend connections:**
```
GET  /portal/procedures
      → [{ id, name, status, trigger_instructions, trigger_count_7d,
            resolution_rate, escalation_rate, abandon_rate, updated_at }]
POST /portal/procedures          { name, description?, from_template? }
      → { id, ... }
DELETE /portal/procedures/{id}
PATCH  /portal/procedures/{id}   { status: "live"|"paused"|"draft" }
```

#### P5b — Procedure Editor

**Layout:** Full-page editor. Left panel: step list (300px). Center: step editor (flex grow). Right panel: attributes + tools sidebar (280px, collapsible).

```
┌────────────────────────────────────────────────────────────────────┐
│ ← Procedures   [Process Refund Request]     [Draft]  [Set Live ▸] │  ← top bar
├──────────────┬──────────────────────────────────┬──────────────────┤
│ STEPS        │ Step 2: Get Order Details        │ ATTRIBUTES       │
│ ──────────── │ ─────────────────────────────    │ ──────────────── │
│ 1 Greet      │ Type  [Instruction        ▾]     │ order_id  text   │
│ ▶ 2 Get order│                                  │ email     text   │
│ 3 Look up    │ Content                          │ [+ Add attribute]│
│ 4 ◆ Eligible?│ ┌──────────────────────────┐    │                  │
│   ├ yes→ 5   │ │Ask the customer for their│    │ TOOLS            │
│   └ no → 7   │ │order number. If they     │    │ ──────────────── │
│ 5 Issue      │ │can't find it, ask for    │    │ Order Lookup API │
│ 6 Confirm    │ │their email address.      │    │ Refund API       │
│ 7 Deny       │ │                          │    │ [+ Connect tool] │
│              │ │Store as: {order_id}      │    │                  │
│ [+ Add step] │ └──────────────────────────┘    │                  │
│              │                                  │                  │
│              │ [← Prev step]   [Next step →]   │                  │
└──────────────┴──────────────────────────────────┴──────────────────┘
```

**Step types and their editor UI:**

| Type | Icon | Editor fields |
|------|------|---------------|
| `instruction` | 💬 | Content textarea, "Store response as" attribute select |
| `condition` | ◆ | Condition description textarea OR Python code toggle; True branch target, False branch target |
| `tool_call` | 🔌 | Tool select, input mapping (attribute → tool param), output mapping (response field → attribute) |
| `sub_procedure` | ↩ | Procedure select |
| `checkpoint` | ⏸ | Message to agent textarea |
| `escalate` | 🧑 | Reason template textarea |
| `end` | ⬛ | Optional closing message |

**Step list interactions:**
- Drag to reorder (drag handle on left)
- Click to select and edit in center panel
- Condition steps show tree branches as nested indented items
- Active step highlighted with left border in brand color

**Top bar:**
- Back link "← Procedures"
- Editable procedure name (click to edit inline)
- Status badge + `[Set Live]` / `[Pause]` button
- `[Save draft]` button (autosave every 30s with indicator "Saved 10s ago")

**Backend connections:**
```
GET    /portal/procedures/{id}
         → { id, name, status, trigger_instructions, trigger_examples,
             trigger_threshold, steps: [...], attributes: [...] }
PUT    /portal/procedures/{id}      full update
PATCH  /portal/procedures/{id}/steps    bulk step reorder/update
POST   /portal/procedures/{id}/steps
DELETE /portal/procedures/{id}/steps/{stepId}
GET    /portal/tools                → [{ id, name, tool_type, description }]
POST   /portal/tools                { name, tool_type, base_url, auth_type, auth_config, ... }
```

#### P5c — Procedure Simulations

**Layout:** Two-panel. Left: simulation list. Right: conversation replay + judgment.

**Left panel — simulation list:**
- Each item: simulation name + last result (`✓ Pass` / `✗ Fail` / `—`) + last run date
- `[+ Add simulation]` button at bottom

**Add simulation dialog:**
- Name: `<Input>`
- Scenario: `<Textarea>` — "Describe the customer's situation: A customer who placed order #1234 three days ago wants a full refund because the product arrived damaged."
- Success criteria: `<Textarea>` — "Bot should collect order ID, call the order lookup tool, determine eligibility, and either issue refund or explain denial. Should not escalate unless lookup fails."
- Mock tool responses: expandable section — per tool: `<Textarea>` with JSON mock response

**Right panel — simulation result:**
```
┌───────────────────────────────────────────────────────┐
│ Scenario: Damaged product refund                       │
│ Last run: 2 minutes ago    [▶ Rerun]                  │
│                                                        │
│ RESULT: ✓ PASS   Score: 0.91                          │
│                                                        │
│ Conversation replay:                                   │
│ ┌──────────────────────────────────────────────────┐  │
│ │ 🧑 Customer: Hi, I received a damaged product... │  │
│ │ 🤖 Bot: I'm sorry to hear that! Could you please │  │
│ │         provide your order number?               │  │
│ │ 🧑 Customer: It's #1234                          │  │
│ │ 🤖 Bot: [calling Order Lookup API...]            │  │ ← tool call shown
│ │         I found your order. Since it arrived     │  │
│ │         damaged, you're eligible for a full      │  │
│ │         refund. Shall I process it now?          │  │
│ │ 🧑 Customer: Yes please                          │  │
│ │ 🤖 Bot: [calling Refund API...]                  │  │
│ │         Done! Refund of $49.99 issued.           │  │
│ └──────────────────────────────────────────────────┘  │
│                                                        │
│ Judge reasoning:                                       │
│ "Procedure collected order ID (✓), called lookup (✓), │
│  assessed eligibility (✓), issued refund (✓),         │
│  did not unnecessarily escalate (✓)."                 │
└───────────────────────────────────────────────────────┘
```

**Failed simulation:** Result panel shows red `✗ FAIL`, failure points list (e.g., "Bot did not collect order ID before calling tool"), suggested fix.

**`[Set Live]` guard:** If any simulation is failing, the "Set Live" button shows a warning tooltip: "1 simulation failing. Fix before going live." It still allows override with confirmation dialog.

**Backend connections:**
```
GET  /portal/procedures/{id}/simulations
POST /portal/procedures/{id}/simulations     { name, scenario, success_criteria, mock_tool_responses }
POST /portal/procedures/{id}/simulations/{simId}/run   → { run_id }
GET  /portal/procedures/{id}/simulations/{simId}/runs/{runId}
      → { passed, score, conversation_json, judgment_json }
```

---

### P6 — Quality

**Purpose:** Show how well the bot answers questions based on automated evaluation metrics.

**Layout:** Score summary cards top + trend chart + low-scoring questions table.

**Score cards (row of 5):**
Each card: metric name + current score (large, color-coded) + trend arrow vs previous run.

| Metric | Good | Warning | Poor |
|--------|------|---------|------|
| Faithfulness | ≥ 0.80 | 0.65–0.79 | < 0.65 |
| Answer Relevancy | ≥ 0.80 | 0.65–0.79 | < 0.65 |
| Context Precision | ≥ 0.75 | 0.60–0.74 | < 0.60 |
| Context Recall | ≥ 0.75 | 0.60–0.74 | < 0.60 |
| Hallucination Rate | ≤ 0.05 | 0.05–0.15 | > 0.15 |

**Trend chart:** Multi-line `LineChart` (Recharts), one line per metric, x-axis = eval run date, tooltips showing all metric values on hover.

**`[Run evaluation now]` button** (secondary) — triggers a manual eval run, shows progress toast.

**Low-scoring questions table:**
Rows sorted by lowest Faithfulness. Columns: Question preview / Faithfulness / Answer Relevancy / Bot Answer preview / `[View detail →]`.
"View detail" opens slide-over drawer: full question, full answer, retrieved chunks, per-metric scores, judge reasoning.

**Improvement suggestions panel** (below table):
"Your bot struggles with questions about X topic — consider uploading more documentation about it."
Generated from low-recall questions.

**Backend connections:**
```
GET /portal/evals/summary
      → { latest_run: {...scores}, previous_run: {...scores},
          trend: [{run_date, faithfulness, answer_relevancy, context_precision, context_recall, hallucination_rate}] }
GET /portal/evals/runs
      → [{ id, run_date, status, scores_summary }]
GET /portal/evals/runs/{id}
      → { id, run_date, items: [{question, answer, scores, retrieved_chunks, judgment}] }
POST /portal/evals/run
      → { run_id }   (202 Accepted)
```

---

### P7 — API Keys

**Purpose:** Create and manage API keys for embedding the chat widget.

**Layout:** Toolbar + table.

**Table columns:** Label / Key preview (`pk_live_...abcd`) / Scopes / Last used / Expires / Actions.

**Create key dialog:**
- Label: `<Input>` e.g., "Production website"
- Scopes: `<CheckboxGroup>` — `chat:send` (required), `session:read` (optional)
- Expiry: `<Select>` — Never / 30 days / 90 days / 1 year

**Post-creation dialog:** Shows full key once with copy button and warning "This key will not be shown again."

**Revoke confirmation:** "Revoke 'Production website'? Any widget using this key will stop working immediately."

**Widget snippet** (shown after key creation):
```html
<script src="https://platform.example.com/widget.js"
        data-api-key="pk_live_..."
        data-title="Support Chat"
        data-color="#2563EB">
</script>
```
Copy button above snippet.

**Backend connections:**
```
GET    /portal/api-keys    → [{ id, label, key_preview, scopes, last_used_at, expires_at }]
POST   /portal/api-keys    { label, scopes, expires_in_days }
         → { id, label, key_plaintext }   ← plaintext returned once
DELETE /portal/api-keys/{id}
```

---

### P8 — Team

**Purpose:** Invite colleagues, manage roles.

**Layout:** Toolbar + members table + pending invites table.

**Members table:** Avatar + Name + Email / Role badge / Joined date / `[Remove]` (admin only).

**Invite dialog:**
- Email: `<Input>`
- Role: `<RadioGroup>` — `Admin` (can change config, manage team) / `Member` (read-only, test chat)

**Role badges:** `Admin` blue filled, `Member` gray outline.

**Pending invites section:** List of unaccepted invites with email + sent date + `[Resend]` / `[Cancel]`.

**Backend connections:**
```
GET    /portal/users           → [{ id, email, name, role, joined_at }]
POST   /portal/users/invite    { email, role }
DELETE /portal/users/{id}
GET    /portal/users/invites   → [{ id, email, role, invited_at }]
DELETE /portal/users/invites/{id}
```

---

### P9 — Usage

**Purpose:** Understand consumption and plan limits.

**Layout:** Plan summary banner + stat cards + usage bar charts + query log table.

**Plan banner:**
`Growth Plan · 2,000 / 10,000 queries this month · 47 / 100 documents · Resets Mar 1`
[Upgrade plan] link.

**Stat cards:** Queries this month / Tokens consumed / Avg latency / Uptime.

**Usage bar charts (Recharts `BarChart`):**
- Queries per day (stacked: answered / escalated / failed)
- Token usage per day (tokens_in + tokens_out)

**Query log table** (paginated, last 100):
Columns: Time / Query preview (truncated 60 chars) / Status (Answered/Escalated/Failed) / Latency / Feedback.
Click row → slide-over drawer with full query, full answer, sources.

**Backend connections:**
```
GET /portal/usage/summary
      → { plan, queries_used, queries_limit, documents_used, documents_limit,
          reset_date, tokens_used, avg_latency_ms, uptime_percent }
GET /portal/usage/daily        → [{ date, queries_answered, queries_escalated, queries_failed, tokens }]
GET /portal/usage/queries      → paginated [{ id, query_preview, status, latency_ms, feedback, created_at }]
GET /portal/usage/queries/{id} → full query detail
```

---

## App 2 — Internal Admin (`admin-web`)

**Audience:** Platform operators (you and your team). Not exposed to clients.
**URL base:** `https://admin.platform.example.com`
**Auth:** Separate super-admin JWT, distinct issuer from portal
**Visual style:** Slightly darker. Sidebar: `#111827` (neutral-900) bg with white text. Content area: `#F9FAFB`. More data density than portal.

### Navigation sidebar

```
┌─────────────────────────┐
│  ◈ Platform Admin       │  ← logo, white on dark
├─────────────────────────┤
│  ⌂  Global Dashboard    │
│  🏢 Tenants             │
│  📊 Global Quality      │
│  🔧 Infrastructure      │
│  📋 Audit Log           │
├─────────────────────────┤
│  [Admin Avatar] You ▾   │
└─────────────────────────┘
```

---

### A1 — Global Dashboard

**Layout:** KPI row + 2 charts + alerts panel.

**KPI cards (row of 5):**
- Active Tenants
- Total Queries Today
- Platform Error Rate (last 1h)
- Avg Response Latency p95 (last 1h)
- Ingestion Jobs Queued

**Charts:**
- Left: `AreaChart` — Queries/hour across all tenants, last 24h
- Right: `BarChart` — Top 10 tenants by query volume this month

**Alerts panel** (right column or bottom):
List of firing Grafana/Prometheus alerts: severity badge (Critical/Warning) + alert name + since.
Empty state: "✓ All systems nominal".

**Backend connections:**
```
GET /admin/stats/global
      → { active_tenants, queries_today, error_rate_1h, p95_latency_ms,
          ingestion_jobs_queued, queries_per_hour: [...], top_tenants: [...] }
GET /admin/infra/alerts   → [{ severity, name, since, description }]
```

---

### A2 — Tenants

**Purpose:** Search, manage, and drill into all client tenants.

**Layout:** Toolbar + filter bar + table.

**Filter bar:**
- Search: `<Input>` (name or slug)
- Plan: `<Select>` All / Starter / Growth / Enterprise
- Status: `<Select>` All / Active / Suspended / Trial

**Tenants table:**
| Column | Content |
|--------|---------|
| Name | Tenant name + slug chip |
| Plan | Badge |
| Status | Badge: Active (green) / Trial (blue) / Suspended (red) |
| Queries (30d) | Number with sparkline |
| Quality Score | Gauge or colored number |
| Documents | Count |
| Last Active | Relative time |
| Actions | `[View]` / `[•••]` (Suspend / Delete) |

**Create tenant dialog:**
- Company name, slug (auto-derived, editable), plan, admin email (invite sent automatically).

**Suspend confirmation:** "Suspend Acme Corp? Their widget will return error 403 immediately."

**Delete confirmation (destructive, two-step):**
"This will permanently delete all documents, vectors, configurations, and billing history for Acme Corp. Type the tenant name to confirm."
`<Input>` must match tenant name before delete button enables.

**Backend connections:**
```
GET    /admin/tenants        ?search=&plan=&status=&page=&limit=
         → { items: [...], total, page }
POST   /admin/tenants        { name, slug, plan, admin_email }
PATCH  /admin/tenants/{id}   { status, plan }
DELETE /admin/tenants/{id}
```

---

### A3 — Tenant Detail

**Purpose:** Complete view of one tenant. Tabbed interface.

**Header (above tabs):**
```
← Tenants   [Acme Corp logo] Acme Corp   acme-corp
            Growth Plan · Active · Created Jan 2025
            [Suspend] [Delete]
```

**Tabs: Overview · Documents · Configuration · Billing · Users · Quality · Procedures**

#### Tab: Overview
- Stat cards: queries this month, avg quality, escalation rate, document count
- Queries/day chart (30 days)
- Quality trend (10 eval runs)
- Recent activity timeline

#### Tab: Documents
Same as portal documents table but read-only (no upload). Admin can trigger re-ingest or delete.
Extra column: "Ingested by" (user name).

```
GET /admin/tenants/{id}/documents
DELETE /admin/tenants/{id}/documents/{docId}
POST /admin/tenants/{id}/documents/{docId}/reingest
```

#### Tab: Configuration
Read-only view of `tenant_configs` row. All fields displayed as labeled values (not editable).
`[Edit config]` button opens same form as portal P3 but acting as admin override.

#### Tab: Billing
**Summary bar:** Queries this month / Tokens in / Tokens out / Estimated cost.

**Usage breakdown chart:** Stacked bar, daily tokens_in and tokens_out.

**Events table** (paginated):
Timestamp / Event type / Queries / Tokens in / Tokens out.

```
GET /admin/tenants/{id}/billing
      → { summary: {...}, daily: [...], events: [...] }
```

#### Tab: Users
Table: Email / Name / Role / Joined / Last login / `[Remove]`.
`[Invite user]` button (admin adding users to tenant on their behalf).

#### Tab: Quality
Full eval dashboard identical to portal P6 but reading tenant data.
Extra control: `[Generate synthetic dataset]` button + `[Run evaluation]`.

#### Tab: Procedures
List of this tenant's procedures: name, status, 7-day stats.
Read-only (admin cannot edit client procedures). Can pause a procedure if it's causing issues.

---

### A4 — Global Quality Heatmap

**Purpose:** Spot quality regressions across all tenants at once.

**Layout:** Full-width grid table. Rows = tenants, columns = metrics. Color-coded cells.

```
Tenant           │ Faithful │ Relevancy │ Ctx Prec │ Ctx Recall │ Hallucin │
─────────────────┼──────────┼───────────┼──────────┼────────────┼──────────┤
Acme Corp        │  0.87 🟢 │  0.91 🟢  │  0.74 🟡 │  0.71 🟡   │  0.03 🟢 │
TechStartup Ltd  │  0.61 🔴 │  0.78 🟡  │  0.55 🔴 │  0.62 🟡   │  0.12 🟡 │
Global Services  │  0.85 🟢 │  0.88 🟢  │  0.80 🟢 │  0.79 🟢   │  0.02 🟢 │
```

Click a cell → tenant detail quality tab.
Filter: date range picker (which eval run).

```
GET /admin/evals/global?run_date=...
      → [{ tenant_id, tenant_name, faithfulness, answer_relevancy,
            context_precision, context_recall, hallucination_rate, run_date }]
```

---

### A5 — Infrastructure Health

**Layout:** Service status grid + queue metrics + resource usage.

**Service status grid:**
Each service as a card: name + status indicator + details.

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ Qdrant       │  │ LLM Server   │  │ Embed Server │
│ 🟢 Healthy   │  │ 🟢 Healthy   │  │ 🟡 Slow      │
│ 23ms ping    │  │ 1.2s avg     │  │ 4.8s avg     │
│ 12.4 GB used │  │ Qwen 2.5 3B  │  │ bge-large    │
└──────────────┘  └──────────────┘  └──────────────┘

┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ PostgreSQL   │  │ Redis        │  │ Hangfire     │
│ 🟢 Healthy   │  │ 🟢 Healthy   │  │ 🟢 Healthy   │
│ 8.2 GB       │  │ 245 MB       │  │ 3 queued     │
└──────────────┘  └──────────────┘  └──────────────┘
```

Status colors: `🟢 Healthy` green / `🟡 Degraded` amber / `🔴 Down` red.

**Qdrant collections table:**
Tenant name / Collection name / Vectors count / Disk size / Last updated.

**Hangfire job queue panel:**
Tab bar: Enqueued / Processing / Succeeded / Failed.
Each tab: table of recent jobs with job type, tenant, enqueued time, duration.
Failed tab: `[Retry]` button per job.

**Backend connections:**
```
GET /admin/infra/health
      → { services: [{ name, status, ping_ms, details }] }
GET /admin/infra/collections
      → [{ tenant_id, tenant_name, collection_name, vectors_count, disk_mb, updated_at }]
GET /admin/infra/jobs?status=failed&limit=50
      → [{ id, type, tenant_id, enqueued_at, duration_ms, error }]
POST /admin/infra/jobs/{id}/retry
```

---

### A6 — Audit Log

**Layout:** Filter bar + chronological table.

**Filters:** Date range / Tenant / Actor / Event type.

**Table:** Timestamp / Actor (email) / Tenant / Event / Details (expandable).
Event types: `tenant.created`, `document.deleted`, `config.updated`, `user.invited`, `procedure.set_live`, etc.

---

## App 3 — Chat Widget (embeddable)

**Audience:** End users visiting a client's website.
**Embed:** Single `<script>` tag with `data-api-key`. No iframe — injects a Shadow DOM to isolate styles.
**Tech:** React + TypeScript + AI SDK (elements.ai-sdk.dev) + Tailwind (scoped) — bundled UMD < 50KB gzip.
**Auth:** API key (public, rate-limited). Anonymous sessions; optional visitor session ID persistence via `localStorage`.

### Widget states

#### State 0 — Bubble (minimized)
Round floating button, bottom-right (or bottom-left, configurable).
- Size: 56×56px
- Background: tenant brand color (from config)
- Icon: speech bubble ✦ or custom bot avatar
- Unread badge: red circle with count (if bot sends proactive message)
- Hover: subtle scale-up (1.05) + shadow elevation
- Click: expands to State 1

#### State 1 — Chat window (open, empty)
```
┌──────────────────────────────────┐
│ [Bot avatar] Support Chat    [×] │  ← header: brand color bg, white text
├──────────────────────────────────┤
│                                  │
│     👋 Hi! How can I help you   │  ← welcome message bubble
│        today?                   │  ← centered, brand color bg
│                                  │
│  ╔════════════════════════════╗  │
│  ║ How do I reset my password ║  │  ← suggested questions as chips
│  ╚════════════════════════════╝  │
│  ╔═══════════════════╗           │
│  ║ What are your hours ║         │
│  ╚═══════════════════╝           │
│                                  │
├──────────────────────────────────┤
│ [Type a message...      ] [  →]  │  ← input bar
└──────────────────────────────────┘
```

Suggested question chips: clickable, auto-submit on click. Disappear after first message.

#### State 2 — Conversation (active)

**Message bubbles:**
- User: right-aligned, brand color background, white text, rounded (full right, partial left)
- Bot: left-aligned, white background, neutral border, rounded (full left, partial right), small bot avatar left

**Streaming state (while bot is typing):**
- Typing indicator: three animated dots in a bot bubble
- Then: text appears word-by-word (AI SDK streaming tokens)
- Input disabled during stream

**Completed bot message:**
```
┌─────────────────────────────────────────┐
│ 🤖  To reset your password, go to       │
│     Settings → Security → Change Pass-  │
│     word. You'll need your current      │
│     password or can use "Forgot".       │
│                                         │
│     ▼ 2 sources                         │  ← collapsed accordion
│                                         │
│     👍  👎                              │  ← feedback (subtle, right-aligned)
└─────────────────────────────────────────┘
```

**Sources accordion (expanded):**
```
│     ▲ 2 sources                         │
│  ┌────────────────────────────────────┐ │
│  │ 📄 password-reset-guide.md  0.94  │ │  ← relevance score badge
│  │ "Navigate to Settings → Security   │ │
│  │  and click Change Password..."     │ │
│  └────────────────────────────────────┘ │
│  ┌────────────────────────────────────┐ │
│  │ 📄 faq.md  0.87                   │ │
│  │ "If you've forgotten your current  │ │
│  │  password, use the Forgot link..." │ │
│  └────────────────────────────────────┘ │
```

#### State 3 — Escalation offered
When bot confidence is low or customer asks for human:
```
┌─────────────────────────────────────────┐
│ 🤖  I wasn't able to fully answer       │
│     this. Would you like to speak       │
│     with a support agent?              │
│                                         │
│  [Connect to agent]   [Keep chatting]  │
└─────────────────────────────────────────┘
```
`[Connect to agent]` fires webhook or redirects to configured support URL.

#### State 4 — Error / offline
```
│ ⚠️  Unable to reach support.           │
│     Please try again in a moment.      │
│                                        │
│            [Try again]                 │
```

### Widget configuration via `data-*` attributes

```html
<script src="https://platform.example.com/widget.js"
  data-api-key="pk_live_..."
  data-title="Support Chat"
  data-welcome="👋 Hi! How can I help you today?"
  data-color="#2563EB"
  data-position="bottom-right"
  data-show-sources="true"
  data-escalation-url="https://yoursite.com/contact"
  data-locale="en">
</script>
```

Alternatively via JS init:
```js
window.BotPlatform.init({
  apiKey: "pk_live_...",
  title: "Support Chat",
  color: "#2563EB",
});
```

### Widget backend connections

```
POST /v1/session
      → { session_id }

POST /v1/chat/stream            (SSE)
  headers: Authorization: Bearer {api_key}
  body:    { query, session_id }
  SSE frames:
    data: {"type":"delta",  "content":"To reset"}
    data: {"type":"delta",  "content":" your password"}
    data: {"type":"sources","sources":[{"file":"...","relevance":0.94,"excerpt":"..."}]}
    data: {"type":"done",   "latency_ms":820}

POST /v1/chat/feedback
  body: { session_id, message_index, rating: "up"|"down" }
  → 204
```

---

## Connection Points Summary

### Portal API (`api-portal`) — consumed by `portal-web`
| Area | Key endpoints |
|------|--------------|
| Auth | `POST /portal/auth/login`, `/refresh`, `/logout` |
| Dashboard | `GET /portal/stats/summary` |
| Documents | `GET/POST /portal/documents`, `GET /portal/documents/{id}/status`, `DELETE /portal/documents/{id}` |
| Config | `GET/PUT /portal/config` |
| Test chat | `POST /portal/chat`, `POST /portal/chat/stream` (SSE), `POST /portal/chat/feedback` |
| API keys | `GET/POST /portal/api-keys`, `DELETE /portal/api-keys/{id}` |
| Team | `GET/POST /portal/users`, `DELETE /portal/users/{id}`, `GET/DELETE /portal/users/invites/{id}` |
| Usage | `GET /portal/usage/summary`, `/usage/daily`, `/usage/queries` |
| Evals | `GET /portal/evals/summary`, `/evals/runs`, `/evals/runs/{id}`, `POST /portal/evals/run` |
| Procedures | `GET/POST /portal/procedures`, `GET/PUT /portal/procedures/{id}`, `GET/POST/DELETE /portal/procedures/{id}/steps`, `GET/POST /portal/tools` |
| Simulations | `GET/POST /portal/procedures/{id}/simulations`, `POST .../simulations/{simId}/run`, `GET .../runs/{runId}` |

### Admin API (`api-admin`) — consumed by `admin-web`
| Area | Key endpoints |
|------|--------------|
| Auth | `POST /admin/auth/login` |
| Global stats | `GET /admin/stats/global`, `GET /admin/infra/alerts` |
| Tenants | `GET/POST /admin/tenants`, `GET/PATCH/DELETE /admin/tenants/{id}` |
| Tenant data | `/admin/tenants/{id}/stats`, `/documents`, `/billing`, `/users`, `/evals`, `/procedures` |
| Quality heatmap | `GET /admin/evals/global` |
| Infrastructure | `GET /admin/infra/health`, `/infra/collections`, `/infra/jobs` |
| Audit | `GET /admin/audit` |

### Public Chat API (`api-public`) — consumed by `widget`
| Area | Key endpoints |
|------|--------------|
| Session | `POST /v1/session`, `GET /v1/session/{id}` |
| Chat | `POST /v1/chat` (sync), `POST /v1/chat/stream` (SSE) |
| Feedback | `POST /v1/chat/feedback` |

---

## Shared Zustand Store Patterns

### Portal auth store
```ts
interface AuthStore {
  user: { id, email, name, role } | null
  tenantName: string
  token: string | null
  login: (email, password) => Promise<void>
  logout: () => void
}
```

### Documents store
```ts
interface DocumentsStore {
  documents: Document[]
  uploading: boolean
  fetch: () => Promise<void>
  upload: (files: File[]) => Promise<void>
  delete: (id: string) => Promise<void>
  pollStatus: (id: string) => void   // starts polling, stops on terminal state
}
```

### Procedures store
```ts
interface ProceduresStore {
  procedures: Procedure[]
  activeProcedure: ProcedureDetail | null
  fetch: () => Promise<void>
  fetchDetail: (id: string) => Promise<void>
  updateStep: (procedureId, step) => Promise<void>
  runSimulation: (procedureId, simId) => Promise<SimulationRun>
}
```

### Widget chat store
```ts
interface ChatStore {
  sessionId: string | null
  messages: Message[]
  streaming: boolean
  send: (query: string) => Promise<void>   // uses AI SDK useChat internally
  feedback: (messageIndex, rating) => void
  reset: () => void
}
```
