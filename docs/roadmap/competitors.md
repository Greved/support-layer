[← Index](index.md)

---

## Competitor Analysis

> Reference for product positioning. Updated March 2026.

### Market context

RAG-based architectures now power the majority of AI support tools. The market has consolidated into:
1. **Full helpdesk suites with AI bolted on** (Intercom Fin)
2. **No-code chatbot builders with RAG** (Chatbase, Botpress)
3. **Developer-first platforms** (Dify, Inkeep)
4. **Conversation-design tools** (Voiceflow)
5. **General-purpose agent builders** (Relevance AI)

None cleanly solve the full stack: isolated multi-tenant vector stores + embeddable widget + configurable RAG pipeline + procedures engine + admin portal, as a single focused SaaS.

---

### 1. Intercom Fin

**Focus:** AI agent on top of Intercom's helpdesk (inbox, help center, tickets). Fin 3 (2025) added Procedures — structured multi-step workflows with deterministic control flow, approval checkpoints, and third-party API calls via Data Connectors.

**Pricing:** $29–$139/seat/month + **$0.99 per AI resolution**. Common spend: $10k–$20k+/month for large teams.

| Pros | Cons |
|------|------|
| Most mature Procedures engine on the market | Severe vendor lock-in — Fin only works inside Intercom |
| Deep helpdesk integration (inbox, tickets, CSAT, routing) | Per-resolution pricing unpredictable at scale |
| Omnichannel: web, email, WhatsApp, SMS, phone | No multi-tenant reseller model |
| SOC 2 Type II, GDPR, EU hosting | No white-labeling at any price |
| Resolution-based pricing rewards AI deflection | RAG locked to Intercom knowledge base — no custom vector store |
| Strong analytics | US CLOUD Act risk for EU businesses |

**Our edge:** Per-tenant isolated vector stores, open-source LLM support, white-label from day one, predictable flat-rate pricing, full data portability.

---

### 2. Chatbase

**Focus:** No-code RAG chatbot builder. Upload docs → embeddable widget in 15 min. Bootstrapped to $8M ARR. Dominant in the "one business, one FAQ bot" segment.

**Pricing:** $19–$399/month base + $199/mo branding removal + $199/mo custom domain. Credit-based billing.

| Pros | Cons |
|------|------|
| Fastest time-to-value (10–15 min setup) | No procedures/workflows engine — pure Q&A only |
| Multi-channel: web, WhatsApp, Messenger, Instagram, Slack | White-label costs $398+/mo on top of base plan |
| Multiple LLM support (GPT-4, Claude, Gemini) | No multi-tenant architecture |
| Proven product-market fit | No live chat / human handoff inbox |
| AI Actions: integrate Stripe, Calendly, Zendesk | No SSE streaming — responses render as a block |
| | Credit shock billing; character-capped knowledge base per plan |

**Our edge:** Multi-tenant architecture, procedures engine, SSE streaming, predictable pricing, RBAC, no branding tax.

---

### 3. Botpress

**Focus:** Developer-oriented conversational AI. Visual flow builder + LLMz inference engine + RAG. Raised $25M Series B (June 2025, ~$40M total).

**Pricing:** Free + AI usage / Plus $89/mo + AI usage / Team $495/mo + AI usage / Enterprise custom.

| Pros | Cons |
|------|------|
| Highly configurable — JavaScript code nodes at every step | Steep learning curve |
| Bring-your-own LLM key (no token markup) | Unpredictable AI Spend variable makes budgeting hard |
| Open-source heritage, active community | No admin portal for end-customers |
| Multi-channel; human handoff built in | Limited analytics below enterprise tier |
| Tables: structured data store inside the platform | Complex to build multi-tenant white-label deployments on top |
| Enterprise: RBAC, private cloud, audit logs, SOC 2 | No native SSE streaming on embeddable widget |

**Our edge:** Turnkey multi-tenant product, predictable per-tenant pricing, native SSE widget, admin portal out of the box.

---

### 4. Dify.ai

**Focus:** Open-source LLM application platform. RAG pipelines, agentic workflows, model management, observability. 60k+ GitHub stars. A development platform, not a finished product.

**Pricing:** Self-hosted free (Apache 2.0). Cloud Team ~$159/mo. Enterprise custom.

| Pros | Cons |
|------|------|
| Broadest model support (GPT, Claude, Gemini, Mistral, Llama, any OpenAI-compatible) | Not a finished multi-tenant product |
| Excellent RAG: PDF/DOCX/web crawl, hybrid search, reranking | No embeddable JS widget |
| Visual workflow builder (drag-and-drop, conditions, loops) | No admin portal for end-customers |
| Observability built in; supports Qdrant, Pinecone, Weaviate, Milvus | No domain-specific procedures engine for support logic |
| Open source + self-hostable — full data sovereignty | Self-hosting requires real DevOps capability |

**Our edge:** Turnkey multi-tenant SaaS with polished widget + admin portal; no infrastructure management; domain-specific procedures engine; managed cloud with predictable pricing.

---

### 5. Voiceflow

**Focus:** Visual conversation design platform for chat and voice AI agents. Best-in-class canvas. Targets PMs, conversation designers, agencies. Raised $50M+.

**Pricing:** Pro $60/month/editor / Business $150/month/editor / Enterprise custom.

| Pros | Cons |
|------|------|
| Best-in-class visual flow designer | No native live agent handoff in chat |
| Multi-agent architecture; voice + chat in one platform | Design tool only — not a customer support SaaS |
| Multi-channel: web, WhatsApp, SMS, Twilio telephony | No admin portal for end-customers |
| Strong agency / client management features | Per-editor seat pricing expensive for teams |
| Good for prototyping and stakeholder collaboration | No SSE streaming; white-label enterprise-only |
| | Big gap between prototype and production deployment |

**Our edge:** Full support SaaS stack, multi-tenant product model, SSE streaming, white-label from day one, predictable per-tenant vs per-editor pricing.

---

### 6. Inkeep / Mendable

**Focus:** Developer-focused AI platforms for DevTool companies, open-source projects, and technical docs. "Ask AI" search and RAG Q&A over docs, GitHub, Notion, Discord.

**Pricing:** Contact-sales only (opaque tiers).

| Pros | Cons |
|------|------|
| Best-in-class for developer documentation RAG | Narrow vertical — not suited for general customer support |
| Source attribution with citations to exact doc sections | No procedures/workflows builder |
| No-code + TypeScript SDK with two-way sync | No multi-tenant reseller platform |
| Enterprise security: PII removal, RBAC, traceability, MCP support | Contact-sales only — self-serve barrier |
| Multi-surface: UI component, REST API, Slack, Discord, Zendesk | Limited embeddable widget customization |

**Our edge:** Serves the much larger non-developer market, multi-tenant model, procedures engine, transparent self-serve pricing.

---

### 7. Relevance AI

**Focus:** General-purpose AI "workforce" platform for internal automation (sales, ops, marketing). Multi-agent orchestration, 9,000+ integrations. Not primarily a customer-facing support product.

**Pricing:** Free / Pro from $19/mo / Team $99–$349+/mo / Enterprise custom. Split into Actions credits + Vendor Credits (LLM pass-through at cost) since Sept 2025.

| Pros | Cons |
|------|------|
| Multi-agent "workforce" model | Not a customer support product — significant custom build required |
| 9,000+ integrations; bring-your-own LLM key (zero markup) | No native embeddable JS widget |
| RBAC and SSO built in | No admin portal for end-customers |
| Flexible: bots, internal copilots, background automation | Primarily outbound sales automation, not inbound support |
| | Predicting LLM-heavy task costs is difficult at scale |

**Our edge:** Turn-key customer support focus with widget + knowledge base portal + procedures engine; multi-tenant model; predictable flat-rate pricing; simple self-serve onboarding.

---

### Competitive positioning summary

| Capability | Intercom Fin | Chatbase | Botpress | Dify.ai | Voiceflow | Inkeep | Relevance AI | **SupportLayer** |
|---|---|---|---|---|---|---|---|---|
| Per-tenant isolated vector store | No | No | No | No (self-host) | No | No | No | **Yes** |
| Embeddable JS widget | Yes | Yes | Yes | No | Yes | Yes | No | **Yes** |
| SSE streaming on widget | Partial | No | Limited | API only | No | Partial | No | **Yes** |
| Admin portal per tenant | No | No | No | No | No | No | No | **Yes** |
| Procedures / workflows engine | Excellent | None | Moderate | Moderate | Strong (flow) | None | Moderate | **Yes (Phase 7)** |
| Human handoff | Yes | 3rd party | Yes | No | Limited | No | No | **Yes (Phase 8)** |
| White-label | No | $398+/mo | Enterprise only | Self-host | Enterprise only | No | No | **Yes (plan-gated)** |
| Open / self-hosted LLM | No | No | Yes | Yes | No | Limited | Yes | **Yes** |
| Self-hostable | No | No | Partial | Yes | No | No | No | **Yes** |
| Predictable flat-rate pricing | No | No | No | Self-host | Moderate | No | No | **Yes** |
| BYOK (LLM key) | No | No | Yes | Yes | No | No | Yes | **Yes** |
