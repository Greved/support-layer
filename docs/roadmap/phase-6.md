[← Index](index.md) · [← Phase 5](phase-5.md) · [Phase 7 →](phase-7.md)

---

## Phase 6 — Evals & Quality Infrastructure
**Goal:** Objectively measure and continuously improve answer quality, retrieval accuracy, and bot
behavior. Quality is trackable per-tenant, per-document-set, and per-config-change. Regressions
are caught before they reach production.

> **Start date:** Evals v1 scaffolding starts in Phase 1 alongside the first working query flow.
> Each sub-milestone below maps to the phase where it becomes a quality gate.

### Why not just RAGAS out of the box
RAGAS provides the core metric algorithms but is not a complete eval platform. The full system here
adds: synthetic dataset generation, per-tenant golden datasets, CI regression gating, human feedback
integration, simulation harness (for Procedures), and a dashboard. Tool recommendation:
- **RAGAS** — metric implementations (Faithfulness, Context Precision/Recall, Answer Relevancy)
- **DeepEval** — test runner, LLM-as-Judge harness, pytest integration, CI gating
- **Custom infrastructure** — synthetic generation, golden datasets, trend storage, dashboard

---

### Eval v1 — First metrics (Phase 1 milestone)

**Goal:** Baseline scores exist before any client goes live.

#### Metrics defined

| Metric | What it measures | Tool |
|--------|-----------------|------|
| **Faithfulness** | Answer claims supported by retrieved context (0–1) | RAGAS |
| **Answer Relevancy** | How directly the answer addresses the question (0–1) | RAGAS |
| **Context Precision** | Relevant chunks ranked above irrelevant ones (0–1) | RAGAS |
| **Context Recall** | Retrieved context covers all info needed to answer (0–1) | RAGAS |
| **Noise Sensitivity** | Score drop when irrelevant chunks are injected (lower = better) | RAGAS |
| **Answer Completeness** | Answer addresses all aspects of the question | DeepEval G-Eval |
| **Hallucination Rate** | Statements not supported by any source | DeepEval |

#### Synthetic dataset generation pipeline
```
tenant docs in Qdrant
        │
        ▼
  LLM (Gemini or local)
  generates for each chunk:
    - 1 simple question
    - 1 multi-hop question (requires 2+ chunks)
    - 1 adversarial / edge case question
    - ground_truth answer
        │
        ▼
  eval_datasets table in PostgreSQL
  (tenant_id, question, ground_truth, source_chunks[], generated_at, version)
        │
        ▼
  eval runs: run_query() for each question → collect (answer, retrieved_chunks)
        │
        ▼
  RAGAS / DeepEval score each row → store in eval_results
```

#### PostgreSQL schema additions
```sql
eval_datasets     (id, tenant_id, question, ground_truth, source_chunk_ids jsonb,
                   question_type, created_at, dataset_version)
eval_runs         (id, tenant_id, run_type, config_snapshot jsonb, triggered_by,
                   started_at, finished_at, status)
eval_results      (id, run_id, dataset_item_id, answer, retrieved_chunks jsonb,
                   faithfulness, answer_relevancy, context_precision, context_recall,
                   hallucination_score, answer_completeness, latency_ms)
eval_baselines    (id, tenant_id, run_id, set_at)   -- pinned "passing" run for regression
```

#### Tasks
- [ ] `eval/` Python module: `generate_dataset.py`, `run_eval.py`, `score.py`
- [ ] RAGAS integration: call `evaluate()` with retrieved context and answer
- [ ] DeepEval integration: `@pytest.mark.eval` test cases with `assert_test()`
- [ ] Synthetic generation CLI: `python -m eval.generate --tenant {slug} --count 50`
- [ ] Store results in PostgreSQL with full context snapshot

---

### Eval v2 — CI regression gating (Phase 2 milestone)

**Goal:** A config or code change that hurts answer quality fails the PR.

#### CI eval pipeline (GitHub Actions)
```yaml
eval-gate:
  runs-on: ubuntu-latest
  steps:
    - start services (docker-compose)
    - run eval suite against staging tenant
    - compare scores to pinned baseline (eval_baselines)
    - fail if any metric drops > 5% relative to baseline
    - post score summary as PR comment
```

#### Regression rules
```python
REGRESSION_THRESHOLDS = {
    "faithfulness":       0.05,   # allow max 5% relative drop
    "answer_relevancy":   0.05,
    "context_precision":  0.08,
    "context_recall":     0.08,
    "hallucination_rate": 0.03,   # absolute, not relative
}
```

#### Triggers
| Change type | Eval scope | Gate |
|-------------|-----------|------|
| PR to `rag-core` | Platform-wide eval tenant | Required pass |
| Tenant ingests new docs | That tenant's dataset | Advisory (logged) |
| Tenant changes system prompt / model | That tenant's dataset | Shown in portal |
| Tenant updates Procedure | Procedure simulations | Required pass before go-live |

#### Tasks
- [ ] GitHub Actions job: `eval-gate` on every PR to `rag-core`
- [ ] Baseline pinning CLI: `python -m eval.set_baseline --tenant {slug} --run-id {id}`
- [ ] PR comment bot: post metric table with delta vs baseline
- [ ] Eval run triggered on ingest completion (Hangfire enqueues job → calls `rag-core` `POST /internal/eval/run`)

---

### Eval v3 — Admin eval dashboard (Phase 3 milestone)

**Goal:** You can see quality trends for every tenant and catch problems before clients complain.

#### Admin API additions
```
GET  /admin/tenants/{id}/evals/runs          (list runs, scores, trend)
GET  /admin/tenants/{id}/evals/runs/{runId}  (per-question breakdown)
POST /admin/tenants/{id}/evals/generate      (trigger synthetic dataset refresh)
POST /admin/tenants/{id}/evals/run           (trigger manual eval run)
GET  /admin/evals/global                     (platform-wide quality heatmap)
```

#### Admin SPA additions
- Quality tab on tenant detail: sparkline charts per metric over time
- Per-run drill-down: question-level table (question / answer / score / retrieved chunks)
- Global heatmap: all tenants × metrics, color-coded (green/yellow/red)
- Alert when a tenant's faithfulness drops below configurable threshold

---

### Eval v4 — Customer-facing quality page (Phase 4 milestone)

**Goal:** Clients can see how well their bot is performing and what's dragging it down.

#### Portal API additions
```
GET  /portal/evals/summary       (current scores vs previous run, trend chart data)
GET  /portal/evals/runs          (list of eval runs for this tenant)
GET  /portal/evals/runs/{id}     (per-question detail, filtered to low-scoring items)
```

#### Portal SPA additions
- **Quality** tab in sidebar: score cards per metric with trend arrows
- Low-scoring questions list: "These questions score poorly — consider adding more content on these topics"
- Suggested documents to add (topics that have low context recall)

---

### Eval v5 — Human feedback loop + production signal (Phase 5 milestone)

**Goal:** Real user feedback from the widget feeds back into the eval dataset, making golden datasets
more representative and catching drift in production.

#### Feedback collection
- Widget: 👍 / 👎 on each response + optional free-text
- Stored in `chat_message_feedback` table with `rating`, `comment`, `flagged`
- Negative feedback with text → reviewed in admin, promoted to golden dataset if confirmed wrong

#### Production eval metrics
These run continuously in production (sampled, not every query):

| Signal | Source | Tracked in |
|--------|--------|-----------|
| Thumbs up rate | Widget feedback | `billing_events` + dashboard |
| Escalation rate | Handoff events | `chat_sessions` |
| Session resolution rate | Session end state | `chat_sessions` |
| Average turns to answer | Message count | `chat_messages` |
| Bounce rate | Single-message sessions | `chat_sessions` |

#### Drift detection
- Nightly job: compare last 7-day production thumbs-up rate vs 30-day baseline
- Alert if user satisfaction drops > 10% week-over-week
- Correlate with recent config changes, doc updates, or model changes

#### Tasks
- [ ] `chat_message_feedback` table + feedback endpoint in `api-public`
- [ ] Feedback UI in widget (subtle 👍/👎 after response)
- [ ] Admin feedback review queue: "promote to golden dataset" action
- [ ] Nightly drift detection job + alerting

### Frontend Design
> Reference: `docs/references/design/stitch_stark_fintech_prd/quality_assurance/`

**Portal Quality page (Eval v4 milestone — sidebar "Quality" entry):**
- Left sidebar "Analysis" sub-section: Assurance · Audit Logs entries
- Two donut charts side by side: Groundedness score (e.g. 92%) · Answer Relevancy score (e.g. 78%); each with numeric label in center and color arc
- Low-confidence queries table below charts: QUERY · GENERATED ANSWER columns; flagged items in the list represent production queries that scored below threshold
- Right panel "Trace View": vertical step display — USER QUERY → RETRIEVED DOCS → LLM REASONING → FINAL ANSWER; each step is expandable to show full content
- Per-row actions: "Mark Correct" (promote to golden dataset) · "Fix in Knowledge Base" (opens document manager filtered to relevant doc)

**Admin eval additions (Eval v3 milestone):**
- Quality tab on tenant detail: sparkline charts per metric over time; per-run drill-down table: question · answer · per-metric scores · retrieved chunks preview
- Global heatmap: all tenants × metrics grid, cells color-coded green/yellow/red by score band

**Widget feedback UI (Eval v5 milestone):**
- Subtle 👍/👎 row beneath each bot response in the chat widget; optional free-text comment on thumbs-down; no interruption to the conversation flow

### Quality Gate ✅ (Phase 6)
- Synthetic dataset generated for at least one real tenant
- All RAGAS metrics computed and stored
- CI eval gate blocks a deliberately degraded PR (test this explicitly)
- Admin dashboard shows trend data for 2+ tenants
- Portal quality page loads with real scores
- Human feedback stored and reviewable in admin
- Production drift alert fires correctly in staging when feedback rate drops

---
