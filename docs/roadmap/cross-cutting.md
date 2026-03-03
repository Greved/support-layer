[← Index](index.md)

---

## Cross-Cutting Concerns

### Multi-tenancy rules (enforce everywhere)
1. Every DB query includes `WHERE tenant_id = ?` — enforce via EF Core global filter
2. Every Qdrant operation uses resolved collection `tenant_{slug}`
3. JWT and API key always resolve to a `tenant_id`; absent = 403
4. Super-admin tokens issued by separate issuer; never cross into portal routes

### Testing strategy summary

| Level | Tool | When |
|-------|------|------|
| Unit | pytest (Python), xUnit (.NET), Vitest (React) | Every PR |
| Integration (.NET) | .NET TestServer + Testcontainers | Every PR |
| Integration (Python) | pytest + httpx + Testcontainers-python | Every PR |
| E2E | **Playwright .NET** | Every PR (fast subset), nightly (full) |
| **Eval (RAG quality)** | **RAGAS + DeepEval + pytest** | **Every PR to rag-core; on ingest; nightly** |
| **Simulation (Procedures)** | **Custom simulation engine** | **Before procedure goes live; on every edit** |
| Load | k6 | Phase 5 gate, nightly on staging |
| Security | OWASP ZAP, manual review | Phase 5 gate, quarterly |
| Visual regression | **Playwright .NET** screenshots | Phase 4+ |
| Chaos | Custom scripts | Phase 5 gate |

### Testcontainers usage (integration tests)

**.NET (primary — all business API tests):**
```csharp
// xUnit + .NET TestServer + Testcontainers
await using var pg    = new PostgreSqlContainer("postgres:16").Build();
await using var redis = new RedisContainer("redis:7").Build();
await using var app   = new WebApplicationFactory<Program>()
    .WithWebHostBuilder(b => b.ConfigureServices(s => {
        s.Configure<DatabaseOptions>(o => o.ConnectionString = pg.GetConnectionString());
    })).CreateClient();
```

**Python (rag-core internal API tests):**
```python
# pytest + httpx + testcontainers-python
with PostgresContainer("postgres:16") as pg, QdrantContainer() as qdrant:
    ...
```

### API versioning
- Public APIs versioned: `/v1/chat`, `/v1/session`
- Portal and admin: header versioning `Api-Version: 2025-01`
- Never break a version — add new versions instead

### Secrets and config
- Dev: `.env` files (never committed)
- Staging/Prod: Docker secrets or HashiCorp Vault
- .NET: `IOptions<T>` with `[Required]` validation on startup
- Python: pydantic-settings with validators

---

---

## Analytics & Instrumentation

**Tool: PostHog** — self-hostable, open-source product analytics. Covers event tracking, funnels,
session replay, feature flags, retention cohorts, and dashboards from a single SDK.

**Hosting strategy:**
- Development + staging: PostHog Cloud free tier (1M events/month — sufficient indefinitely at
  early scale). Zero infra overhead.
- Enterprise clients requiring data residency: PostHog can be self-hosted inside their
  infrastructure via Docker Compose or Helm.

---

### SDKs by service

| Service | SDK | Notes |
|---------|-----|-------|
| `portal-web` (React) | `posthog-js` | Auto-captures pageviews, clicks; manual event calls for domain events |
| `admin-web` (React) | `posthog-js` | Separate project key or same project with `$groups` filter |
| `widget` (React UMD) | `posthog-js` (lazy-loaded) | Initialise only after user interaction; respect DNT header |
| `rag-core` (Python) | `posthog` (pip) | Server-side events for ingestion, query quality, evals |
| `api-portal` (.NET) | `PostHog` (NuGet) or HTTP API | Auth events, billing events, quota hits |
| `api-public` (.NET) | HTTP API (fire-and-forget) | Widget query events; no blocking calls in the hot path |
| `api-admin` (.NET) | `PostHog` NuGet | Super-admin actions, infra health events |

---

### Global properties (sent on every event)

Every `posthog.capture()` call should include these via a global middleware/plugin:

```python
# Python (rag-core)
posthog.capture(
    distinct_id=user_id,
    event="...",
    properties={
        # --- always present ---
        "tenant_id":       tenant_id,
        "tenant_plan":     "hobby|starter|pro|business|enterprise",
        "environment":     "production|staging",
        "app_version":     APP_VERSION,      # git tag
        # --- per event ---
        ...
    }
)
posthog.group_identify("tenant", tenant_id, {
    "plan":            tenant_plan,
    "created_at":      tenant_created_at,
    "document_count":  n,
    "query_count_mtd": n,
})
```

PostHog **Group Analytics**: every tenant is a `group` of type `tenant`. This lets you answer
"what % of Pro tenants have created at least one Procedure?" without writing SQL.

---

### Events schema

#### 1 — Identity & Authentication

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `user_signed_up` | `plan`, `invited_by_user_id`, `auth_method` | api-portal | Signup volume; invite-vs-organic split |
| `user_logged_in` | `auth_method: password\|sso`, `mfa_used: bool` | api-portal | DAU, auth method adoption |
| `user_invited` | `role`, `invitee_email_domain` | api-portal | Team growth per tenant |
| `user_invite_accepted` | `days_since_invite` | api-portal | Invite → activation lag |
| `password_reset_requested` | — | api-portal | Auth friction signal |
| `mfa_enrolled` | — | api-portal | Security feature adoption |
| `user_impersonated` | `impersonated_tenant_id`, `admin_user_id` | api-admin | Audit trail complement |

---

#### 2 — Tenant Lifecycle & Trial

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `tenant_created` | `plan`, `source: organic\|invite\|api` | api-portal | New tenant rate |
| `trial_started` | `plan_at_expiry: starter` | api-portal | Trial funnel entry |
| `trial_converted` | `converted_to_plan`, `days_remaining` | api-portal | Trial → paid CVR |
| `trial_expired_without_payment` | `days_since_first_query` | api-portal | Churn root cause |
| `tenant_deleted` | `plan`, `days_active`, `documents_count` | api-admin | Churn signal |

---

#### 3 — Onboarding

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `onboarding_step_viewed` | `step: upload\|configure\|embed\|test` | portal-web | Drop-off by step |
| `onboarding_step_completed` | `step`, `time_on_step_s` | portal-web | Completion time per step |
| `onboarding_completed` | `total_duration_s`, `plan` | portal-web | Full funnel completion rate |
| `onboarding_skipped` | `step_skipped_at` | portal-web | Where users bail |
| `widget_code_copied` | `embed_method: script\|npm` | portal-web | Embed method preference |

**Funnel to build:** `tenant_created` → `onboarding_step_completed {step: upload}` →
`onboarding_step_completed {step: configure}` → `onboarding_step_completed {step: embed}` →
`chat_session_started` (first real query). Drop-off at each step = where to invest UX effort.

---

#### 4 — Document Ingestion

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `document_upload_started` | `file_type`, `size_mb`, `plan` | api-portal | File type distribution |
| `document_upload_rejected` | `reason: type\|size\|quota`, `size_mb` | api-portal | Quota/limit friction |
| `document_ingestion_queued` | `file_type`, `size_mb` | worker | Queue depth insight |
| `document_ingested` | `chunk_count`, `duration_ms`, `file_type`, `size_mb` | rag-core | Ingestion performance |
| `document_ingestion_failed` | `error_type`, `file_type` | rag-core | Error rate by file type |
| `document_deleted` | `age_days`, `chunk_count` | api-portal | Churn signal for content |
| `ingestion_quota_reached` | `plan`, `document_count` | api-portal | Upgrade pressure point |
| `document_reingested` | `old_chunk_count`, `new_chunk_count` | rag-core | Content iteration signal |

---

#### 5 — Chat & Widget (end-user)

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `widget_loaded` | `page_url`, `trigger: auto\|manual`, `position` | widget | Embed health |
| `widget_opened` | `trigger: manual\|proactive\|auto_delay`, `page_url` | widget | Open rate by trigger type |
| `widget_closed` | `session_had_query: bool`, `open_duration_s` | widget | Bounce rate |
| `chat_session_started` | `channel: widget\|portal_test\|slack\|email` | api-public | Volume by channel |
| `chat_message_sent` | `turn_number`, `query_length_chars`, `session_id` | widget / api-public | Engagement depth |
| `chat_response_received` | `latency_ms`, `has_sources: bool`, `source_count`, `confidence_score`, `procedure_triggered: bool` | api-public | Latency, quality signal |
| `chat_source_clicked` | `source_rank`, `source_type: document\|url` | widget | Source quality signal |
| `chat_follow_up_clicked` | `suggestion_rank` | widget | Suggestion usefulness |
| `chat_feedback_positive` | `turn_number`, `latency_ms` | widget | CSAT signal |
| `chat_feedback_negative` | `turn_number`, `comment_provided: bool` | widget | DSAT + improvement signal |
| `chat_escalated` | `reason: user_requested\|low_confidence\|procedure_checkpoint`, `turn_number` | api-public | Escalation rate |
| `chat_session_ended` | `turns`, `had_positive_feedback: bool`, `had_escalation: bool`, `duration_s`, `resolution_state: resolved\|escalated\|abandoned` | api-public | Resolution rate |
| `low_confidence_query` | `max_score`, `threshold`, `query_preview_hash` | rag-core | Knowledge gap detection |

---

#### 6 — Bot Configuration

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `config_saved` | `changed_fields: string[]` | portal-web | Which config fields matter |
| `system_prompt_edited` | `prompt_length_chars`, `previous_length_chars` | portal-web | Prompt iteration frequency |
| `model_changed` | `from_model`, `to_model` | portal-web | Model preference trends |
| `temperature_changed` | `from`, `to` | portal-web | Tuning behaviour |
| `no_answer_message_set` | — | portal-web | Fallback feature adoption |
| `widget_appearance_customised` | `changed_fields: color\|logo\|title\|position` | portal-web | Customisation depth |
| `test_chat_used` | `query_count`, `session_id` | portal-web | Portal test-chat engagement |

---

#### 7 — Procedures

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `procedure_created` | `created_via: wizard\|blank` | portal-web | Creation method split |
| `procedure_step_added` | `step_type`, `step_number`, `via: list\|canvas` | portal-web | Step type distribution; canvas vs list adoption |
| `procedure_step_edited` | `step_type` | portal-web | Iteration depth |
| `procedure_tool_connected` | `tool_type: http_rest\|webhook`, `auth_type` | portal-web | Tool usage |
| `procedure_simulation_run` | `passed: bool`, `step_count`, `turns` | rag-core | Simulation pass rate |
| `procedure_simulation_failed` | `failure_point_step`, `reason_summary` | rag-core | Common failure modes |
| `procedure_published` | `step_count`, `has_conditions: bool`, `has_tool_calls: bool`, `simulation_pass_rate` | api-portal | Complexity at publish time |
| `procedure_paused` | `reason: manual\|regression_detected` | api-portal | Stability issues |
| `procedure_trigger_matched` | `confidence`, `procedure_id` | rag-core | Trigger accuracy |
| `procedure_trigger_missed` | `confidence`, `fell_through_to_rag: bool` | rag-core | Threshold tuning signal |
| `procedure_completed` | `steps_executed`, `duration_s`, `escalated: bool` | rag-core | Completion vs escalation rate |
| `procedure_deleted` | `was_live: bool`, `trigger_count_7d` | api-portal | Abandonment signal |

---

#### 8 — Visual Flow Editor (Phase 7b)

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `canvas_opened` | `step_count`, `procedure_status` | portal-web | Canvas adoption rate |
| `canvas_view_toggled` | `to_view: canvas\|list` | portal-web | Preferred view |
| `canvas_node_dragged` | `step_type` | portal-web | Canvas engagement |
| `canvas_step_added_via_palette` | `step_type` | portal-web | Palette-driven creation rate |
| `canvas_auto_arrange_used` | `node_count` | portal-web | Layout tool adoption |
| `canvas_simulation_replay_viewed` | `passed: bool` | portal-web | Replay feature engagement |

---

#### 9 — Evals & Quality

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `eval_dataset_generated` | `question_count`, `tenant_id`, `model_used` | rag-core | Dataset coverage |
| `eval_run_started` | `trigger: manual\|ci\|post_ingest\|scheduled`, `dataset_size` | rag-core | Eval frequency |
| `eval_run_completed` | `faithfulness`, `answer_relevancy`, `context_precision`, `context_recall`, `hallucination_rate`, `duration_s` | rag-core | Quality trends |
| `eval_regression_detected` | `metric`, `delta`, `baseline_run_id` | rag-core | CI gate effectiveness |
| `eval_baseline_pinned` | `run_id`, `triggered_by: manual\|auto` | rag-core | Baseline management |
| `knowledge_gap_flagged` | `query_topic_cluster`, `occurrence_count` | rag-core | Content improvement signal |

---

#### 10 — Billing & Plans

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `plan_upgraded` | `from_plan`, `to_plan`, `mrr_delta`, `trigger: quota\|feature\|manual` | api-portal | Upgrade rate; trigger analysis |
| `plan_downgraded` | `from_plan`, `to_plan`, `days_on_previous_plan` | api-portal | Downgrade signal |
| `payment_succeeded` | `amount_usd`, `plan`, `billing_period: monthly\|annual` | api-portal (Stripe webhook) | Revenue events |
| `payment_failed` | `reason`, `retry_attempt` | api-portal (Stripe webhook) | Revenue at risk |
| `quota_warning_shown` | `resource: documents\|queries\|storage`, `percent_used`, `plan` | portal-web | Upgrade intent signal |
| `quota_exceeded` | `resource`, `plan`, `request_count_blocked` | api-portal | Hard upgrade pressure |
| `annual_plan_selected` | `plan`, `savings_usd` | portal-web | Annual CVR |

**Funnel to build:** `quota_warning_shown` → `plan_upgraded` — measures how effectively quota
warnings convert to upgrades. If drop-off is high, improve the upgrade CTA or lower the trigger
threshold.

---

#### 11 — Integrations & Webhooks

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `webhook_created` | `event_type`, `plan` | api-portal | Webhook adoption |
| `webhook_delivery_succeeded` | `event_type`, `latency_ms` | worker | Reliability |
| `webhook_delivery_failed` | `event_type`, `status_code`, `retry_attempt` | worker | Integration health |
| `webhook_delivery_exhausted` | `event_type` | worker | Dead endpoint alert signal |
| `integration_connected` | `integration: zendesk\|hubspot\|slack`, `plan` | api-portal | Integration adoption |
| `integration_disconnected` | `integration`, `days_connected` | api-portal | Retention by integration |
| `zapier_trigger_fired` | `event_type` | api-portal | Zapier activity |

---

#### 12 — Admin (super-admin)

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `admin_tenant_created` | `plan`, `created_by: admin\|self_signup` | api-admin | Manual vs organic growth |
| `admin_tenant_plan_changed` | `from_plan`, `to_plan`, `reason` | api-admin | Admin-driven plan changes |
| `admin_tenant_deleted` | `plan`, `days_active`, `mrr_lost` | api-admin | Churn attribution |
| `admin_impersonation_started` | `target_tenant_id` | api-admin | Support investigation rate |
| `infra_health_degraded` | `service: qdrant\|llm\|embed\|redis\|pg`, `status` | api-admin | Reliability signal |

---

#### 13 — Errors & Performance

| Event | Key Properties | Source | Answers |
|-------|---------------|--------|---------|
| `api_error` | `endpoint`, `status_code`, `error_type`, `tenant_id` | all .NET APIs | Error rate by endpoint |
| `rag_query_slow` | `latency_ms`, `threshold_ms: 5000`, `retrieval_ms`, `llm_ms` | rag-core | p95 latency tracking |
| `embedding_service_unreachable` | `retry_count` | rag-core | Infra reliability |
| `llm_service_unreachable` | `provider`, `retry_count` | rag-core | LLM provider reliability |
| `ingestion_job_timeout` | `file_type`, `size_mb`, `timeout_s` | worker | Long-running job signal |
| `rate_limit_hit` | `api_key_id`, `endpoint`, `plan` | api-public | Abuse or legitimate burst |

---

### Key PostHog features to configure

**Funnels** (build these first — highest ROI):
1. **Onboarding:** `tenant_created` → each `onboarding_step_completed` → `chat_session_started`
2. **Plan upgrade:** `quota_warning_shown` → `plan_upgraded`
3. **Procedure adoption:** `tenant_created` → `procedure_created` → `procedure_published` → `procedure_completed`
4. **Trial conversion:** `trial_started` → `trial_converted`

**Retention cohorts:**
- Weekly active tenants (had ≥1 `chat_session_started` in 7 days)
- Monthly active procedures (had ≥1 `procedure_completed` in 30 days)

**Dashboards:**

| Dashboard | Key metrics |
|-----------|------------|
| **Product health** | DAU/WAU/MAU per tenant, queries/day, ingestions/day, widget open rate |
| **Business** | MRR by plan, trial CVR, upgrade rate, quota hit rate by plan |
| **RAG quality** | Avg confidence score, escalation rate, feedback positive rate, low-confidence query rate |
| **Feature adoption** | % tenants with ≥1 Procedure live, % using visual canvas, % with eval baseline pinned |
| **Operations** | p95 query latency, ingestion error rate, webhook delivery success rate |

**Session replay** (portal-web only):
- Enable with PII masking on all `<input>` and `<textarea>` elements
- Use to investigate onboarding drop-off points
- Sample rate: 100% for trial users (high value), 20% for paid (cost control)

**Feature flags** (maps to Phase 8 A/B testing):
- `procedure-visual-canvas` — phased rollout of Phase 7b canvas
- `eval-ci-gate` — controlled rollout of CI eval blocking
- `proactive-widget-greeting` — A/B test proactive vs manual open

---

### Implementation timeline

| Phase | What to add |
|-------|-------------|
| **0** | Add PostHog SDK to docker-compose (optional self-hosted container); configure `identify()` on login for all .NET services; `group_identify()` for tenant group |
| **1** | Ingestion events (`document_uploaded`, `document_ingested`, `document_ingestion_failed`); portal auth events; `tenant_created`, trial events |
| **2** | All widget/chat events in `widget` and `api-public`; `chat_session_ended` pipeline |
| **4** | Portal-web: onboarding funnel events, config events, all portal-web interactions; session replay enabled |
| **5** | Error events, performance events, infra health events; alerting on `rag_query_slow` |
| **7** | All procedure events (creation, publishing, triggering, completion) |
| **7b** | Canvas interaction events |
| **9** | Billing events (plan upgrades, payment, quota warnings) via Stripe webhook handlers |
| **10** | Webhook delivery events, integration events |

### Privacy & GDPR notes
- No PII in event properties — use hashed IDs; queries stored only as `query_length_chars` or `query_preview_hash`
- PostHog Cloud (EU region) satisfies GDPR by default; self-hosted adds an extra layer
- Widget: check `navigator.doNotTrack` before initialising PostHog; honour opt-out
- Tenants can request event deletion via data export + GDPR flow (Phase 3 data export endpoint)
- `posthog-js` cookie consent: integrate with any consent banner the widget host page shows
