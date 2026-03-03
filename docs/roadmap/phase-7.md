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

#### Integration tests (Testcontainers)
- [ ] Full procedure run: procedure completes all steps, returns expected response
- [ ] Tool call: mock HTTP server returns data, attributes stored correctly
- [ ] Non-linear navigation: customer provides step-3 info in step-1 → step-3 skipped correctly
- [ ] Escalation: checkpoint step pauses execution, resumes after mock human approval
- [ ] Procedure vs RAG fallback: query below trigger threshold routes to RAG correctly
- [ ] Two tenants: Tenant A procedure does not activate for Tenant B

#### Simulation tests
- [ ] Simulation engine produces a full conversation and a judgment
- [ ] Happy path simulation passes
- [ ] Intentionally broken procedure (missing step) → simulation fails with clear reason
- [ ] Mock tool failure → simulation detects and reports error handling

#### E2E tests (Playwright .NET — portal)
- [ ] Create procedure → add steps → add condition → run simulation → see pass
- [ ] Set procedure live → chat widget triggers it → procedure completes
- [ ] Edit procedure → simulation reruns → regression detected → blocked from going live

### Quality Gate ✅
- At least one complete procedure runs end-to-end via chat widget
- Trigger classifier: precision ≥ 0.90, recall ≥ 0.85 on a test set of 20 queries
- All simulation types (happy path, edge case) produce meaningful pass/fail + reasoning
- Procedure can only go live when all its simulations pass (enforced by API)
- Tool connector: HTTP call with Bearer auth succeeds in integration test
- Portal editor usable without documentation (UX test: build procedure in <10 minutes)

---
