# SaaS Implementation Status

Status of work completed against [`SAAS_IMPLEMENTATION_PLAN.md`](./SAAS_IMPLEMENTATION_PLAN.md).  
Last updated: **21 Jul 2026** (Phase 3 onboarding + workspace settings).

**Live site:** https://taskmanager-app-plt1.onrender.com  
**Stack:** ASP.NET Core + Blazor WASM, PostgreSQL (Neon), Render, Razorpay (INR), MudBlazor.

---

## Summary

| Phase | Plan focus | Status |
|-------|------------|--------|
| **0** Foundations & hardening | Secrets, Postgres, email, Hangfire, observability, CI/CD | **Mostly done** |
| **1** Design system (MudBlazor) | Theme, shell, FeatureGate | **Mostly done** |
| **2** Billing & subscriptions | Razorpay, entitlements, pricing UI | **Mostly done** |
| **3** Signup & onboarding | Self-serve signup, invites, org settings, wizard | **Mostly done** |
| **4–9** PM depth, realtime, API, enterprise, analytics | Later roadmap | **Not started** |

**First milestone (Phase 0 + Phase 2)** from the plan is largely shipped. Remaining gaps are called out below.

---

## Locked decisions (Section 0) — followed

| Decision | Choice | Implemented? |
|----------|--------|--------------|
| Market / gateway | India → Razorpay (INR) | Yes — `IBillingProvider` + `RazorpayBillingProvider` |
| UI | MudBlazor | Yes — shell and main pages migrated |
| Database | PostgreSQL (Npgsql) | Yes — Neon + EF migrations |
| Hosting | Free / low-cost | Yes — Render + Docker |
| Pricing | Per-seat, monthly + annual | Yes — catalog + checkout |
| First milestone | Phase 0 + Phase 2 | Largely complete |

---

## Phase 0 — Foundations & hardening

| Item | Status | Notes |
|------|--------|-------|
| PostgreSQL / Npgsql | Done | SQL Server removed; Postgres migrations |
| Serilog | Done | Console structured logging |
| Health checks | Done | `/health/live`, `/health/ready` (DB) |
| Auth rate limiting | Done | `auth` policy on login/register/reset |
| `IEmailService` (SMTP) | Done | Welcome, reset, invite, receipt; logs if SMTP unset |
| Hangfire + Postgres | Done | Email jobs; dashboard `/hangfire` |
| Docker + `render.yaml` | Done | Deploy guide in [`DEPLOYMENT_GUIDE.md`](./DEPLOYMENT_GUIDE.md) |
| GitHub Actions CI | Done | Build workflow |
| Configurable seed | Done | `SeedOptions` — production-safe defaults |
| Data Protection keys in DB | Done | Survives Render redeploys / antiforgery |
| Secrets out of `appsettings.json` | **Deferred** | User chose to keep JWT/DB in repo for now; Render uses env vars for Razorpay |
| Sentry / OpenTelemetry | Not started | |
| Automated test harness | Not started | |
| Redis cache | Not started | In-memory cache used for entitlements |

---

## Phase 1 — Design system

| Item | Status | Notes |
|------|--------|-------|
| MudBlazor adoption | Done | App shell, auth, admin/manager/user pages |
| `FeatureGate` / `UpgradePrompt` | Done | Client billing components |
| Empty states / loading / page header | Done | Shared MudBlazor patterns |
| Marketing landing polish | Partial | Pricing page exists; full marketing landing still light |
| Dark mode / tenant branding theme | Not started | |

---

## Phase 2 — Billing & subscriptions

| Item | Status | Notes |
|------|--------|-------|
| Domain: Plan, PlanFeature, Subscription, Invoice, UsageCounter, BillingEvent | Done | EF + seed from `PlanCatalog` |
| `IBillingProvider` + Razorpay | Done | Customer, plan, subscription, cancel, webhook verify |
| Plan sync on startup | Done | Creates Razorpay plans; refreshes when catalog prices change |
| Checkout | Done | Checkout.js + `subscription_id` (hosted `short_url` unreliable in test) |
| Webhooks | Done | Subscription lifecycle + `invoice.paid`; idempotent `BillingEvent` |
| Receipt email on paid invoice | Done | Via Hangfire |
| `IEntitlementService` (cached) | Done | Features + limits; invalidate on sub change |
| Feature gating UI | Done | `FeatureGate` / upgrade prompts |
| API `[RequiresFeature]` filter | Partial | Entitlement checks exist; not every create path gated yet |
| Pricing page (₹) | Done | Monthly/annual toggle |
| Billing page | Done | Current plan, invoices, cancel |
| SuperAdmin MRR / orgs / users | Done | Summary, org suspend/archive, reports |
| Current prices (INR / seat) | Done | Free ₹0 · Starter ₹89 · Professional ₹149 · Business ₹349 · Enterprise custom |
| Annual discount | Done | ~17% (10× monthly ≈ 2 months free) |
| 14-day trial on paid plans | Done | Catalog `TrialDays`; Razorpay `start_at` |
| Dunning / `past_due` soft-limit | Partial | Status mapping exists; admin email + grace soft-limit not fully built |
| Seat proration / change plan mid-cycle | Not started | Upgrade creates new subscription path today |
| Customer portal (Razorpay) | Not started | Cancel at period end via API |

See also [`RAZORPAY_TEST_SETUP.md`](./RAZORPAY_TEST_SETUP.md).

---

## Phase 3 — Self-serve signup & onboarding

| Item | Status | Notes |
|------|--------|-------|
| Public `/register` → org + Free plan + OrganizationAdmin | Done | |
| Password forgot / reset | Done | Token + email via Hangfire |
| Seat invites by email | Done | `/invites`, `/accept-invite`; seat limits enforced |
| Role assignment on invite | Done | OrgAdmin: User/Manager; Manager: User |
| Onboarding wizard | Done | `/onboarding` — workspace → invite → first project → finish |
| Org settings / branding / timezone | Done | `/admin/organization` + `api/organizations/current` |
| Custom domain | Not started | Enterprise later |

---

## Phases 4–9 — Not started (high level)

| Phase | Scope | Status |
|-------|--------|--------|
| **4** Kanban, calendar, saved views, custom fields, templates | Not started |
| **5** SignalR, notification center, @mentions, activity feed | Not started |
| **6** Gantt, dependencies, recurring tasks, time tracking, automations | Not started |
| **7** Public API keys, outbound webhooks, Slack/GitHub | Not started |
| **8** SSO/SAML/SCIM, custom RBAC, GDPR export | Not started |
| **9** Custom reports, usage analytics, optional AI | Not started |

Baseline already had list-style tasks, subtasks, comments, tags, attachments, watchers — not the gated “depth” views above.

---

## Milestone A / B checklist (plan Section 8)

### Milestone A — Foundations

- [x] PostgreSQL switch + migrations  
- [x] Email (`IEmailService` SMTP + templates)  
- [x] Hangfire (Postgres) for emails  
- [x] Serilog + health checks  
- [ ] Secrets fully removed from source & rotated  
- [ ] Sentry / richer error tracking  

### Milestone B — Billing

- [x] Billing domain model + 5-tier seed (INR)  
- [x] Razorpay provider + plan sync  
- [x] Checkout + webhook sync  
- [x] Entitlements + UI gates  
- [x] Pricing + billing UI  
- [x] SuperAdmin subscription overview  
- [ ] Full dunning + soft-limit after grace  
- [ ] Seat change proration  

---

## Ops & docs delivered

| Doc / artifact | Purpose |
|----------------|---------|
| [`DEPLOYMENT_GUIDE.md`](./DEPLOYMENT_GUIDE.md) | Render + Neon + env vars |
| [`RAZORPAY_TEST_SETUP.md`](./RAZORPAY_TEST_SETUP.md) | Keys, webhook, Checkout.js test flow |
| `Dockerfile` / `render.yaml` | Container deploy |
| `.github/workflows/ci.yml` | CI build |

---

## Suggested next work (priority)

1. **Phase 4** — Kanban board (highest product differentiator after billing/onboarding).  
2. **Harden billing** — dunning emails, enforce limits on project/user create everywhere, plan change/proration.  
3. **Secrets hygiene** — remove JWT/DB secrets from Git; rotate.  
4. **Tests + Sentry** — protect revenue and signup paths.  
5. Custom domain / advanced org branding (Enterprise).

---

## How to keep this file current

When finishing a plan item:

1. Tick or move the row in the matching phase table.  
2. Bump **Last updated** at the top.  
3. Link any new doc under **Ops & docs**.

The authoritative product roadmap remains [`SAAS_IMPLEMENTATION_PLAN.md`](./SAAS_IMPLEMENTATION_PLAN.md); this file is the **progress ledger**.
