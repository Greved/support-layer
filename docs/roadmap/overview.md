[← Index](index.md)

---

# RAG Chat Platform — Overview & Architecture

## Vision

Multi-tenant SaaS RAG platform: each client gets an isolated knowledge base, a configurable chat bot,
and an embeddable widget for their site. You get a central admin panel, billing visibility, and full
infrastructure control.

---

---

## Current State

| Item | Status |
|------|--------|
| Core RAG pipeline (embed → search → generate) | ✅ Working |
| Qdrant vector store + ingestion CLI | ✅ Working |
| llama.cpp (CPU/GGUF) + vLLM (GPU) support | ✅ Working |
| Gemini fallback | ✅ Working |
| Multi-tenancy | ❌ None — single shared collection |
| Auth | ❌ None |
| PostgreSQL | ❌ Not integrated |
| Customer portal | ❌ None |
| Internal admin | ❌ None |
| Chat widget | ❌ None |
| Billing/usage tracking | ❌ None |
| Observability | ⚠️ Basic logging only |
| Tests | ⚠️ Skeleton only |

---

---

## Target Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        External Clients                           │
│   Client A website    Client B website    ...                    │
│   ┌──────────────┐    ┌──────────────┐                           │
│   │  Chat Widget │    │  Chat Widget │  (embeddable JS/React)    │
│   └──────┬───────┘    └──────┬───────┘                           │
└──────────┼────────────────────┼──────────────────────────────────┘
           │  HTTPS + API key   │
┌──────────▼────────────────────▼──────────────────────────────────┐
│                    .NET Public API (ASP.NET Core)                  │
│   POST /v1/chat   GET /v1/session   SSE streaming                │
│   Auth: API key per tenant  ·  Rate limiting  ·  CORS            │
└──────────────────────────┬───────────────────────────────────────┘
                           │ internal HTTP
           ┌───────────────┼───────────────┐
           │               │               │
┌──────────▼───────┐ ┌─────▼──────┐ ┌────▼──────────────────────┐
│ .NET Customer    │ │ .NET Admin │ │  Python RAG Service        │
│ Portal API       │ │ API        │ │  (FastAPI / Haystack)      │
│                  │ │ (internal) │ │  POST /internal/query      │
│ /portal/*        │ │ /admin/*   │ │  POST /internal/embed      │
│ JWT auth         │ │ Super-admin│ │  POST /internal/ingest     │
│ Tenant-scoped    │ │ JWT        │ │                            │
└──────────────────┘ └────────────┘ └────────────┬───────────────┘
                                                  │
                    ┌─────────────────────────────┼─────────────────┐
                    │                             │                  │
             ┌──────▼──────┐            ┌─────────▼────┐  ┌────────▼──────┐
             │  PostgreSQL │            │    Qdrant     │  │ LLM / Embed   │
             │             │            │  per-tenant   │  │ llama.cpp     │
             │ tenants     │            │  collections  │  │ or vLLM       │
             │ users       │            │               │  └───────────────┘
             │ documents   │            └───────────────┘
             │ configs     │
             │ billing     │
             │ api_keys    │
             └─────────────┘

Frontends (React + TypeScript, separate repos/apps):
  - Customer Portal SPA  → talks to .NET Customer Portal API
  - Internal Admin SPA   → talks to .NET Admin API
  - Chat Widget          → talks to .NET Public Chat API
```

---

---

## Tech Stack Decision

### Core RAG Service — keep Python FastAPI + Haystack
- Haystack 2.x is the best Python RAG framework; replacing it gains nothing
- llama.cpp / vLLM integration is native Python
- Becomes an **internal microservice only** — not exposed to external clients directly
- Scope narrows to: embed, search, generate, ingest

### Business / API layer — add .NET ASP.NET Core
**Why .NET for the API gateway and portal APIs:**
- Your existing expertise → faster delivery and fewer bugs in auth/billing/tenant logic
- ASP.NET Core outperforms FastAPI on raw HTTP throughput
- Better ecosystem for auth (ASP.NET Identity, Duende IdentityServer), EF Core for PostgreSQL
- Strong typed API contracts, OpenAPI out of the box
- Natural fit for multi-tenant SaaS patterns (tenant middleware, claim-based auth)

**Service breakdown:**

| Service | Language | Responsibility |
|---------|----------|----------------|
| `rag-core` | Python / FastAPI | Embedding, vector search, LLM generation, ingestion pipeline |
| `api-public` | .NET ASP.NET Core | Chat widget API, API-key auth, SSE streaming, rate limiting |
| `api-portal` | .NET ASP.NET Core | Customer portal: document mgmt, bot config, tenant users, test chat |
| `api-admin` | .NET ASP.NET Core | Internal super-admin: all tenants, billing, infra health |
| `worker` | .NET + Hangfire | Job scheduler — enqueues and retries jobs, calls `rag-core` Python endpoints for actual execution (ingestion, eval runs, scheduled tasks) |
| `portal-web` | React + TypeScript + Zustand + shadcn/ui + Tailwind + Axios | Customer portal SPA |
| `admin-web` | React + TypeScript + Zustand + shadcn/ui + Tailwind + Axios | Internal admin SPA |
| `widget` | React + TypeScript + AI SDK (ai-sdk.dev) — bundled as UMD | Embeddable chat widget with streaming AI chat components |

### Data stores

| Store | Purpose |
|-------|---------|
| PostgreSQL 16 | Tenants, users, configs, documents registry, billing events, API keys, chat sessions; **also Hangfire job store** |
| Qdrant | Per-tenant vector collections (one collection per tenant → clean isolation, easy delete) |
| Redis | JWT blocklist, rate limit counters, SSE session state |

### Frontend stack (all SPAs and widget)
| Concern | Library |
|---------|---------|
| Framework | React + TypeScript |
| State management | Zustand |
| HTTP client | Axios |
| UI components | shadcn/ui |
| Styling | Tailwind CSS |
| Charts | Recharts |
| Build | Vite |
| Chat UI (widget + test-chat) | **AI SDK** (elements.ai-sdk.dev) — streaming chat components |

### Infrastructure / DevOps
- Docker Compose (current) → production: Kubernetes or Docker Swarm
- GitHub Actions for CI/CD
- Prometheus + Grafana for metrics
- OpenTelemetry (traces → Tempo), Loki (logs, Grafana stack)

### AI-assisted development workflow
- **GitHub Spec Kit** — each feature is authored as a structured spec (Markdown) before implementation
- **Spec merging skill** — a custom Claude Code skill (`/merge-spec`) merges individual feature specs into the canonical `docs/project-spec.md`, which serves as the single source of truth for AI-assisted code generation
- Workflow:
  1. Write `specs/feature-name.md` describing the feature (endpoints, data model, behaviour, test cases)
  2. Run `/merge-spec specs/feature-name.md` → spec is normalised and merged into `docs/project-spec.md`
  3. AI generates implementation guided by the full project spec
  4. PR references the spec section; spec updated when behaviour changes

---

---

## Phase Summary

| Phase | Focus | Duration estimate | Key output |
|-------|-------|-------------------|------------|
| 0 | Multi-tenancy foundation + .NET scaffolding | 2–3 weeks | PG schema, tenant isolation, auth |
| 1 | Customer portal API | 3–4 weeks | Full self-service CRUD + async ingestion |
| 2 | Chat widget + public API | 2–3 weeks | Embeddable widget, SSE streaming |
| 3 | Internal admin API + SPA | 3–4 weeks | Full admin visibility |
| 4 | Customer portal frontend | 3–5 weeks | End-to-end SPA with Playwright .NET e2e |
| 5 | Observability + production hardening | 2–3 weeks | Monitored, load-tested, secured |
| **6** | **Evals & quality infrastructure** | **3–4 weeks** | **RAGAS metrics, CI gate, admin dashboard, feedback loop** |
| **7** | **Procedures engine** | **5–7 weeks** | **Step editor, tool connectors, simulations, trigger classifier** |
| **7b** | **Visual dialog flow editor** | **2–3 weeks** | **React Flow canvas, simulation replay on canvas, list ↔ canvas toggle** |
| 8 | Retrieval quality + advanced features | Ongoing | Hybrid search, reranker, multilingual, white-label, mobile SDK |
| **9** | **Billing & Monetization** | **2–3 weeks** | **Stripe subscriptions, plan enforcement, quota alerts, tenant billing portal** |
| **10** | **Integrations & Ecosystem** | **3–4 weeks** | **Webhooks, Zapier, Zendesk/HubSpot/Slack native integrations, developer API hub** |

**Sequencing notes:**
- Phases 0→1→2 strictly sequential
- Phase 3 can start in parallel with Phase 2
- Phase 4 can start when Phase 1 API is stable
- **Eval v1** starts during Phase 1 (first working query) — no need to wait for Phase 6
- **Eval v2 CI gate** is in place by Phase 2 (before widget ships to real users)
- Phase 7 (Procedures) requires Phase 0 + Phase 1 API + Phase 4 portal SPA
- Phase 5 hardening is a formal gate before any external clients onboard
- Phase 9 (Billing) can start in parallel with Phase 8 once the first paying clients are live (after Phase 2)
- Phase 10 (Integrations) follows Phase 9; integration marketplace requires billing plan enforcement to be in place
