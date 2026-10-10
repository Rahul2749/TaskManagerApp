# TaskManager: GitHub Actions + Linux Docker deployment

This guide deploys the existing .NET 10 Blazor Web App to one Ubuntu server. GitHub Actions builds the Docker image, publishes a commit-tagged image to GitHub Container Registry (GHCR), copies the Compose file to the server, and restarts the app. Nginx runs on the host and proxies web traffic to the app container; Certbot obtains and renews its HTTPS certificate. PostgreSQL runs in a persistent Docker volume on the same server.

The Render setup remains available. The container listens on port 8080 by default; Render can continue to override that with its existing `ASPNETCORE_URLS=http://0.0.0.0:10000` setting.

## What you need before starting

- A GitHub repository with this code and permission to change repository settings.
- An Ubuntu 22.04 or 24.04 x64 server with a public IPv4 address and an account that can SSH as `root` (or an account with `sudo`). A small production installation should have at least 2 GB RAM; more is preferable when the database and Hangfire jobs are busy.
- A domain or subdomain you control, for example `tasks.example.com`.
- The domain's DNS `A` record pointed to the server's public IPv4. Add an `AAAA` record only if the server has working public IPv6 and its firewall allows it.
- Credentials from your email provider and Razorpay only if you plan to enable those integrations on day one.

Replace example domains, IPs, repository owners, and email addresses below with yours.

## 1. Point DNS and open the server firewall

At your DNS provider, add:

| Type | Name | Value |
|---|---|---|
| A | `tasks` (or your chosen subdomain) | Server public IPv4 |

Point the DNS record to the server before running Certbot. At the server provider's network firewall, allow inbound TCP ports **22**, **80**, and **443**. On the server, allow SSH and Nginx through UFW:

```bash
ufw allow OpenSSH
ufw allow 'Nginx Full'
ufw --force enable
ufw status verbose
```

Do not open PostgreSQL port 5432 or application port 8080 publicly.

## 2. Install Docker and prepare a non-root deployment user

Connect as the server's initial administrator. These commands assume Ubuntu and a root SSH session:

```bash
ssh root@SERVER_PUBLIC_IP
apt-get update
apt-get upgrade -y
apt-get install -y ca-certificates curl nginx certbot python3-certbot-nginx

install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo \"${UBUNTU_CODENAME:-$VERSION_CODENAME}\") stable" > /etc/apt/sources.list.d/docker.list

apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker
systemctl enable --now nginx
```

Create a dedicated account for deployments. Membership in the `docker` group grants root-equivalent control of this server, so use this account only for trusted GitHub deployments and server administration.

```bash
adduser --disabled-password --gecos "" deploy
usermod -aG docker deploy
install -d -o deploy -g deploy -m 0750 /opt/taskmanager
install -d -o deploy -g deploy -m 0750 /opt/taskmanager/backups
```

Keep this root session open while setting up and testing SSH access for `deploy`.

## 3. Create the GitHub Actions SSH key

On your own computer (PowerShell, macOS, or Linux), create a dedicated key with no passphrase. Keep the private key for GitHub only:

```bash
ssh-keygen -t ed25519 -a 64 -C "github-actions-taskmanager" -f taskmanager_deploy
```

This creates `taskmanager_deploy` (private) and `taskmanager_deploy.pub` (public). Add the **public** key to the server:

```bash
ssh root@SERVER_PUBLIC_IP
install -d -o deploy -g deploy -m 0700 /home/deploy/.ssh
nano /home/deploy/.ssh/authorized_keys
```

Paste the one-line contents of `taskmanager_deploy.pub`, save, then run:

```bash
chown deploy:deploy /home/deploy/.ssh/authorized_keys
chmod 0600 /home/deploy/.ssh/authorized_keys
```

From your computer, test the key before proceeding:

```bash
ssh -i ./taskmanager_deploy deploy@SERVER_PUBLIC_IP
```

Confirm this logs in as `deploy`. Exit that session. Do not put the private key on the server.

## 4. Prepare PostgreSQL and, if needed, migrate the existing Render database

From the repository root on your computer, copy the initial Compose files to the server. The workflow will keep these files current after the first deployment:

```bash
scp -i ./taskmanager_deploy compose.yaml deploy@SERVER_PUBLIC_IP:/opt/taskmanager/
```

Create the `.env` file in section 5 below, then return here. Before starting the app, start **only** PostgreSQL:

```bash
cd /opt/taskmanager
docker compose up -d db
docker compose exec -T db sh -c 'until pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"; do sleep 2; done'
```

If this is a new installation with no production data to keep, skip the export/import below. The first GitHub deployment will create the schema and initial administrator.

If the live Render app currently uses Neon/PostgreSQL and its users and data must be kept, make a final consistent database dump **before** the app is first started against this server. Schedule a short maintenance window and stop app writes (the Render web service and background workers) before taking the final dump. Use a PostgreSQL URI from the database provider, such as `postgresql://user:password@host/database?sslmode=require`; percent-encode reserved characters in URI credentials. Do not paste the app's semicolon-delimited Npgsql connection string here. On the new server as `deploy`, enter the URI without saving it to shell history:

```bash
read -rsp "Source PostgreSQL URI: " SOURCE_DATABASE_URL; echo
docker run --rm -e SOURCE_DATABASE_URL="$SOURCE_DATABASE_URL" postgres:17-alpine \
  sh -c 'pg_dump --format=custom --no-owner --no-privileges "$SOURCE_DATABASE_URL"' \
  > /opt/taskmanager/backups/pre-cutover.dump
unset SOURCE_DATABASE_URL
```

Use a `postgres` image whose `pg_dump` client is the same or newer major version than the source database. Restore the dump into the fresh, empty local database before starting the web app:

```bash
cd /opt/taskmanager
docker compose exec -T db sh -c 'pg_restore --exit-on-error --no-owner --no-privileges -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < /opt/taskmanager/backups/pre-cutover.dump
```

Keep the source database and its provider backup until the new app is verified. Do not restore into a non-empty database. Before the first workflow deployment, point the domain's DNS `A` record to this server and wait until it resolves here. The Render app can remain stopped during the short cutover window; keep the source database and Render configuration until the new site is verified. The local PostgreSQL volume then becomes live; continue taking off-server backups.

## 5. Create the production `.env` file on the server

Log in as `deploy` and create the production environment file. Replace every example value before saving it:

```bash
ssh -i ./taskmanager_deploy deploy@SERVER_PUBLIC_IP
cd /opt/taskmanager
touch .env
nano .env
```

Use this content as the starting point (the checked-in `.env.example` has the same variable names):

```dotenv
GHCR_IMAGE=ghcr.io/LOWERCASE_GITHUB_OWNER/taskmanager-app
IMAGE_TAG=latest
DOMAIN=tasks.example.com

POSTGRES_DB=taskmanager
POSTGRES_USER=taskmanager
POSTGRES_PASSWORD=REPLACE_WITH_OUTPUT_OF_openssl_rand_hex_32

JWT_SECRET_KEY=REPLACE_WITH_OUTPUT_OF_openssl_rand_base64_48
SEED_SUPERADMIN_EMAIL=admin@example.com
SEED_SUPERADMIN_PASSWORD=ReplaceWithStrongPassword123
SEED_SUPERADMIN_USERNAME=superadmin
DATABASE_INITIALIZE_ON_STARTUP=true
SEED_CREATE_SUPERADMIN=true
SEED_DEMO_DATA=false

RAZORPAY_KEY_ID=
RAZORPAY_KEY_SECRET=
RAZORPAY_WEBHOOK_SECRET=
EMAIL_API_KEY=
EMAIL_FROM_ADDRESS=
EMAIL_FROM_NAME=TaskManager
```

Generate the two secrets in another server terminal; paste their outputs into `.env`:

```bash
openssl rand -hex 32
openssl rand -base64 48
```

For `GHCR_IMAGE`, use the lowercase GitHub account or organization that owns the repository. Use a strong unique administrator password with at least 12 characters, including uppercase, lowercase, and digits. The generated database password uses safe hexadecimal characters. Protect the Resend API key as a secret. Validate with `docker compose --env-file .env config --quiet`. Do not print the expanded Compose configuration because it includes secrets. Protect the file:

```bash
chmod 0600 /opt/taskmanager/.env
```

`SEED_CREATE_SUPERADMIN=true` creates the initial platform administrator only when none exists. After the first successful deployment and login, edit `.env` and set it to `false`. Keep `DATABASE_INITIALIZE_ON_STARTUP=true` so the app applies its EF Core migrations at startup. Keep `SEED_DEMO_DATA=false`; the demo seed contains fixed sample credentials.

## 6. Add GitHub Actions secrets

In GitHub, open **Repository → Settings → Secrets and variables → Actions → New repository secret**. Add:

| Secret name | Value |
|---|---|
| `DEPLOY_HOST` | Server public IP or SSH DNS name |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | Entire private key file `taskmanager_deploy`, including the `BEGIN` and `END` lines |
| `DEPLOY_PORT` | `22` (optional; workflow defaults to 22 if omitted) |
| `DEPLOY_PATH` | `/opt/taskmanager` |
| `DEPLOY_HOST_FINGERPRINT` | Verified SHA-256 SSH host-key fingerprint (instructions below) |

Get the fingerprint from the server console, or from a trusted SSH session to the server:

```bash
ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub -E sha256
```

Copy the value beginning `SHA256:` into `DEPLOY_HOST_FINGERPRINT`. The same fingerprint check is applied to both file transfer and remote commands. The action gets a short-lived `GITHUB_TOKEN` with package read access to pull the private image; no personal access token or database/app secret belongs in GitHub.

No other repository variable is needed. `GHCR_IMAGE` and all application credentials stay in `/opt/taskmanager/.env` on the server.

## 7. Check repository and workflow settings

1. Ensure the default branch is named `main`, or change `.github/workflows/ci.yml` to your deployment branch.
2. Confirm **Settings → Actions → General → Actions permissions** allows the referenced GitHub and Appleboy actions. If the repository or organization restricts actions, allow `actions/checkout`, `docker/setup-buildx-action`, `docker/login-action`, `docker/build-push-action`, `appleboy/scp-action`, and `appleboy/ssh-action`.
3. The workflow declares narrow per-job token permissions: `packages: write` to publish and `packages: read` on the deploy job. If an organization policy overrides these, allow the workflow token to publish packages.
4. Commit and push the deployment files on a branch, then open a pull request. The PR run builds the Docker image but does not publish it or connect to the server.

On merge/push to `main`, the workflow publishes `ghcr.io/<owner>/taskmanager-app:<full-commit-sha>` and `:latest`, transfers the current `compose.yaml`, authenticates to GHCR using the job token, and runs `docker compose pull` and `docker compose up -d`. The first run may create the GHCR package under the repository's visibility policy; the deployment job uses package read permission to pull it even when it is private.

## 8. First deployment and checks

Push the deployment changes to `main` after all GitHub secrets and the server `.env` are in place. In GitHub, open **Actions → build-and-deploy-docker** and inspect the run. It must finish both the image build and deploy jobs successfully.

On the server, check the containers and logs:

```bash
cd /opt/taskmanager
docker compose ps
docker compose logs --tail=200 app
docker compose logs --tail=100 db
```

The app is bound to `127.0.0.1:8080`, so it is not directly exposed to the Internet. Install the tracked Nginx site template and replace its example domain:

```bash
# Run on your computer from the repository root:
scp -i ./taskmanager_deploy nginx/taskmanager.conf deploy@SERVER_PUBLIC_IP:/opt/taskmanager/taskmanager.nginx.conf

# Then run on the server:
sudo cp /opt/taskmanager/taskmanager.nginx.conf /etc/nginx/sites-available/taskmanager
sudo nano /etc/nginx/sites-available/taskmanager
```

Set `server_name` to the exact domain in `.env`, save, and enable the site:

```bash
sudo ln -s /etc/nginx/sites-available/taskmanager /etc/nginx/sites-enabled/taskmanager
sudo nginx -t
sudo systemctl reload nginx
```

Issue the certificate and let Certbot configure HTTPS and the HTTP-to-HTTPS redirect:

```bash
sudo certbot --nginx --redirect --agree-tos --no-eff-email --email ops@example.com -d tasks.example.com
sudo nginx -t
sudo systemctl reload nginx
sudo certbot renew --dry-run
systemctl status certbot.timer --no-pager
```

Replace the email and domain arguments with the actual values. Certbot installs its renewal timer; the dry run confirms renewal works. Do not replace the Certbot-managed TLS directives manually. The app's external webhook URL, if used, is `https://tasks.example.com/api/billing/webhook`.

Open `https://tasks.example.com/health/live` and `https://tasks.example.com/health/ready`. The first confirms the process is reachable; readiness checks PostgreSQL. Then open the site and sign in with the initial superadmin credentials. Immediately set `SEED_CREATE_SUPERADMIN=false` in `.env` and restart:

```bash
docker compose up -d app
```

Nginx accepts public traffic on ports 80/443 and proxies to the app on localhost port 8080. PostgreSQL port 5432 is not published to the host. PostgreSQL persists across container updates in the `postgres_data` named volume.

## 9. Environment variable reference

Docker Compose maps these server `.env` names to ASP.NET Core configuration keys:

| `.env` name | Application key | Required? | Purpose |
|---|---|---:|---|
| `POSTGRES_DB` | `ConnectionStrings:DefaultConnection` | Yes | PostgreSQL database name |
| `POSTGRES_USER` | `ConnectionStrings:DefaultConnection` | Yes | PostgreSQL app/database user |
| `POSTGRES_PASSWORD` | `ConnectionStrings:DefaultConnection` | Yes | PostgreSQL password |
| `JWT_SECRET_KEY` | `JwtSettings:SecretKey` | Yes | HMAC JWT signing key; must be at least 32 characters |
| `JWT_ISSUER` | `JwtSettings:Issuer` | No | Defaults to `TaskManagerAPI` |
| `JWT_AUDIENCE` | `JwtSettings:Audience` | No | Defaults to `TaskManagerClient` |
| `DOMAIN` | `App:PublicBaseUrl`, `Cors:AllowedOrigins` | Yes | Public HTTPS site URL and allowed browser origin |
| `DATABASE_INITIALIZE_ON_STARTUP` | `Database:InitializeOnStartup` | Yes | Defaults to `true`; applies EF migrations and seeds plans |
| `SEED_CREATE_SUPERADMIN` | `Seed:CreateSuperAdmin` | Yes | `true` for initial admin creation; set `false` afterwards |
| `SEED_DEMO_DATA` | `Seed:SeedDemoData` | Yes | Keep `false` in production |
| `SEED_SUPERADMIN_USERNAME` | `Seed:SuperAdminUsername` | Yes | Initial administrator username |
| `SEED_SUPERADMIN_EMAIL` | `Seed:SuperAdminEmail` | Yes | Initial administrator email |
| `SEED_SUPERADMIN_PASSWORD` | `Seed:SuperAdminPassword` | Yes for first admin | Initial administrator password |
| `RAZORPAY_KEY_ID` | `Razorpay:KeyId` | Optional | Razorpay API key ID |
| `RAZORPAY_KEY_SECRET` | `Razorpay:KeySecret` | Optional | Razorpay API secret |
| `RAZORPAY_WEBHOOK_SECRET` | `Razorpay:WebhookSecret` | Optional | Razorpay webhook signature secret |
| `EMAIL_API_KEY` | `Email:ApiKey` | Optional | Resend API key; required to send email, store as a secret |
| `EMAIL_FROM_ADDRESS` | `Email:FromAddress` | Optional | Sender address on a domain verified in Resend; required to send email |
| `EMAIL_FROM_NAME` | `Email:FromName` | Optional | Sender display name; defaults to `TaskManager` |
| `GHCR_IMAGE` / `IMAGE_TAG` | Compose only | Yes | Image location and deployed commit tag |

If the site is also accessed from other browser origins, add each allowed origin in `compose.yaml` as `Cors__AllowedOrigins__1`, `__2`, and so on, then push to `main`. Keep every origin as an exact `https://` origin with no path or trailing slash.

## 10. Updating and operating the server

Every push to `main` deploys the new image. To redeploy the current image manually:

```bash
cd /opt/taskmanager
docker compose pull app
docker compose up -d
docker compose ps
```

To follow logs:

```bash
docker compose logs -f app
```

To stop the app without deleting database data:

```bash
docker compose down
```

Do not run `docker compose down -v` unless you intentionally want to delete the PostgreSQL volume. The `.env` file, Nginx configuration, and database volume are not overwritten by GitHub Actions.

### Back up PostgreSQL

Create a compressed database dump before schema changes and on a regular schedule. This command writes outside the container:

```bash
cd /opt/taskmanager
docker compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' | gzip > "/opt/taskmanager/backups/taskmanager-$(date +%F-%H%M%S).sql.gz"
```

Copy backups off-server; a backup on the same disk does not protect against disk or server loss. To restore, stop the app, create a fresh DB volume/server, start only PostgreSQL, then load a reviewed dump:

```bash
docker compose stop app
gzip -dc /path/to/backup.sql.gz | docker compose exec -T db sh -c 'psql -U "$POSTGRES_USER" "$POSTGRES_DB"'
docker compose up -d app
```

Practice restore on a separate database before relying on backups.

## 11. Troubleshooting

- **GitHub cannot connect:** verify `DEPLOY_HOST`, port 22 at the provider firewall, the public key in `/home/deploy/.ssh/authorized_keys`, and `DEPLOY_HOST_FINGERPRINT`.
- **`docker compose` permission denied:** log out and back in after adding `deploy` to the Docker group; check `id` and `docker ps`.
- **GHCR `denied`/unauthorized:** ensure the publishing job has `packages: write`, the deploy job has `packages: read`, and `GHCR_IMAGE` matches the lower-case image name generated in the workflow.
- **GHCR package permission error:** in the package's **Package settings → Manage Actions access**, grant this repository access if the package is private and not automatically linked to it.
- **Certbot cannot issue a certificate:** verify DNS resolves to this server, provider/UFW firewalls allow ports 80 and 443, and Nginx loads the matching `server_name`; inspect `sudo nginx -t` and `/var/log/letsencrypt/letsencrypt.log`.
- **Nginx returns 502:** verify the app container is running and `curl http://127.0.0.1:8080/health/live` works on the server.
- **App starts but readiness is unhealthy:** inspect `docker compose logs db app`; check the database settings in `.env` and wait for the database health check. Startup logs report migration failures.
- **Login token errors:** `JWT_SECRET_KEY` must remain stable across restarts and be at least 32 characters. Changing it invalidates tokens already issued.
- **Email or billing unavailable:** configure the Resend API key and verified sender address, or the corresponding Razorpay credentials, in `.env` and restart `app`. Without Resend settings, email jobs are logged as skipped.

## Files this deployment uses

- `Dockerfile` — builds and runs the .NET 10 web host.
- `.github/workflows/ci.yml` — PR build, GHCR publish, and SSH deployment.
- `compose.yaml` — app and PostgreSQL services with a persistent database volume. The app port is bound to host loopback only.
- `nginx/taskmanager.conf` — host Nginx reverse proxy template, including SignalR/WebSocket headers.
- `.env.example` — variable template; never copy production secrets into Git.
