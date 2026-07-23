# Enterprise security (Phase 8)

Business+ plans include `audit_log` and `sso`.

## Audit log

- UI: **Audit log** (Admin / Manager)
- API: `GET /api/audit-logs?take=100`
- Recorded actions include password/SSO login, API key create/revoke, SSO config changes, and GDPR exports

## GDPR export

Org admins: **Security → Download export** (`GET /api/gdpr/export`) — JSON of users, projects, tasks, comments, and time entries for the current workspace.

## SSO (Google / Microsoft OIDC)

1. Ensure `App__PublicBaseUrl` matches your public site URL (used for OAuth redirects).
2. In Google Cloud / Azure AD, register an OAuth app with redirect URI:

   `{PublicBaseUrl}/api/sso/callback`

3. In the app: **Security** → enter Client ID / secret, enable SSO, optionally restrict email domains.
4. Users sign in via **Login → Sign in with SSO** using the workspace slug (`/api/sso/{slug}/start`).

Mobile uses the same flow with `?client=mobile`, which redirects to `taskmanager://sso-callback?code=…` for WebAuthenticator.

SAML, SCIM, and custom RBAC are deferred.
