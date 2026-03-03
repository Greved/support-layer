[← Index](index.md) · [← Phase 3](phase-3.md) · [Phase 5 →](phase-5.md)

---

## Phase 4 — Customer Portal Frontend
**Goal:** Clients have a polished self-service web UI, not just an API.

### Deliverables
- React + TypeScript customer portal SPA
- Document management UI (drag-and-drop upload, status polling, delete)
- Bot configuration form (system prompt, model, temperature, widget appearance)
- Live test-chat panel (mirrors the real widget experience)
- API key management UI
- Team management (invite by email, set role, revoke)
- Basic usage dashboard (queries this month, doc count, plan limits)

### Tasks
- [ ] Portal SPA scaffold: Vite + React + TypeScript + Tailwind + shadcn/ui + Zustand + Axios
- [ ] Auth flow: login, refresh, logout, protected routes
- [ ] Documents page: upload dropzone, status badge with polling, delete confirm
- [ ] Config page: form with live preview of widget appearance
- [ ] Test chat: embedded widget (AI SDK chat component) in sandboxed iframe using current config
- [ ] API Keys page: create with label, copy-on-create, revoke
- [ ] Team page: invite form, role selector, member list
- [ ] Usage dashboard: Recharts for charts, Zustand for state, Axios for API calls
- [ ] Onboarding checklist/wizard: first-run guide for new tenants (step 1: upload a document → step 2: configure bot → step 3: copy widget embed code → step 4: send a test message); persisted completion state per tenant
- [ ] Password reset flow: "Forgot password" → time-limited signed email link → reset form
- [ ] 2FA/MFA: optional TOTP authenticator app enrollment (QR code + backup codes); enforceable for `tenant_admin` role on enterprise plan
- [ ] Notification preferences page: email on ingestion complete/error, quota warning at 80% and 100% of plan limit

### Tests (Phase 4)
- **Unit (Vitest):** Form validation, status badge logic, polling hook
- **Component (React Testing Library):** Upload dropzone, config form, chat panel
- **E2E (Playwright .NET):**
  - New tenant: register → upload PDF → wait for ready → test chat → see answer
  - Config change (system prompt) → reflected in next test-chat response
  - Invite team member → member logs in → sees documents (not config, by role)
  - API key create → copy → revoke → request with revoked key fails
- **Visual regression (Playwright .NET screenshots):** Key pages across light/dark mode

### Quality Gate ✅
- Full new-tenant onboarding flow works end-to-end in Playwright .NET
- Upload → ingest → query cycle completes under 60s for a <5MB PDF
- All Playwright .NET e2e tests pass in CI
- Lighthouse score ≥ 85 (performance) on portal pages

---
