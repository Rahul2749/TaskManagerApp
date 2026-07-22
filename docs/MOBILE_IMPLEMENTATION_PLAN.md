# TaskManager Mobile — Implementation Plan (aligned to Web SaaS)

Companion to [`SAAS_IMPLEMENTATION_PLAN.md`](./SAAS_IMPLEMENTATION_PLAN.md) and [`IMPLEMENTATION_STATUS.md`](./IMPLEMENTATION_STATUS.md).  
**Client:** .NET MAUI (`src/TaskManager/TaskManager.Mobile`) · **Backend:** same ASP.NET Core API as Blazor web.

> The original SaaS plan left mobile out of scope while web caught up. This document is the **mobile catch-up + parity roadmap** against what web has already shipped (Phases 0–5) and what comes next (6–9).

**Last updated:** 22 Jul 2026

---

## 0. Locked decisions (mobile)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Platform** | Stay on **.NET MAUI** | Same Shared DTOs, one team, Android + iOS (+ Windows/Mac Catalyst optional) |
| **API** | Consume existing REST (+ SignalR where useful) | No mobile-specific backend; extend Shared DTOs only when needed |
| **Billing UX** | **View + deep-link to web checkout** first; native Razorpay later | Checkout.js / hosted flows are awkward in WebView; web already works |
| **SuperAdmin** | **Out of scope** on mobile | Platform ops stay on web |
| **Marketing / pricing site** | **Out of scope** | Use web `/pricing`; optional in-app “Upgrade” → browser |
| **First mobile milestone** | **M0 fix + M1 auth/onboarding parity + M2 task depth** | Unblock org admins, point at live API, then match daily PM workflows |

---

## 1. Where mobile is today

### Strengths
- JWT login / logout / refresh via SecureStorage + `AuthenticatedHttpMessageHandler`
- Shell flyout: Dashboard, Tasks, Projects, Users, Profile
- Task CRUD + status update + history display
- Project / user CRUD (intended for Admin/Manager)
- Dashboard stats from `api/dashboard`
- References `TaskManager.Shared` for DTOs

### Critical defects (fix before features)
| Issue | Impact | Fix |
|-------|--------|-----|
| Role checks use `"Admin"` | Org admins (`OrganizationAdmin`) lose Projects/Users menus; user editor can save invalid role | Align to `OrganizationAdmin` / `Manager` / `User` / `SuperAdmin` everywhere |
| Default API URL is `http://taskboard.runasp.net/` | README documents localhost / Render; app may hit stale host | Debug: per-platform localhost; Release: production Render URL (`https://taskmanager-app-plt1.onrender.com/`) |
| Docs vs code drift | Confusing local runs | Update `README.md` + `USER_GUIDE.md` § mobile |

### Feature gap vs web (shipped Phases 0–5)

| Web (done) | Mobile |
|------------|--------|
| Register workspace, password reset, invites, onboarding | Missing |
| Org settings / branding / timezone | Missing |
| Billing page, entitlements, FeatureGate | Missing |
| Kanban, calendar, saved views, custom fields, templates | Missing |
| Comments / subtasks / tags / attachments / watchers UI | DTOs exist; UI missing |
| Notifications + SignalR, @mentions, activity feed | Missing |
| Project ↔ user mapping | Missing |
| SuperAdmin console | Intentionally skip |

---

## 2. Product principles for mobile

1. **Mobile-first workflows:** triage tasks, update status, comment/mention, see notifications — not recreate every web admin screen.
2. **Same entitlements:** call `api/billing/entitlements` (or equivalent) and hide/disable gated features; “Upgrade” opens web billing in browser.
3. **Offline-light:** optimistic UI optional later; v1 is online-only with clear error toasts.
4. **Push > polling:** after in-app notifications work, add FCM/APNs for mention/status (Phase M5+).
5. **One design language:** keep MAUI Shell; introduce consistent cards, empty states, skeletons (already started), and brand color from org settings when available.

---

## 3. Phased delivery (mobile milestones)

Phases are numbered **M0–M9** to mirror web **0–9**, but scope is mobile-appropriate. Each milestone should be shippable to TestFlight / Play internal track.

### M0 — Stabilize & align (3–5 days) ← **start here**

**Goal:** App works against current production API with correct roles.

| Task | Detail |
|------|--------|
| Fix role strings | `OrganizationAdmin` everywhere; map display label “Admin” only in UI text |
| Fix user role picker | `User`, `Manager`, `OrganizationAdmin` (Managers invite Users only if API enforces) |
| API base URL | `#if DEBUG` platform defaults; Release → Render HTTPS; keep Preferences override |
| Cleartext / HTTPS | Prefer HTTPS to Render; restrict cleartext to debug emulators |
| Smoke test matrix | Login as OrgAdmin / Manager / User; list tasks; update status; logout/refresh |
| Update mobile README | Match real URLs + roles |

**Exit criteria:** OrgAdmin sees Projects + Users; app hits live (or local) API reliably.

---

### M1 — Account & onboarding parity (1–2 wks) ≈ Web Phase 3

| Task | API / notes |
|------|-------------|
| Register workspace | `POST api/auth/register` (or current register endpoint) → then login |
| Forgot / reset password | Reuse web token email flow; deep link `taskmanager://reset?token=` or open web |
| Accept invite | `/accept-invite` parity; deep link from email |
| Lightweight onboarding | Checklist: set workspace name → invite → create first project (can skip full wizard UI) |
| Profile refresh | Load current user from API, show org name |

**Out of scope for M1:** custom domain, full branding editor (read-only org color optional).

---

### M2 — Task depth (core PM) (2–3 wks) ≈ Web Phase 4 baseline + rich task

| Task | Notes |
|------|-------|
| Task filters | Status / project / assignee on list (API already supports query params) |
| Comments | List + add; hint for `@username` mentions |
| Subtasks | Toggle complete / add |
| Tags + watchers | Read/write if endpoints exist; else phase next |
| Attachments | Open URL / download; upload if API supports multipart |
| Project members | Assign users to project (parity with Project Mapping) |

**Exit criteria:** Mobile user can fully work a task without opening web for comments/subtasks.

---

### M3 — Views & PM depth I (2–3 wks) ≈ Web Phase 4 gated views

| Task | Entitlement |
|------|-------------|
| Kanban board (swipe/drag columns) | `board_view` |
| Calendar (month by due date) | `calendar_view` |
| Saved filters (simple: save current filter name) | Same as web saved views |
| Custom field values on task detail | `custom_fields` (Professional+) |
| Apply task template (picker → create) | Templates API |

**Defer if needed:** full custom-field admin, project template designer (keep on web).

---

### M4 — Collaboration & realtime (2 wks) ≈ Web Phase 5

| Task | Notes |
|------|-------|
| Notification list + unread badge | `api/notifications` |
| Mark read / open deep link to task | |
| SignalR hub `/hubs/tasks` | JWT `access_token`; update badge + optional toast |
| Activity feed screen | `api/activity` |
| Mention notifications | Already produced by API when comments include `@user` |

**Exit criteria:** Badge updates without pull-to-refresh; tap opens task.

---

### M5 — Billing awareness (1 wk) ≈ Web Phase 2 (read-only + upgrade)

| Task | Notes |
|------|-------|
| Show current plan + seats used | Billing/subscription APIs |
| FeatureGate pattern | Hide Kanban/calendar/etc. with Upgrade CTA |
| Upgrade / manage billing | Open system browser to web `/billing` or `/pricing` |
| Seat limit errors | Surface API 403 messages on invite/create |

**Defer:** Native Razorpay Checkout SDK until web checkout is fully stable and India store compliance is reviewed.

---

### M6 — PM depth II (3–4 wks) ≈ Web Phase 6 (after web ships)

Depends on web APIs for:
- Gantt / dependencies (mobile: read-only timeline or dependency chips first)
- Recurring tasks
- Time tracking (start/stop timer, log hours)
- Simple automations (view rules only; edit on web)

---

### M7 — Integrations surface (1–2 wks) ≈ Web Phase 7

- Deep links for Slack/email → task
- Show webhook/API-key management? **No** — web only
- Optional: share task link

---

### M8 — Security & enterprise (2–3 wks) ≈ Web Phase 8

- Biometric unlock (re-auth with SecureStorage session)
- MFA challenge UI when web enables TOTP
- SSO: prefer system browser / ASWebAuthenticationSession when OIDC is ready
- Remote logout / session list (if API adds it)

---

### M9 — Analytics & polish (ongoing) ≈ Web Phase 9

- Lightweight reports (charts) for Manager
- App Store / Play listing, crash reporting (Sentry mobile SDK)
- Performance: list virtualization, image caching

---

## 4. Suggested build order (priority)

| Priority | Milestone | Why |
|----------|-----------|-----|
| **P0** | **M0** Stabilize | Broken Admin role + wrong API host block everything |
| **P1** | **M2** Task depth | Daily value for existing mobile users |
| **P1** | **M4** Notifications | Matches web Phase 5; high engagement |
| **P2** | **M1** Auth/onboarding | Needed for self-serve mobile acquisition |
| **P2** | **M3** Kanban/calendar | Parity with paid web features |
| **P3** | **M5** Billing awareness | Monetization prompts without native payments complexity |
| **P4** | M6–M9 | Follow web API availability |

---

## 5. Technical notes

### Shared code
- Prefer extending `TaskManager.Shared` DTOs; avoid duplicating models in Mobile.
- Mirror web client service methods in `TaskManager.Mobile/Services/ApiService.cs` incrementally.

### SignalR on MAUI
- Package: `Microsoft.AspNetCore.SignalR.Client` (same as Blazor client).
- Connect after login; disconnect on logout; reconnect with refreshed token.

### Deep links
- Scheme: `taskmanager://` (tasks, invites, reset password).
- Android App Links / iOS Universal Links to `https://taskmanager-app-plt1.onrender.com/...` later.

### CI
- Linux CI cannot build MAUI — keep excluded from current workflow.
- Add **Windows** (or Mac) GitHub Actions job for `net10.0-android` compile when M0 is done.

### Store release (when ready)
- Separate Release API URL, obfuscation optional, privacy policy URL, Razorpay disclosure if native pay added.

---

## 6. Explicitly out of scope (mobile)

- SuperAdmin / platform MRR console  
- Hangfire dashboard, seed tooling  
- Editing plan catalog / Razorpay plan sync  
- Full org branding designer, custom domains  
- Public API key management, outbound webhook config  
- Marketing landing page  

These remain **web-only**.

---

## 7. Tracking

| Artifact | Purpose |
|----------|---------|
| This file | Mobile roadmap |
| [`IMPLEMENTATION_STATUS.md`](./IMPLEMENTATION_STATUS.md) | Web ledger — add a **Mobile** section when M0 starts |
| `TaskManager.Mobile/README.md` | Runbook for developers |

When finishing a mobile milestone: update a **Mobile progress** table in `IMPLEMENTATION_STATUS.md` (or a sibling `MOBILE_IMPLEMENTATION_STATUS.md` if the web ledger gets too large).

---

## 8. First sprint checklist (M0)

- [x] Replace `"Admin"` → `"OrganizationAdmin"` in `AppShell`, ViewModels, role pickers  
- [x] DEBUG/RELEASE `ApiSettings` (local vs Render)  
- [ ] Verify login against production + local  
- [x] Update `TaskManager.Mobile/README.md`  
- [ ] Manual QA: OrgAdmin / Manager / User flyout visibility  

**M0–M3 code complete.** Next: **M4** (notifications + SignalR).
