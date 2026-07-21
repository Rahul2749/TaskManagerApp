# Razorpay Test Mode Setup

You need **three** values from the Razorpay Dashboard (Test Mode):

| Value | Required for | Where to find |
|-------|--------------|---------------|
| **Key ID** (`rzp_test_...`) | Creating customers, plans, subscriptions | Settings → API Keys |
| **Key Secret** | Signing API calls | Settings → API Keys (shown once) |
| **Webhook Secret** | Activating plans after payment | Settings → Webhooks → create endpoint → signing secret |

Without the webhook secret, checkout can still open, but your app will not flip the subscription to `active`/`trialing` after payment.

## 1. Set keys locally (do not commit)

From the web project folder:

```powershell
cd src\TaskManager\TaskManager\TaskManager
dotnet user-secrets set "Razorpay:KeyId" "rzp_test_YOUR_KEY_ID"
dotnet user-secrets set "Razorpay:KeySecret" "YOUR_KEY_SECRET"
dotnet user-secrets set "Razorpay:WebhookSecret" "YOUR_WEBHOOK_SECRET"
```

Restart the app. On startup it will:

1. Create Razorpay Plans for Starter / Professional / Business (monthly + annual)
2. Store the `plan_xxx` IDs in your Postgres `Plans` table

Check:

```text
GET /api/billing/status
```

Expected:

```json
{ "provider": "razorpay", "configured": true, "webhookConfigured": true }
```

## 2. Create the webhook (Test Mode)

1. Razorpay Dashboard → **Settings → Webhooks**
2. Add URL:
   - Local (needs a tunnel such as ngrok): `https://YOUR_TUNNEL/api/billing/webhook`
   - Render: `https://taskmanager-app-plt1.onrender.com/api/billing/webhook`
3. Subscribe to at least:
   - `subscription.authenticated`
   - `subscription.activated`
   - `subscription.charged`
   - `subscription.pending`
   - `subscription.halted`
   - `subscription.cancelled`
   - `invoice.paid`
4. Copy the **Webhook Secret** into user-secrets / Render env as `Razorpay__WebhookSecret`

## 3. Render environment variables

In Render → Environment:

```text
Razorpay__KeyId=rzp_test_...
Razorpay__KeySecret=...
Razorpay__WebhookSecret=...
```

Redeploy / restart so plan sync runs.

## 4. Test the flow

1. Register or sign in as an **OrganizationAdmin**
2. Open `/pricing`
3. Click **Upgrade** on Starter or Professional
4. Complete Razorpay test checkout (use [Razorpay test cards](https://razorpay.com/docs/payments/payments/test-card-upi-details/))
5. Confirm webhook delivery in Razorpay → Webhooks → Logs
6. Open `/billing` — status should become **Active** or **Trial**

## Notes

- Keys stay out of Git. Empty placeholders remain in `appsettings.json`.
- Plan IDs are created once and reused. To recreate them, clear `ProviderMonthlyPlanId` / `ProviderAnnualPlanId` in the DB and restart.
- Enterprise stays “Contact sales” (no self-serve Razorpay plan).
- Free plan never goes through Razorpay.
