# Public API & integrations (Phase 7)

Professional+ plans include `public_api` and `integrations`.

## API keys

1. In the app: **Integrations → API keys → Create key** (copy the `tm_…` secret once).
2. Call the public API:

```http
GET /api/v1/tasks
X-Api-Key: tm_your_secret_here
```

Or: `Authorization: Bearer tm_your_secret_here`

### Endpoints

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/v1/projects` | List projects |
| GET | `/api/v1/tasks` | Optional `projectId`, `status` |
| GET | `/api/v1/tasks/{id}` | Task detail |
| POST | `/api/v1/tasks` | Create (TaskDto body) |
| PATCH | `/api/v1/tasks/{id}/status` | `{ "status": "InProgress" }` |

## Outbound webhooks

Create under **Integrations → Webhooks**. Events: `*` or comma list such as `task.created,task.status_changed,task.completed`.

Headers on delivery:

- `X-TaskManager-Event`
- `X-TaskManager-Delivery`
- `X-TaskManager-Signature: sha256=<hex>` (HMAC-SHA256 of body with the webhook secret)

Failed deliveries retry via Hangfire (exponential backoff, up to 5 attempts).

## Slack / GitHub

Add an **incoming webhook URL** under **Integrations → Slack / GitHub**. Task create/status/complete events post a simple Slack message (or JSON for GitHub/custom).
