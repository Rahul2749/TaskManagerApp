# TaskManager → Enterprise SaaS: Implementation Plan

This plan turns the current web app (ASP.NET Core API + Blazor host + Blazor WASM client, multi-tenant, role-based) into a market-ready, enterprise-grade SaaS with billing, subscription plans, feature gating, and a professional design system. Mobile is intentionally out of scope for this plan.

---

## 0. Locked decisions (confirmed)

| Decision | Choice | Implications |
|----------|--------|--------------|
| **Market / gateway** | **India first → Razorpay (INR, UPI + cards)** | Use Razorpay Subscriptions + Webhooks; keep an `IBillingProvider` abstraction so Stripe can be added for global later. |
| **UI** | **MudBlazor** design system | Replace per-page CSS with MudBlazor theme/tokens; custom CSS only for the marketing site. |
| **Database** | **PostgreSQL** (move off SQL Server) | Switch EF provider to **Npgsql**; free/cheap managed Postgres (Neon / Supabase / Railway). Re-generate migrations. |
| **Hosting** | **Free / low-cost for now** | Target free tiers (Render / Railway / Fly.io / Azure free) + free managed Postgres + Razorpay. Cloud storage via a free-tier bucket (Supabase Storage / Cloudflare R2). |
| **Pricing** | **Per-seat, monthly + annual** | Seats tied to billing quantity; proration on seat/plan change. |
| **First milestone** | **Phase 0 hardening + Phase 2 billing** (fastest to revenue) | Secrets cleanup, Postgres switch, email, jobs, then Razorpay subscriptions + entitlements + feature gating. |

> Because the market is India, all prices are in **INR (₹)** and Razorpay is the primary gateway. Stripe remains a future add behind the same `IBillingProvider`.

---

## 1. Where we are today (baseline)

**Strengths already in place**
- Multi-tenant data model (`Organization`, `OrganizationMember`) with EF Core global query filters per tenant.
- JWT auth with refresh tokens, role-based access (`SuperAdmin`, `OrganizationAdmin`, `Manager`, `User`).
- Core domain: Projects, Tasks, Subtasks, Comments, Tags, Attachments, Watchers, Task history.
- Clean-ish server layering: Repository/UnitOfWork, FluentValidation, exception middleware, DTO mapping, pagination.
- Custom CSS design tokens + Phosphor icons; skeleton loaders started.

**Key gaps for an enterprise SaaS**
- No billing / subscriptions / plan entitlements / feature gating.
- No self-serve signup + org onboarding; orgs are seeded.
- `EmailService` is an empty stub (no transactional email).
- No real-time updates, in-app notifications, or activity feed.
- Auth lacks MFA, SSO/SAML, API keys, audit logging, session management.
- Project management depth is thin (no boards, calendar, timeline/Gantt, custom fields, automation, time tracking, templates, dependencies).
- No public API/webhooks/integrations, no cloud file storage, no background jobs, no caching.
- No observability (structured logging/metrics/tracing), no automated tests, no CI/CD.
- UI is hand-rolled per page (inconsistent, hard to scale, accessibility not guaranteed).

---

## 2. Product & pricing strategy

### 2.1 Subscription tiers (recommended)

| Plan | Target | Price model | Seats | Highlights |
|------|--------|-------------|-------|------------|
| **Free** | Individuals / trial | $0 | up to 3 | 2 projects, list + board views, 100 MB storage, community support |
| **Starter** | Small teams | per seat / mo | up to 10 | Unlimited projects, calendar view, basic reports, 5 GB, email support |
| **Professional** | Growing teams | per seat / mo | up to 50 | Timeline/Gantt, custom fields, automations, time tracking, integrations, 100 GB, priority support |
| **Business** | Larger orgs | per seat / mo | up to 200 | Advanced reports/dashboards, workload, guest users, SSO (Google/MS), audit log, 1 TB |
| **Enterprise** | Enterprise | custom / annual | unlimited | SAML SSO + SCIM, custom roles/RBAC, data residency, SLA, dedicated support, invoicing/PO |

Add a **14-day Professional trial** on signup, monthly + annual billing (annual ~2 months free), and per-seat proration on changes.

### 2.2 Entitlements = features + limits

Two kinds of gates, both data-driven (not hard-coded per tier):
- **Feature flags** (boolean): `gantt`, `automations`, `custom_fields`, `time_tracking`, `sso_saml`, `audit_log`, `public_api`, `guest_users`, `advanced_reports`, etc.
- **Usage limits** (numeric): `max_projects`, `max_seats`, `storage_gb`, `api_calls_per_month`, `automation_runs_per_month`, `file_size_mb`.

---

## 3. Billing architecture

### 3.1 Gateway choice — Razorpay (India-first, confirmed)
- **Primary: Razorpay Subscriptions** — INR, UPI + cards + netbanking, Plans + Subscriptions API, hosted checkout, and **webhooks** as the source of truth. Razorpay handles the card capture; we store only references + display data.
- Wrap everything behind an **`IBillingProvider`** interface (`CreateCustomer`, `CreateSubscription`, `ChangePlan`, `Cancel`, `VerifyWebhook`) so **Stripe** can be added later for the global market without touching business logic.
- Razorpay specifics: create **Plans** (per interval) → create a **Subscription** for the org's customer → redirect to Razorpay checkout → verify signature → confirm via `subscription.activated`/`charged` webhooks. Handle `subscription.pending/halted` for dunning.

### 3.2 New domain entities (backend)
- `Plan` (code, name, description, isActive, trialDays, prices per interval, provider price IDs)
- `PlanFeature` (planId, featureKey, boolean or numeric limit)
- `Subscription` (organizationId, planId, status: trialing/active/past_due/canceled, currentPeriodEnd, seats, providerCustomerId, providerSubscriptionId, cancelAtPeriodEnd)
- `Invoice` (organizationId, amount, currency, status, providerInvoiceId, pdfUrl, issuedAt)
- `PaymentMethod` (optional cache of last4/brand for display; source of truth stays in Stripe)
- `UsageCounter` (organizationId, key, period, value) for metered limits
- `BillingEvent` / webhook log for idempotency & audit

### 3.3 Flow
1. Org admin picks a plan → server creates Stripe Customer + Checkout Session → redirect.
2. Stripe **webhooks** (`checkout.session.completed`, `customer.subscription.updated/deleted`, `invoice.paid`, `invoice.payment_failed`) update our `Subscription`/`Invoice` — webhooks are the source of truth (idempotent handlers).
3. Plan changes/cancellations via Stripe Customer Portal → webhook syncs back.
4. Dunning: on `payment_failed`, mark `past_due`, email the admin, and soft-limit the org after a grace period.

### 3.4 Feature gating (enforced in 3 layers)
- **Service**: `IEntitlementService.HasFeature(orgId, key)` and `GetLimit(orgId, key)` with Redis/memory caching (invalidated on subscription webhook).
- **API**: `[RequiresFeature("gantt")]` authorization filter + limit checks before create operations (e.g., block project #3 on Free).
- **UI**: a `<FeatureGate Feature="gantt">` Blazor component that shows the feature or an upgrade prompt, plus disabled/locked states with "Upgrade" CTAs.

---

## 4. Feature roadmap (what to build)

### 4.1 Account, onboarding & org management
- Self-serve **signup** → create org (workspace) → onboarding wizard (invite teammates, create first project, sample data).
- **Seat management**: invite by email, pending invites, role assignment, deactivate/reactivate, seat count tied to billing.
- **Org settings**: profile, logo/branding, timezone, working days, custom domain (Enterprise).
- **Custom roles & permissions (RBAC)** beyond the 4 fixed roles (Business/Enterprise).

### 4.2 Project management depth (core differentiator)
- **Views**: List, **Kanban board** (drag-drop), Calendar, **Timeline/Gantt**, Table — with saved filters & per-user saved views.
- **Custom fields** (text/number/date/select/user) per project.
- **Task enhancements**: dependencies, recurring tasks, milestones, **task templates & project templates**, checklists (subtasks exist), priorities/estimates (exist).
- **Automations / workflow rules**: "when status = Done, notify watchers", "when created, assign to…", scheduled rules.
- **Time tracking**: estimates vs. actuals (fields exist), timers, timesheets, reports.
- **Workload / capacity** view per assignee.

### 4.3 Collaboration & notifications
- **Real-time** updates via **SignalR** (live board/task changes, presence).
- **Notifications**: in-app center + email digests; `@mentions` in comments; per-user preferences.
- **Activity feed / audit trail** per task, project, and org.

### 4.4 Search, reporting & analytics
- Global search (tasks/projects/people) with filters.
- Dashboards & **custom reports**: burndown, velocity, cycle time, completion; export CSV/Excel/PDF.
- Scheduled report emails (Business+).

### 4.5 Integrations & API platform
- **Public REST API** with **API keys** + scopes; OpenAPI docs.
- **Webhooks** (outbound) for task/project events.
- First-party integrations: Slack/Teams notifications, GitHub/GitLab, Google/Outlook calendar, Zapier/Make.

### 4.6 Files & storage
- Move attachments to **cloud storage** (Azure Blob or S3) with signed URLs, size limits per plan, image previews, virus scan hook.

### 4.7 Platform / SuperAdmin console
- Tenant list & health, subscription/MRR overview, impersonation (with audit), feature-flag overrides, suspend/reactivate orgs, plan management UI.

---

## 5. Non-functional & platform hardening

- **Security**: MFA/TOTP, SSO (OIDC now, SAML+SCIM for Enterprise), password policy, session/device management, rate limiting, secrets in **Azure Key Vault / user-secrets** (currently a live DB password + JWT key sit in `appsettings.json` — must be removed & rotated), CSP/security headers, audit logging.
- **Background jobs**: **Hangfire** (or Quartz) for emails, webhook delivery + retries, billing sync, reminders, digests, usage rollups.
- **Caching**: **Redis** for entitlements, sessions, hot reads.
- **Transactional email**: **SendGrid/Postmark** behind `IEmailService` (implement the current stub) with templates (invite, reset, receipts, dunning, digests).
- **Observability**: **Serilog** structured logs → Seq/App Insights; OpenTelemetry traces/metrics; **Sentry** for errors; health checks + readiness probes.
- **Testing**: xUnit unit tests, WebApplicationFactory integration tests, Playwright E2E for critical flows (signup→pay→create project).
- **CI/CD & infra**: Dockerize, GitHub Actions (build/test/scan/deploy), IaC (Bicep/Terraform), staging + prod environments, DB migrations in pipeline, backups & restore drills.
- **Tenancy at scale**: keep shared-DB + tenant-column now; document a path to per-tenant schema/DB for large Enterprise customers later.

---

## 6. UI / UX strategy (must look market-grade, not "vibe coded")

**Approach: adopt a real design system instead of per-page hand-rolled CSS.**

- **Component library (recommended): MudBlazor** — mature, enterprise data-dense components (data grid, dialogs, date pickers, drag-drop, charts), theming, accessibility, dark mode out of the box. Alternative: **Radzen Blazor** (great DataGrid/scheduler/Gantt) or **Fluent UI Blazor** (Microsoft look). Keep Phosphor icons.
- **Design tokens & theme**: formalize the existing CSS variables into a single theme (color, spacing, radius, typography, elevation), light/dark, brand-configurable per tenant.
- **Pattern library**: standardized page shell, data tables, empty states, skeletons (already started), toasts, modals, form patterns, filters, and an **`<UpgradePrompt>`/`<FeatureGate>`** pattern for gated features.
- **Marketing surface**: a polished public **landing page + pricing page + docs** (this is what "captures the market"). Fast, responsive, SEO-friendly, with clear CTAs to trial/signup.
- **Onboarding UX**: guided first-run, sample workspace, checklist, contextual tips.
- **Quality bar**: WCAG 2.1 AA, keyboard nav, responsive, consistent 60fps interactions, motion used sparingly.

> Decision needed: introduce MudBlazor/Radzen (faster, consistent, accessible) vs. keep hand-rolled CSS and formalize it (more control, more effort). Recommendation: **MudBlazor for the app UI + custom CSS for the marketing site.**

---

## 7. Phased delivery plan

Each phase is shippable. Order optimizes for revenue enablement + trust.

**Phase 0 — Foundations & hardening (2–3 wks)**
Secrets out of source & rotated; Serilog + health checks + Sentry; Hangfire; implement `IEmailService` (SendGrid/Postmark); rate limiting; CI/CD + Docker + staging; test harness scaffolding.

**Phase 1 — Design system (2–3 wks, parallelizable)**
Introduce chosen component library + theme/tokens; migrate shell + auth + top 3 pages; build `FeatureGate`/`UpgradePrompt`, empty states, toasts.

**Phase 2 — Billing & subscriptions (3–4 wks)**
`Plan`/`Subscription`/`Invoice`/entitlement entities; Stripe Checkout + Customer Portal + webhooks; `IEntitlementService` + gating (API + UI); plan/pricing pages; SuperAdmin plan management; dunning + trial.

**Phase 3 — Self-serve signup & onboarding (2 wks)**
Public signup → create org → onboarding wizard; seat invites tied to billing; org settings/branding.

**Phase 4 — PM depth I (3–4 wks)**
Kanban board (drag-drop), Calendar view, saved filters/views, custom fields, task templates.

**Phase 5 — Collaboration & real-time (2–3 wks)**
SignalR live updates; notification center + email digests; `@mentions`; activity feed/audit.

**Phase 6 — PM depth II (3–4 wks)**
Timeline/Gantt, dependencies, recurring tasks, time tracking + timesheets, workload view, automations.

**Phase 7 — Integrations & API platform (3 wks)**
Public API + API keys + OpenAPI; outbound webhooks; Slack + GitHub + calendar integrations.

**Phase 8 — Enterprise & security (3–4 wks)**
SSO (OIDC→SAML) + SCIM, custom RBAC, advanced audit, data export/GDPR, SLA/observability polish.

**Phase 9 — Analytics, reporting & AI (ongoing)**
Custom dashboards/reports + exports; usage analytics; optional AI (task suggestions, summaries, natural-language search).

---

## 8. First milestone — concrete task breakdown (Phase 0 + Phase 2)

### Milestone A — Foundations & DB move (no external paid accounts needed)
1. **Secrets cleanup**: remove the DB password + JWT key from `appsettings.json`, move to user-secrets/env vars, rotate them, and ensure `.gitignore` covers local secret files.
2. **PostgreSQL switch**: swap `Microsoft.EntityFrameworkCore.SqlServer` → `Npgsql.EntityFrameworkCore.PostgreSQL`; fix provider-specific types (e.g. `decimal`, `datetime` → `timestamptz`); **re-create migrations** for Postgres; point at a free managed Postgres (Neon/Supabase).
3. **Email**: implement `IEmailService` (SMTP now / provider later) with templates (invite, reset, receipt, dunning).
4. **Background jobs**: add **Hangfire** (Postgres storage) for webhook retries, emails, usage rollups.
5. **Observability**: Serilog structured logging + health checks; error tracking hook.

### Milestone B — Billing & subscriptions (needs Razorpay keys)
6. **Domain model**: `Plan`, `PlanFeature`, `Subscription`, `Invoice`, `UsageCounter`, `BillingEvent` + migrations; seed the 5 plans (INR, per-seat, monthly/annual).
7. **`IBillingProvider` + `RazorpayBillingProvider`**: create customer/subscription, change plan, cancel, verify webhook signature.
8. **Checkout flow**: pick plan → create subscription → Razorpay checkout → signature verify → activate.
9. **Webhooks**: idempotent handlers for `subscription.activated/charged/pending/halted/cancelled`, `invoice.paid` → sync `Subscription`/`Invoice`.
10. **`IEntitlementService`** (cached) + enforcement: `[RequiresFeature]` API filter + limit checks + `<FeatureGate>`/`<UpgradePrompt>` UI components.
11. **Billing UI**: pricing page (₹), current plan, invoices list, seat management, upgrade/downgrade/cancel.
12. **SuperAdmin**: plan management + subscription/MRR overview.

### What I need from you to build Milestone B
- A **Razorpay account** (test mode is fine to start) → **Key ID + Key Secret** and a **webhook secret**.
- A **free Postgres** connection string (Neon/Supabase) for Milestone A.

I can start **Milestone A** immediately (no paid accounts required) — the DB move and hardening are prerequisites for everything else.

---

## 9. Confirmed decisions
See **Section 0** — India/Razorpay/INR, MudBlazor, PostgreSQL, free hosting, per-seat pricing, revenue-first milestone.
