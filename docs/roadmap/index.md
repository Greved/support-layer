# SupportLayer — Roadmap Index

Multi-tenant SaaS RAG platform: each client gets an isolated knowledge base, a configurable
chat bot, and an embeddable widget. Central admin panel, billing, and full infrastructure control.

---

## Navigation

### Foundation
| File | Contents |
|------|----------|
| [Overview & Architecture](overview.md) | Vision, current state, target architecture, tech stack, phase summary |
| [Cross-Cutting Concerns & Analytics](cross-cutting.md) | Multi-tenancy rules, testing strategy, API versioning, secrets, PostHog events schema |

### Phases
| Phase | File | Focus |
|-------|------|-------|
| 0 | [phase-0.md](phase-0.md) | Multi-tenancy foundation, PG schema, .NET scaffolding, JWT auth |
| 1 | [phase-1.md](phase-1.md) | Customer portal API — document management, bot config, API keys |
| 2 | [phase-2.md](phase-2.md) | Public chat API + embeddable JS widget, SSE streaming |
| 3 | [phase-3.md](phase-3.md) | Internal admin API + SPA, tenant management, audit logs |
| 4 | [phase-4.md](phase-4.md) | Customer portal frontend SPA, onboarding wizard, MFA |
| 5 | [phase-5.md](phase-5.md) | Observability, security hardening, load testing, CD pipeline |
| 6 | [phase-6.md](phase-6.md) | Evals & quality infrastructure — RAGAS, DeepEval, CI gate, feedback loop |
| 7 | [phase-7.md](phase-7.md) | Procedures engine — trigger classifier, executor, tool connectors, simulations |
| 7b | [phase-7b.md](phase-7b.md) | Visual dialog flow editor — React Flow canvas, simulation replay |
| 8 | [phase-8.md](phase-8.md) | Retrieval quality & advanced features — hybrid search, multilingual, mobile SDK |
| 9 | [phase-9.md](phase-9.md) | Billing & monetization — Stripe, plan enforcement, quota alerts |
| 10 | [phase-10.md](phase-10.md) | Integrations & ecosystem — webhooks, Zapier, Zendesk/HubSpot/Slack, developer hub |

### Business
| File | Contents |
|------|----------|
| [Business Model & Revenue Projections](business-model.md) | Pricing tiers, revenue scenarios (conservative/base/optimistic), infra costs, solopreneur capacity |
| [Competitor Analysis](competitors.md) | Intercom Fin, Chatbase, Botpress, Dify, Voiceflow, Inkeep, Relevance AI — pros/cons + positioning matrix |

---

## Current State

| Item | Status |
|------|--------|
| Core RAG pipeline (embed → search → generate) | ✅ Working |
| Qdrant vector store + ingestion CLI | ✅ Working |
| llama.cpp (CPU/GGUF) + vLLM (GPU) support | ✅ Working |
| Gemini / LM Studio fallback | ✅ Working |
| Multi-tenancy | ❌ None — single shared collection |
| Auth | ❌ None |
| PostgreSQL | ❌ Not integrated |
| Customer portal | ❌ None |
| Internal admin | ❌ None |
| Chat widget | ❌ None |
| Billing/usage tracking | ❌ None |
| Observability | ⚠️ Basic logging only |
| Tests | ⚠️ Skeleton only |

**Next step:** [Phase 0 — Multi-tenancy Foundation](phase-0.md)
