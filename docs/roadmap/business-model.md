[← Index](index.md)

---

## Business Model & Revenue Projections

> Context: solo founder building this part-time alongside a day job at an IT company.
> IT company is the first target customer (internal support bot use case).

---

### Pricing tiers

| | **Hobby** | **Starter** | **Pro** | **Business** | **Enterprise** |
|---|---|---|---|---|---|
| **Monthly** | $19/mo | $29/mo | $89/mo | $249/mo | from $599/mo |
| **Annual** | $190/yr | $290/yr | $890/yr | $2,490/yr | custom (annual only) |
| **Annual savings** | $38 (17%) | $58 (17%) | $178 (17%) | $498 (17%) | — |
| Workspaces | 1 | 1 | 1 | 3 | unlimited |
| Documents | 25 | 100 | 500 | 2,000/workspace | unlimited |
| Max file size | 5 MB | 10 MB | 50 MB | 100 MB | custom |
| Queries/month | 500 | 2,000 | 10,000 | 50,000 (pooled) | unlimited |
| Team members | 1 | 3 | 10 | unlimited | unlimited |
| Widget branding | "Powered by SupportLayer" | "Powered by SupportLayer" | Unbranded | White-label (custom logo) | White-label |
| Procedures | — | — | 10 active | unlimited | unlimited |
| Visual flow editor | — | — | ✓ | ✓ | ✓ |
| Evals dashboard | — | basic (read-only) | full | full + CI gate | full + CI gate |
| A/B testing | — | — | — | ✓ | ✓ |
| Webhooks | — | — | — | ✓ | ✓ |
| Slack / Teams integration | — | — | — | ✓ | ✓ |
| SSO (OIDC) | — | — | — | — | ✓ |
| On-premise deployment | — | — | — | — | ✓ |
| SLA | — | — | — | — | 99.9% uptime |
| Support | Community | Email 48h | Email 24h | Email + Slack | Dedicated Slack |

**Upgrade path:** Hobby → Starter (hits doc/query limits or needs email support) → Pro (needs Procedures or unbranded widget) → Business (multiple workspaces, white-label, team > 10) → Enterprise (OIDC/SLA/on-premise).

---

### Pricing rationale

| Tier | vs Competitor | Primary upgrade trigger |
|------|--------------|------------------------|
| **Hobby $19** | Same price as Chatbase Hobby, fewer raw limits, no Procedures | Low-friction entry; tight limits drive Starter upgrades |
| **Starter $29** | Below Chatbase Standard ($49+) | Procedures absent — that absence is the pressure valve to Pro |
| **Pro $89** | Matches Botpress Plus ($89) | Procedures + visual flow editor unlock — core differentiator over Chatbase |
| **Business $249** | Half of Botpress Team ($495) | 3 workspaces targets agencies / multi-product companies; white-label seals it |
| **Enterprise $599+** | Far below Intercom ($1.5k–$20k+/mo) | OIDC, SLA, on-premise; annual contract only |

**LLM strategy: BYOK (Bring Your Own Key) as default.** Customers connect their own OpenAI/Anthropic/Gemini key or their own LLM server. Gross margin ~85%. Offer managed LLM only as an Enterprise add-on.

---

### Your IT company: first customer strategy

| | |
|---|---|
| Use case | Internal IT helpdesk bot for employees (password reset, VPN, access requests, common troubleshooting) |
| Recommended plan | **Business** ($249/mo or $2,490/yr) — 3 workspaces (IT, HR, Dev tools), no external branding |
| Immediate ARR | **$2,490/year from day one** |
| Expected query volume | 500–2,000 queries/month for a 50–200 person company → well within Business limits |
| Strategic value | Real production load → real bugs → faster iteration; reference customer + case study for sales |
| Suggested deal | Standard rate; in exchange: permission to use as public case study and reference for prospects |

Even if internal budget approval is slow, use it at cost internally — the feedback loop is worth more than $249/mo at this stage.

---

### Market benchmarks (research-backed)

| Metric | Benchmark | Source |
|--------|-----------|--------|
| Chatbase blended ARPU | ~$67/month ($8M ARR, 10k+ customers) | eesel.ai analysis 2025 |
| B2B SaaS trial → paid conversion (median) | 18.5%; top quartile: 35–45% | First Page Sage 2025 |
| SMB SaaS monthly churn target | 3–5%/month | Recurly / ChartMogul 2025 |
| Bootstrapped SaaS MoM customer growth (sub-$1M ARR) | ~4–5% MoM (≈50% annually) | SaaS Capital 2025 |
| AI chatbot market CAGR (2025–2031) | 23.15% | Mordor Intelligence |
| SMBs actively using AI chatbots (2025) | 46% of AI-using SMBs | USM Systems |
| Time to $10K MRR (realistic solo founder) | 14–24 months post-launch | ChartMogul / Indie Hackers |
| Time to $50K MRR (realistic solo founder) | 30–48 months post-launch | Bannerbear / ScrapingBee case studies |

---

### Revenue scenarios

> **T=0 = March 2026 (today). MVP (Phase 0–2) estimated at T+4–5 months part-time.**
> **First paying customers at ~T+5–6 months.**
>
> Model inputs: 3.5% monthly churn (B2B SaaS median), ~4% MoM net customer growth post-launch.
> Blended ARPU starts lower (~$50–65) because Hobby/Starter dominate early sign-ups, then rises
> as Procedures drives Pro/Business upgrades. Estimated tier mix at T+6: 35% Hobby / 30% Starter /
> 25% Pro / 8% Business / 2% Enterprise → blended ~$58. At T+36: 15% / 20% / 30% / 25% / 10%
> → blended ~$120.

| | | **T+6mo** | **T+12mo** | **T+24mo** | **T+36mo** |
|---|---|---|---|---|---|
| **Conservative** | Customers | 8 | 24 | 75 | 170 |
| *(part-time, slow organic)* | Blended ARPU | $50 | $65 | $82 | $100 |
| | MRR | $400 | $1,560 | $6,150 | $17,000 |
| | ARR | **$4,800** | **$18,720** | **$73,800** | **$204,000** |
| **Base** | Customers | 15 | 48 | 150 | 340 |
| *(Product Hunt + active content)* | Blended ARPU | $58 | $75 | $96 | $118 |
| | MRR | $870 | $3,600 | $14,400 | $40,120 |
| | ARR | **$10,440** | **$43,200** | **$172,800** | **$481,440** |
| **Optimistic** | Customers | 35 | 105 | 350 | 740 |
| *(viral moment or prior audience)* | Blended ARPU | $65 | $85 | $110 | $135 |
| | MRR | $2,275 | $8,925 | $38,500 | $99,900 |
| | ARR | **$27,300** | **$107,100** | **$462,000** | **$1,198,800** |

> **Effect of adding Hobby tier:** More sign-ups at lower ARPU; net MRR is roughly the same as
> without it. The real upside is a larger base to convert up the tier ladder — every 10 Hobby
> customers who hit their 25-doc limit and upgrade to Pro adds ~$700/mo incremental MRR.

**Key inflection points:**

| MRR Milestone | What it means | Base ETA | Optimistic ETA |
|---|---|---|---|
| **$300/mo** | Infra costs covered | T+6mo | T+4mo |
| **$3K/mo** | Product fully self-sustaining | T+11mo | T+7mo |
| **$10K/mo** | Viable to reduce day-job hours; first contractor feasible | T+23mo | T+13mo |
| **$20K/mo** | Full-time focus justified | T+29mo | T+17mo |
| **$50K/mo** | First engineering hire warranted | T+41mo | T+27mo |

---

### Infrastructure cost estimate

| Customers | Monthly infra | Composition |
|-----------|--------------|-------------|
| 0–20 | $100–200 | Qdrant Cloud starter, managed PostgreSQL, 1 app server, monitoring |
| 20–80 | $250–550 | Scaled Qdrant, Redis, CDN, email volume |
| 80–250 | $600–1,500 | Multiple app replicas, larger PG, structured logging |
| 250–600 | $1,800–4,500 | Load balancing, Qdrant cluster, full observability stack |

**Gross margin with BYOK:** ~85%. **With managed LLM included:** ~65–72%.

---

### Solopreneur capacity

| Customer count | Weekly overhead | Recommended action |
|---|---|---|
| 0–50 | 3–5h/week | Evenings + weekends; manageable alongside day job |
| 50–150 | 8–12h/week | Invest in self-service docs and in-app guidance |
| 150–300 | 20+h/week | Full-time or part-time customer success VA |
| 300+ | Beyond solo capacity | First engineering hire required |

**Levers that extend solo capacity:** self-service onboarding wizard (Phase 4), in-app guidance, docs hub (Phase 10), Procedure simulation (customers debug their own bots).

---

### Year 1 services income (cash flow bridge)

| Service | Price | Time |
|---------|-------|------|
| Setup / onboarding session | $500–1,500 one-time | 2–3h |
| Procedure design workshop | $200–500 | 2h |
| Custom tool connector | $1,000–3,000 | 1–3 days |

Realistic year 1 services income: **$3,000–$12,000**. Phase out as recurring revenue crosses $10K MRR.
