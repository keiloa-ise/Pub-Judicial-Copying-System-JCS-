# JCS — Deployment (Docker Compose)

Runs the whole stack on a fresh Ubuntu host: **SQL Server**, the **.NET 9 API**, and the
**Nginx-served SPA** (Nginx also reverse-proxies `/api` to the API).

## 1. Install Docker
```bash
sudo apt update && sudo apt install -y docker.io docker-compose-plugin
sudo usermod -aG docker $USER   # log out/in so `docker` works without sudo
```

## 2. Get the code & configure secrets
```bash
git clone <repo> jcs && cd jcs
cp .env.example .env
# Edit .env:
#   SA_PASSWORD          strong SQL Server password
#   JWT_SIGNING_KEY      openssl rand -base64 48
#   JCS_ADMIN_PASSWORD   first admin password
#   JCS_BOOTSTRAP=true   (for the FIRST run only)
```

## 3. First run (builds images, creates DB, seeds, makes the admin)
```bash
docker compose up -d --build
docker compose logs -f api      # watch for "Created initial Administrator" then "Now listening"
```
The API waits for SQL Server to be healthy, then (because `JCS_BOOTSTRAP=true`):
applies migrations → seeds the 9 decision-type templates + paragraphs → creates the admin.

## 4. Lock down the bootstrap
Edit `.env` → `JCS_BOOTSTRAP=false`, then:
```bash
docker compose up -d            # recreates the api container with bootstrap off
```

Open **http://<server-ip>/** and log in with `JCS_ADMIN_USERNAME` / `JCS_ADMIN_PASSWORD`.
Create courts, judges, users (head/copyist/reviewer) from the admin screens.

## Upgrades (new version with new migrations)
```bash
git pull
or
git -c http.sslVerify=false pull origin main

# set JCS_BOOTSTRAP=true in .env for this deploy (to apply the new migrations), then:
docker compose up -d --build
# set JCS_BOOTSTRAP=false and `docker compose up -d` again
```

## Remote DB access (SSMS / Azure Data Studio)

The `mssql` service publishes port **1433** to the host (see `docker-compose.yml`), so you can
connect from another PC on the same network.

1. Open the firewall on the server (if `ufw` is enabled):
   ```bash
   sudo ufw allow 1433/tcp
   # Tighter — restrict to one admin PC:
   # sudo ufw allow from 192.168.1.50 to any port 1433 proto tcp
   ```
2. Connect with **SSMS** (Windows) or **Azure Data Studio** (Win/macOS/Linux):

   | Field | Value |
   |-------|-------|
   | Server name | `<server-ip>,1433` (e.g. `192.168.1.160,1433`) |
   | Authentication | SQL Server Authentication |
   | Login | `sa` |
   | Password | the `SA_PASSWORD` from `.env` |
   | Encryption | Optional — tick **Trust server certificate** (self-signed) |
   | Database | `Jcs` |

Quick check without any client, on the server itself (note `-d Jcs` — otherwise you query the
`master` system database and see only built-in tables):
```bash
docker compose exec mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" -C -d Jcs -Q "SELECT name FROM sys.tables ORDER BY name"

docker compose exec mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P Ch4ngeMe_Str0ng! -C -d Jcs -Q "SELECT * FROM Judges"
```

> ⚠ This exposes the `sa` super-account on the network — acceptable on a trusted LAN, but
> restrict the firewall to your admin PC, and prefer a read-only login for day-to-day querying.
> Do **not** edit/delete rows in `AuditEntries` (the audit trail is append-only). For exposure
> beyond a trusted LAN, bind to `127.0.0.1:1433` instead and reach it over an SSH tunnel.

## Notes & production hardening
- **Arabic collation** is handled automatically: the SQL Server container sets
  `MSSQL_COLLATION=Arabic_CI_AS`, so the `Jcs` database inherits it on creation. (Don't change
  this after data exists — collation is expensive to change later.)
- **TLS**: expose `web` on 443 behind a TLS terminator, or front the stack with host Nginx /
  Caddy / a load balancer and point it at the `web` container. Don't serve plain HTTP publicly.
- **Dedicated DB login**: the compose uses `sa` for simplicity. For production, create a
  least-privilege login for the app and,  **revoke UPDATE/DELETE on `AuditEntries`**
  (the audit trail is append-only). Update `ConnectionStrings__Jcs` accordingly.
- **Backups**: back up the `mssql-data` volume (and/or scheduled SQL backups). This is a legal
  system with a permanent audit trail — treat backups as mandatory.
- **Secrets**: `.env`, `appsettings.Development.json`, and `web/.env` are gitignored. Never bake
  secrets into images; they're passed as environment variables at runtime.
- **Migrations are deliberate**: the app never auto-migrates in Production except via the
  explicit `JCS_BOOTSTRAP` flag . Alternatively generate a script on a build machine
  (`dotnet ef migrations script --idempotent`) and apply it out-of-band.

## Useful commands
```bash
docker compose ps
docker compose logs -f api
docker compose exec mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -Q "SELECT name, collation_name FROM sys.databases WHERE name='Jcs'"
docker compose down              # stop (keeps the data volume)
docker compose down -v           # stop AND delete the DB volume (destroys data)
```

## Go-live: numbering start points (FR-17)

On a real deployment, courts already have decisions whose sequential numbers were issued by the
previous (manual) process. Before users start creating copies, the **Administrator** must seed the
starting point of each auto-generated sequence so the system continues from where the manual
numbering stopped — never restarting from 1 and never colliding with already-issued numbers.

Do this from the admin screen **«ضبط بدايات الترقيم» (Numbering start points)**:

1. **Finalise room numbering policies first** (admin → «المحاكم» / «مستويات الترقيم»): for each room
   choose court level / room level / special level A–Z. Special levels are **per court**. This must
   be done before seeding رقم المتفرق, because the policy decides which counter (scope) a room uses.
2. **رقم النسخة (copy number):** for each **court** and **year**, enter the **last issued number**.
   The system will issue the next copy as `lastNumber + 1` (format `{courtCode}/{year}/{seq}`).
3. **رقم المتفرق (misc number):** for each **scope** (court level / a specific room / a special level)
   and **year**, enter the **last issued number**. Next متفرق copy in that scope continues at `+1`.
4. Repeat per year if earlier-year decisions exist (both sequences reset yearly).

Rules enforced by the server:
- The entered value is the **last issued number**; auto numbering continues at `+1`.
- A start point **cannot be set below the highest number already used** in the system for that
  court/scope+year (guards against re-issuing an existing number).
- Setting start points is **Administrator-only**.

API (admin): `GET/PUT /api/admin/numbering/copy-counters`, `GET/PUT /api/admin/numbering/misc-counters`.
