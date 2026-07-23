# Free Deployment Guide — Render + Neon

This deployment uses:

- Render Free Web Service for the ASP.NET Core host, API, and Blazor client
- Neon Free PostgreSQL for persistent data
- GitHub for source control and automatic deployments

The free tiers are suitable for demos and early validation. They do not provide
production uptime guarantees.

## 1. Create the Neon database

1. Sign in at <https://console.neon.tech>.
2. Create a project named `taskmanager-production`.
3. Select Singapore, or the region nearest to the Render service.
4. Open **Connect** and select **.NET**.
5. Disable the connection-pooling toggle for this deployment.
6. Copy the direct .NET connection string. It should resemble:

   ```text
   Host=ep-example.ap-southeast-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=...;SSL Mode=VerifyFull;Channel Binding=Require
   ```

The application performs idempotent migrations and plan seeding during startup,
so the direct Neon connection is used. Never commit the Neon connection string.

## 2. Push the deployment files

From the repository root:

```powershell
dotnet build "src/TaskManager/TaskManager/TaskManager/TaskManager.csproj" --configuration Release
docker build --tag taskmanager:local .
git status
git add .
git commit -m "Prepare TaskManager for free cloud deployment"
git push origin HEAD
```

The repository includes:

- `Dockerfile`
- `.dockerignore`
- `render.yaml`
- `.github/workflows/ci.yml`

## 3. Create the Render service

1. Sign in at <https://dashboard.render.com> with GitHub.
2. Select **New → Blueprint**.
3. Connect this repository.
4. Render detects `render.yaml`.
5. Keep the Free plan and Singapore region.
6. Enter the requested secret environment values:

   - `ConnectionStrings__DefaultConnection`: direct Neon .NET connection string
   - `Seed__SuperAdminEmail`: the platform administrator email
   - `Seed__SuperAdminPassword`: a new password with at least 12 characters,
     uppercase, lowercase, and a number

Render generates `JwtSettings__SecretKey` automatically.

Do not add Razorpay environment variables until credentials are available.

## 4. First deployment

Click **Apply** or **Create Web Service**.

The first startup will:

1. Connect to Neon over TLS.
2. Apply the PostgreSQL migration.
3. Seed the five subscription plans.
4. Create the configured SuperAdmin if one does not exist.
5. Start the application on port `10000`.

Expected log messages include:

```text
Seeded 5 subscription plans
Created platform SuperAdmin account superadmin; password was not logged
Database initialization completed
Now listening on: http://0.0.0.0:10000
```

The health check configured in Render is:

```text
/health/live
```

## 5. Verify deployment

Replace `your-service` with the Render service name:

```text
https://your-service.onrender.com/
https://your-service.onrender.com/health/live
https://your-service.onrender.com/health/ready
https://your-service.onrender.com/pricing
https://your-service.onrender.com/register
```

Both health endpoints should return HTTP 200.

Then verify:

1. Sign in using the SuperAdmin account configured in Render.
2. Open all SuperAdmin pages.
3. Register a separate workspace through `/register`.
4. Create a manager, user, project, and task.
5. Sign out and sign back in.
6. Register a second workspace and confirm tenant data is isolated.
7. Confirm `/billing` displays the provider-not-configured message.

## 6. Automatic deployments

Every push to the configured branch triggers a Render deployment. GitHub Actions
also performs:

1. .NET restore
2. Release build of the web application
3. Docker image build

The MAUI project is intentionally excluded from Linux CI.

## 7. Updating the database

For this free single-instance deployment, startup migration remains enabled:

```text
Database__InitializeOnStartup=true
```

Migrations and seed operations are idempotent. Before pushing a migration:

1. Create a Neon snapshot.
2. Test the migration locally.
3. Push the migration and monitor Render logs.
4. Confirm `/health/ready` returns HTTP 200.

For a paid or multi-instance deployment, disable startup migrations and use a
dedicated release/migration job.

## 8. Custom domain

1. Open Render service **Settings → Custom Domains**.
2. Add `app.yourdomain.com`.
3. Add the displayed CNAME record at the DNS provider.
4. Wait for Render to issue the TLS certificate.

The default `onrender.com` address is free and can be used initially.

## 9. Free-tier limitations

- Render Free sleeps after approximately 15 minutes without traffic.
- A cold start can take around one minute.
- Render local filesystem contents are not durable.
- Do not rely on local attachment storage.
- Neon Free storage and monthly compute/network quotas are limited.
- Render Free blocks common SMTP ports; leave `Email__Host` empty to log emails, or use a provider HTTPS API later.
- Hangfire dashboard is at `/hangfire` (open in Development; SuperAdmin in Production).
- Create Neon snapshots and periodic `pg_dump` backups.

Upgrade hosting before promising uptime or onboarding paying customers.

## 10. Email (optional)

Set on Render when you have SMTP credentials that work from the host:

```text
App__PublicBaseUrl=https://taskmanager-app-plt1.onrender.com
App__BillingGracePeriodDays=7
Email__Host=
Email__Port=587
Email__EnableSsl=true
Email__FromAddress=
Email__FromName=TaskManager
Email__Username=
Email__Password=
```

For SSO (Google/Microsoft), register this OAuth redirect URI with your IdP:

`{App__PublicBaseUrl}/api/sso/callback`

See [`ENTERPRISE_SECURITY.md`](./ENTERPRISE_SECURITY.md).

Without `Email__Host`, welcome / reset / invite / receipt / payment-failed messages are logged and treated as sent.

## 11. Razorpay later

When test credentials are available, add these only in Render Environment:

```text
Razorpay__KeyId
Razorpay__KeySecret
Razorpay__WebhookSecret
```

Never add payment secrets to `appsettings.json` or Git.
