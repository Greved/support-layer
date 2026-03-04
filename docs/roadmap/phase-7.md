[← Index](index.md) · [← Phase 6](phase-6.md) · [Phase 7b →](phase-7b.md)

---

## Phase 7 — Procedures Engine
**Goal:** Clients can define structured, multi-step workflows (Procedures) that the bot follows for
specific support scenarios — combining natural language instructions, branching logic, external API
calls, and human escalation. Modeled after [Fin Procedures](https://fin.ai/procedures).

### What Procedures enable vs pure RAG

| RAG only | Procedures |
|----------|-----------|
| Answer questions from docs | Execute multi-step workflows |
| Single-turn best-effort | Follow a defined process start-to-finish |
| No external data at query time | Call live APIs (order status, account data) |
| No business logic | If/else branching, eligibility checks |
| No human approval | Checkpoint: pause and await human approval |
| No reusable flows | Sub-procedures (e.g., shared "verify identity" step) |
| Hard to test systematically | Built-in Simulations with pass/fail criteria |

### How execution works

```
User message arrives
        │
        ▼
  Trigger Classifier
  (embedding similarity + LLM scoring vs procedure trigger_examples)
        │
   ┌────┴────┐
   │ match?  │
   no       yes (procedure_id, confidence)
   │         │
   ▼         ▼
  RAG    Procedure Executor
  flow   (step-by-step loop)
              │
              ▼
        For each step:
        ┌─────────────────────────────────────────┐
        │ instruction → LLM generates response    │
        │ condition   → LLM or Python evaluates   │
        │              → branch to true/false     │
        │ tool_call   → HTTP connector call       │
        │              → store response attrs     │
        │ sub_procedure → recurse                 │
        │ checkpoint  → notify human, pause       │
        │ escalate    → hand off, end             │
        │ end         → close procedure           │
        └─────────────────────────────────────────┘
              │
        LLM reads full procedure context at each step
        (non-linear: can jump, revisit, or switch procedure)
```

### Data model

```sql
procedures (
  id, tenant_id, name, description, status,  -- status: draft|live|paused
  trigger_instructions text,                  -- natural language: "activate when customer..."
  trigger_examples     jsonb,                 -- [{query, should_trigger: bool}]
  trigger_threshold    float default 0.80,    -- min confidence to activate
  version              int,
  created_at, updated_at
)

procedure_steps (
  id, procedure_id, order_idx, name,
  step_type   text,   -- instruction|condition|tool_call|sub_procedure|checkpoint|escalate|end
  content     text,   -- natural language instruction or condition description
  condition_code text, -- optional Python for deterministic conditions
  tool_id     uuid references procedure_tools,
  sub_procedure_id uuid references procedures,
  on_true_step_id  uuid,   -- branch target for conditions
  on_false_step_id uuid,
  attributes_out   jsonb   -- {attr_name: "json.path.to.extract"} from tool response
)

procedure_tools (
  id, tenant_id, name, description,
  tool_type   text,   -- http_rest|webhook|mcp
  base_url    text,
  auth_type   text,   -- none|bearer|api_key|oauth2
  auth_config jsonb,  -- encrypted at rest
  request_schema  jsonb,  -- JSON Schema for inputs
  response_schema jsonb   -- JSON Schema for outputs
)

procedure_attributes (
  id, procedure_id, name, data_type, description, required bool, default_value text
)

-- Simulations
procedure_simulations (
  id, procedure_id, name,
  scenario_description text,        -- "A customer who bought order #1234 wants a refund because..."
  success_criteria     text,        -- "Procedure should collect order ID, check eligibility, issue refund"
  mock_tool_responses  jsonb,       -- {tool_id: {mock_response_json}} for testing without real APIs
  created_at
)

simulation_runs (
  id, simulation_id, procedure_id,
  conversation_json  jsonb,         -- full message history
  judgment_json      jsonb,         -- LLM judge output: {passed, score, reasoning, failure_points[]}
  passed             bool,
  triggered_by       text,          -- manual|ci|pre_publish
  run_at
)
```

### Procedure execution engine (`rag-core`)

#### Trigger classifier
```python
class ProcedureTriggerClassifier:
    """
    1. Embed incoming query
    2. For each active procedure: cosine-sim vs stored trigger_examples embeddings
    3. For top candidates: LLM judges "does this query match the procedure trigger?"
    4. Return highest-confidence match above threshold, or None → fall through to RAG
    """
```

#### Execution loop
```python
class ProcedureExecutor:
    session: ProcedureSession          # holds attributes, step history
    procedure: Procedure
    llm: LLMClient
    tool_runner: ToolRunner

    def run_step(self, step: ProcedureStep, user_message: str) -> StepResult:
        match step.step_type:
            case "instruction":
                # LLM generates response using step content + session context
            case "condition":
                # code evaluation OR LLM: returns true/false → next step
            case "tool_call":
                # execute HTTP connector, extract attrs, store in session
            case "sub_procedure":
                # push sub-procedure onto execution stack
            case "checkpoint":
                # emit escalation event, pause execution, await resume
            case "escalate" | "end":
                # terminate, emit event
```

#### Attribute system
Attributes are conversation-scoped key-value pairs collected during execution:
```python
# Example: procedure asks for email, stores it as attribute
session.attributes["customer_email"] = "alice@example.com"
# Next step can reference it: "Use {customer_email} to look up the account"
# Tool call can inject it: POST /accounts/lookup {email: session["customer_email"]}
```

#### Non-linear navigation
The LLM receives the **full procedure context** (all steps, descriptions) at each turn. If the
customer goes off-script, the LLM can:
- Jump ahead (customer already provided info a step would collect)
- Revisit a step (customer says "wait, I meant a different order")
- Switch procedures (customer's actual problem differs from initial trigger)
- Fall back to RAG (question not covered by any procedure step)

### Simulation engine

```python
class ProcedureSimulation:
    """
    Runs a two-actor conversation:
      Actor 1: customer_llm — plays the customer based on scenario_description
      Actor 2: procedure_executor — the real bot using real procedure logic
      Actor 3: judge_llm — after conversation ends, evaluates against success_criteria
    """
    def run(self, simulation: Simulation) -> SimulationResult:
        messages = []
        # customer opens with first message
        # bot executes procedure
        # customer responds (played by LLM using scenario)
        # repeat until procedure ends or max_turns
        # judge evaluates: did the procedure achieve success_criteria?
        return SimulationResult(passed=..., score=..., reasoning=..., failures=[...])
```

### Tool connector framework

```python
class HTTPToolConnector:
    """Generic REST connector. Auth handled via auth_type + auth_config."""
    def call(self, tool: ProcedureTool, inputs: dict) -> dict:
        headers = self._build_auth_headers(tool)
        response = httpx.post(tool.base_url, json=inputs, headers=headers, timeout=10)
        response.raise_for_status()
        return self._extract_attributes(response.json(), tool.attributes_out)
```

Supported auth types: `none`, `bearer` (static token), `api_key` (header or query param),
`oauth2_client_credentials`. Credentials stored encrypted in `procedure_tools.auth_config`.

### Portal UI — Procedure editor (extends Phase 4 portal SPA)

**Procedure list page:**
- Table of procedures: name, status (draft/live/paused), trigger count (7-day), resolution rate
- Create from template (FAQ escalation, order refund, account reset, subscription change)
- AI-assisted draft: describe the process → Fin generates initial steps

**Procedure editor (step builder):**
```
┌─────────────────────────────────────────────────────┐
│ Procedure: "Process refund request"           [Live] │
├──────────────────┬──────────────────────────────────┤
│ Steps            │  Step editor                      │
│ ──────────       │  ─────────────────                │
│ 1. Greet         │  Type: Instruction                │
│ 2. Get order ID  │                                   │
│ 3. Look up order │  Content:                         │
│ 4. [condition]   │  "Ask the customer for their      │
│    eligible?     │   order number. If they don't     │
│    ├─ yes → 5    │   know it, ask for their email    │
│    └─ no  → 7    │   address instead."               │
│ 5. Issue refund  │                                   │
│ 6. Confirm       │  Attribute out: order_id          │
│ 7. Deny + reason │                                   │
│ [+ Add step]     │  [Save step]                      │
└──────────────────┴──────────────────────────────────┘
```

**Simulation tab:**
- Add simulation: name, scenario, success criteria, mock tool responses
- Run all → see pass/fail table with conversation replay
- Rerun on every procedure save → catch regressions

**Analytics tab:**
- Trigger rate (how often activated per 100 queries)
- Completion rate vs escalation rate vs abandon rate
- Average steps executed per session
- Tool call success/failure rates

### Procedure + Evals integration
- Every procedure has a set of simulations (eval-like)
- `procedure_simulations` rows are also eval dataset items
- Simulation results feed into the tenant's overall quality dashboard
- CI gate: procedure can only go `live` if all its simulations pass

### Frontend Design
> References: `docs/references/design/stitch_stark_fintech_prd/`
> `textual_procedure_editor_stark_refined/` · `procedure_simulation_runner_stark/`

**Textual Procedure Editor (default / list view):**
- Breadcrumb in top bar: PROCEDURES › PROCEDURE NAME (all-caps, uppercase styling)
- Tabs: Canvas · History · Analytics (Canvas is the default; list/text view is always available via toggle)
- Left panel "PROCEDURE EDITOR" label + numbered STEPS list: current step shows filled checkmark; others show unfilled circle; drag-handle icon on right of each row for reordering; "+ Add New Step" footer button
- Center panel: step name as heading + ACTIVE/DRAFT status badge; STEP TYPE dropdown (Instruction / Condition / Tool Call / …); INSTRUCTIONS FOR AGENT textarea with monospace hint text `Use {{attribute}} to inject dynamic data` in a blue info bar; "Delete Step" (red) + "Duplicate" buttons at bottom
- Right panel, two sub-sections:
  - ATTRIBUTES: scrollable list of defined attributes — attribute name (monospace blue) + type label + description; "+" add button top-right
  - CONNECTED TOOLS: connected tools shown with name + Connected/Inactive status chip + HTTP method + endpoint path; greyed-out inactive tools; "+ Connect Tool" text button at bottom

**Simulation Runner (Simulation tab inside procedure editor):**
- 3-panel layout:
  - Left: simulation scenarios list — scenario name, scenario description excerpt; active scenario highlighted
  - Center: conversation replay — customer/bot message bubbles (same style as widget); tool call events shown inline as an expandable "tool call" card between messages displaying tool name + request/response JSON summary
  - Right: Configure API Call panel — METHOD dropdown + URL input + HEADERS key-value editor + BODY JSON textarea + RESPONSE MAPPING fields

**Procedure List page:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/procedure_list_stark/`
- Title: "Procedures {N} Total"; "+ Create from Template" secondary button + "+ New Procedure" primary button
- Tabs: All Procedures · Active · Drafts · Archived; search input + Filter button
- Table columns: NAME (bold + procedure ID in gray below) · STATUS · TRIGGER RATE (7D) · RESOLUTION RATE (progress bar + %) · ACTIONS (edit pencil + pin icon + ⋮ menu)
- Status badges: Live = green filled pill · Paused = amber pill · Draft = outlined gray pill
- Bottom summary row: 3 KPI cards — Average Resolution % · Active Triggers count · Weekly Volume (events); weekly volume card uses blue highlight background
- Pagination: "SHOWING N OF N PROCEDURES" + Previous/Next

**Procedure Analytics tab:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/procedure_analytics_stark/`
- Tabs: Steps · Simulations · Analytics (active = blue underline) within the procedure editor; breadcrumb shows procedure name
- Title: "{Procedure Name} Analytics"; "Export Report" + "Edit Procedure" buttons top-right
- 4 KPI cards: TRIGGER RATE · COMPLETION RATE · ESCALATION RATE · ABANDON RATE — each with small icon, large % value, green/red trend badge vs last month
- "Performance Trends" area/line chart: Completion (blue) vs Escalation (gray) dual lines over 30-day x-axis; legend top-right
- "Tool Call Performance" table: TOOL NAME (icon + name) · SUCCESS RATE (progress bar + %) · AVG LATENCY · FAILURE RATE (red text) · VOLUME (30D); paginated

### Tasks
- [ ] `procedure/` Python module in `rag-core`: `trigger.py`, `executor.py`, `tool_runner.py`, `simulation.py`
- [ ] PG migrations for procedures schema
- [ ] Trigger classifier: embedding-based candidate selection + LLM scoring
- [ ] Execution engine: step processor, attribute system, non-linear navigation
- [ ] Condition evaluator: safe Python sandbox (`RestrictedPython`) + LLM fallback
- [ ] HTTP tool connector with all auth types
- [ ] Simulation engine: customer LLM actor + judge LLM
- [ ] `api-portal` endpoints: CRUD for procedures, tools, simulations, run simulation
- [ ] Portal SPA: procedure list, step editor, tool manager, simulation runner, analytics
- [ ] CI: simulate all tenant procedures on `rag-core` PRs, fail on regression

### Tests (Phase 7)

#### Unit tests
- [ ] Trigger classifier: query matches correct procedure, does not trigger on non-matching query
- [ ] Step processor: each step type executes correctly
- [ ] Condition evaluator: Python conditions correct, LLM condition branching
- [ ] Attribute extraction from tool responses
- [ ] Auth header building for all auth types

#### Integration tests (Testcontainers Postgres + Python pytest + mock HTTP server)
- [ ] **CRUD endpoints (.NET TestServer):** `POST /portal/procedures` → 201; `GET /portal/procedures` → list; `PATCH /portal/procedures/{id}` updates name/trigger; `DELETE /portal/procedures/{id}` → 204; unknown ID → 404
- [ ] **Step CRUD:** add instruction step → 201; add condition step with `on_true_step_id` + `on_false_step_id` → 201; reorder steps → verify `order_idx` persists; delete step → cascades out of execution order
- [ ] **Tool CRUD:** `POST /portal/tools` with Bearer auth config → 201; auth config encrypted at rest (not returned in GET); `DELETE /portal/tools/{id}` → 204
- [ ] **Simulation CRUD:** `POST /portal/procedures/{id}/simulations` → 201; `GET /portal/procedures/{id}/simulations` → list; `POST /portal/procedures/{id}/simulations/{sid}/run` → 202 + `runId`
- [ ] **Full procedure run (Python):** seed 3-step procedure (instruction → tool_call → end) with mock HTTP tool server returning `{orderId: "123"}`; send trigger message → procedure executes all 3 steps → response contains tool response data; `procedure_sessions` row has `status=complete`
- [ ] **Tool call with auth:** HTTP connector sends `Authorization: Bearer <token>` header to mock server; mock server verifies header; missing auth → step fails gracefully with error recorded in session
- [ ] **Attribute extraction:** tool response `{customer: {email: "alice@example.com"}}` with `attributes_out = {"customer_email": "customer.email"}` → `session.attributes["customer_email"] = "alice@example.com"`
- [ ] **Non-linear navigation:** 4-step procedure; customer's first message provides step-3 data; executor skips to step 4 without re-asking for step-3 info
- [ ] **Condition branch — Python eval:** condition `session.attributes["amount"] > 100` with `amount=150` → true branch taken; `amount=50` → false branch taken
- [ ] **Checkpoint pause/resume:** checkpoint step → session state set to `paused`; `POST /portal/procedures/sessions/{id}/resume` → execution continues from next step
- [ ] **Escalate step:** escalate step → session ends with `status=escalated`; escalation webhook event emitted
- [ ] **Procedure vs RAG fallback (Python):** query with similarity below `trigger_threshold` → classifier returns `None` → request routed to RAG pipeline; no `procedure_session` row created
- [ ] **Tenant isolation:** Tenant A procedure with `trigger_threshold=0.80`; Tenant B sends exact trigger phrase → classifier does not activate Tenant A's procedure for Tenant B
- [ ] **Go-live gate:** `PATCH /portal/procedures/{id}` with `status=live` when simulations exist but one fails → 422 with `{failedSimulations: [...]}`; all simulations passing → status updated to `live`

#### Simulation tests
- [ ] Simulation engine produces a full conversation and a judgment
- [ ] Happy path simulation passes
- [ ] Intentionally broken procedure (missing step) → simulation fails with clear reason
- [ ] Mock tool failure → simulation detects and reports error handling

#### E2E tests (Playwright .NET — portal)
- [ ] **Procedure list:** Navigate to Procedures → verify table renders with NAME / STATUS / TRIGGER RATE / RESOLUTION RATE columns → verify status badges (Live = green, Paused = amber, Draft = outlined) → filter to Active tab → verify only live procedures shown → search by name → verify filtered result
- [ ] **Create from template:** Click "+ Create from Template" → select template → verify steps pre-populated in step list → save → verify new procedure appears in list with Draft status
- [ ] **Step builder:** Create new procedure → add Instruction step (enter instructions text) → add Condition step (set true/false branch targets) → add Tool Call step (configure URL + method) → add End step → save → verify step list shows all 4 steps in correct order
- [ ] **Attribute injection:** Define attribute `order_id` → reference it in an instruction step as `{{order_id}}` → verify blue info bar hint is visible → save → verify attribute listed in right panel ATTRIBUTES section
- [ ] **Tool connector:** Add HTTP tool with Bearer auth → configure base URL + endpoint → click "Connect Tool" → verify tool listed as Connected with green status chip → verify endpoint path shown
- [ ] **Simulation runner:** Add simulation with scenario and success criteria → add mock tool response → run simulation → verify conversation replay panel shows customer/bot message bubbles → verify tool call event card visible between messages → verify pass/fail judgment shown
- [ ] **Regression gate:** Create passing simulation → set procedure live → edit a step in a breaking way → attempt to re-publish → verify blocked with simulation failure message → verify procedure reverts to Draft
- [ ] **Widget trigger:** Set procedure live → open embedded widget → send message matching trigger phrase → verify procedure executes (bot follows procedure steps instead of RAG) → verify procedure completes and session ends correctly
- [ ] **Analytics tab:** Open procedure → click Analytics tab → verify 4 KPI cards render (Trigger Rate / Completion Rate / Escalation Rate / Abandon Rate) → verify Performance Trends chart has two lines → verify Tool Call Performance table lists connected tools with success rates
- [ ] **Tenant isolation:** Tenant A creates procedure → Tenant B logs in → verify Tenant B cannot see or trigger Tenant A's procedure

### Quality Gate ✅
- At least one complete procedure runs end-to-end via chat widget
- Trigger classifier: precision ≥ 0.90, recall ≥ 0.85 on a test set of 20 queries
- All simulation types (happy path, edge case) produce meaningful pass/fail + reasoning
- Procedure can only go live when all its simulations pass (enforced by API)
- Tool connector: HTTP call with Bearer auth succeeds in integration test
- Portal editor usable without documentation (UX test: build procedure in <10 minutes)

---
