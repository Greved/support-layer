[← Index](index.md) · [← Phase 4](phase-4.md) · [Phase 6 →](phase-6.md)

---

## Phase 5 — Observability, Security & Production Hardening
**Goal:** Platform is ready for real clients: monitored, secured, load-tested.

### Observability stack
- **Metrics:** Prometheus scraping all services; Grafana dashboards for RAG latency, token throughput, error rates, queue depth, Qdrant collection sizes
- **Traces:** OpenTelemetry in Python RAG service + .NET services → Tempo or Jaeger
- **Logs:** Structured JSON logs from all services → **Loki** (Grafana stack); query logs tagged with `tenant_id`, `request_id`; Grafana dashboards query Loki directly
- **Alerts:** Grafana alerting for p95 latency > threshold, error rate spike, queue depth > N, disk usage > 80%

### Security hardening
- [ ] Input validation and max-length limits on all user-supplied fields
- [ ] Prompt injection detection (block/flag queries that attempt to override system prompt)
- [ ] File upload: MIME-type validation, virus scan (ClamAV), size limit per plan
- [ ] API key rotation policy, expiry enforcement
- [ ] OWASP top-10 review of all .NET API endpoints
- [ ] Rate limiting at nginx/gateway level in addition to application level
- [ ] Secrets management: Docker secrets or Vault (no plaintext env vars in prod)
- [ ] HTTPS everywhere, HSTS, CSP headers on frontends
- [ ] Automated database backups: nightly `pg_dump` to S3-compatible storage, 30-day retention, restore test in CI weekly
- [ ] CDN distribution for `widget.js`: cache-busted on each release, served from edge nodes (CloudFront, Bunny CDN, or similar)
- [ ] Pre-launch penetration test: structured manual pentest (external vendor or OWASP Testing Guide self-checklist) before onboarding first external client

### CI/CD pipeline (GitHub Actions)
```
PR:
  lint → unit tests → integration tests → build images → e2e tests (docker-compose)

Main merge:
  + push images to registry → deploy to staging → smoke tests → manual gate → deploy to prod
```

### Load testing (k6)
- Scenario 1: 100 concurrent widget chat sessions, 5-min duration
- Scenario 2: 10 tenants each uploading 50-page PDF simultaneously
- Scenario 3: Admin dashboard with 20 concurrent super-admin users
- Acceptance: p95 chat latency < 8s, ingestion throughput ≥ 5 docs/min, zero 5xx errors

### Tasks
- [ ] Add Prometheus metrics to Python RAG service (request count, latency histogram, token count)
- [ ] Add OpenTelemetry SDK to all services
- [ ] Grafana dashboards: RAG pipeline, API gateway, infra health
- [ ] Configure alerting rules
- [ ] Write and run k6 load scenarios
- [ ] Security review checklist executed
- [ ] Staging environment on a real server/VM with prod-like config

### Tests (Phase 5)
- **Load (k6):** All scenarios at acceptance thresholds
- **Security (OWASP ZAP):** Automated scan against staging
- **Chaos:** Qdrant restart mid-query, LLM server crash → graceful degradation
- **Smoke (post-deploy):** 5-minute automated smoke test suite on every deploy

### Quality Gate ✅
- All k6 load scenarios pass acceptance thresholds
- Zero P0/P1 OWASP ZAP findings
- Grafana dashboards operational with at least 48h of data from staging
- Zero secrets in environment variables (all via secrets management)
- Chaos tests: errors are logged and returned gracefully (no 500 leaking stack traces)

---
