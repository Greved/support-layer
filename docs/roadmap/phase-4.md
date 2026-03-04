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

### Frontend Design
> References: `docs/references/design/stitch_stark_fintech_prd/`
> `portal_dashboard_stark_blue_theme/` · `documents_manager_stark_refined/` · `configuration_preview/` · `team_api_management_stark_refined/`

**Design system — Portal SPA:**
- Light theme: white page background; light-gray sidebar (`≈#f8fafc`); blue primary accent (`≈#2563eb`)
- Left sidebar: logo + wordmark top-left; icon + label navigation — Dashboard · Procedures · Configuration · Quality · Docs; SYSTEM section — Settings · API Keys; user avatar + name + company slug bottom-left
- Top bar: `● System Operational` green status badge left; context-dependent primary action button top-right (e.g. "+ New Procedure")

**Portal Dashboard:**
- 4 KPI cards: Total Conversations · Msg/User · Resolution Rate · Avg Sentiment — each with a ± delta trend badge (green/red arrow + %)
- Token Usage area chart with time range toggle pills: 1H · 24H · 7D
- Top Intents horizontal bar chart: intent label + percentage + blue progress bar
- LIVE FEED right panel: scrollable timestamped activity stream — conversation starts, handoffs, hallucination flags, procedure updates, plan changes; hallucination-flagged items show a blue "Inspect Trace" link

**Knowledge Base (Documents) page:**
- Title "KNOWLEDGE BASE"; tabs: All · Processing · Completed · Errors; upload button top-right
- Table columns: NAME · STATUS · CHUNKS · SIZE · UPLOADED · ACTIONS
- Status badges (pill): Ready = solid green · Processing = blue · Error = red · Queued = gray

**Configuration page:**
- Horizontal tab strip at top of content area: Behavior · Retrieval · Appearance (not sidebar tabs)
- Behavior tab: system prompt textarea + temperature slider + max tokens input
- Live chat preview panel fixed to the right side — mirrors widget appearance in real time as config changes
- Retrieval trace panel (bottom-right): shows last query's retrieval chain — query → retrieved docs → LLM reasoning → answer

**Team & API page (single page, two sections):**
- Section 1 — ACTIVE MEMBERS: count in heading; "+ Invite Member" button top-right; table: avatar · name/email · ROLE badge (ADMIN = dark filled, MEMBER = outlined) · JOINED date · trash icon
- Section 2 — API KEYS: subtitle description; "+ Create New Key" button top-right; table: LABEL · masked API key with clipboard copy icon · SCOPES badges (READ · WRITE · FULL ACCESS) · ACTIONS (gear + disable icon)

**Login / Sign-in page:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/portal_login_stark_black/`
- Full-page centered layout, light gray background (`≈#f5f5f5`)
- Logo + wordmark top-left; "SUPPORT" outlined button top-right
- Product name "BOTPLATFORM" (or platform name) as large bold all-caps heading; short blue decorative underline bar
- Centered card with thin border: "SIGN IN" heading; EMAIL ADDRESS label + input; PASSWORD label + "FORGOT PASSWORD?" link right-aligned + password input with show/hide eye toggle
- "ENTER PLATFORM" full-width dark navy button (very bold, all-caps)
- "NEW TO STARK? REQUEST ACCESS" text link below button
- Footer: Privacy Policy · Terms of Service · Security links left; copyright right

**Password Reset page:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/password_reset_stark_black/`
- Identical full-page layout and product name heading as login page
- Card: "RESET PASSWORD" heading + instruction text; EMAIL ADDRESS input with placeholder `name@company.com`; "SEND RESET LINK" full-width dark button
- "REMEMBER YOUR PASSWORD? LOG IN" text link below

**Onboarding Wizard:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/onboarding_wizard_stark/`
- Left sidebar lists wizard steps as navigation: Documents · Bot Configuration · Embed Code · Test Message (active step highlighted blue, inactive steps grayed out); Support link at bottom
- Progress: "Setup your AI Assistant" heading + "Step N of 4: {Step Name}" subtitle; horizontal 4-segment progress bar (filled blue = completed/active, gray = upcoming)
- 4 step icons in a row below progress bar: Upload · Configure · Embed · Test (active icon = filled blue square, inactive = gray outlined)
- Step 1 — Upload: "Upload Your First Document" heading; large dashed-border drag-and-drop zone (cloud upload icon + "Drag and drop your file here" + accepted formats + OR + "Browse Local Files" outlined button); "RECENT UPLOADS" section with empty state
- Footer: "Back" (disabled on step 1) · "Skip for now" · "Continue to Step 2" primary button

**Settings — General page:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/portal_settings_stark/`
- Settings sub-navigation sidebar: "Portal Settings / Manage your workspace" header; items: General · Security · Notifications · API Keys · Billing
- Breadcrumb: Settings › General; search input + bell icon in top bar
- Title: "General Settings" + description + "Save Changes" button top-right
- Content split into cards: Profile Settings (logo upload widget with initials fallback + "Upload New" / "Remove" buttons) · Organization Details (name input + language dropdown + timezone dropdown) · Regional Preferences (toggle rows: Automatic Currency Conversion, Metric System, etc.)

**Settings — Notification Preferences page:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/notification_preferences_stark/`
- Same settings sidebar layout, Notifications item active
- Breadcrumb: SETTINGS › NOTIFICATION PREFERENCES
- Title + description
- Two-column toggle table: Notification Type (label + description) / Email toggle / In-App toggle
- Rows: Ingestion Complete · Ingestion Error · Quota at 80% · Quota at 100%
- Toggle switches: blue = enabled, gray = disabled; each row is independent per channel
- "Discard changes" secondary + "Save Changes" primary buttons bottom-right

**MFA Enrollment page:**
> Reference: `docs/references/design/stitch_stark_fintech_prd/mfa_enrollment_stark_light_backup_codes/`
- Full-page layout, light gray background; logo top-left; "Security Settings" link top-right
- Title: "MFA Enrollment" + subtitle
- Two side-by-side cards:
  - Card ① "Scan QR Code": QR code image centered in a white inset box; "SECRET KEY" label + monospace key string below QR
  - Card ② "Verify Code": instruction text; 6 individual single-digit input boxes (OTP style) in a row; "✓ Verify & Enable" full-width blue button; "Cancel enrollment" text link
- Dark navy panel below: "🔑 Backup Codes" heading + description + "↓ Download Codes" button; 10 backup codes displayed in 2×5 grid (white bordered boxes, monospace font); blue info bar warning codes are shown once
- Footer copyright line

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
