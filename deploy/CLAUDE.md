# Deploy (Docker / docker compose)

This file covers only deployment. Every service runs as a Docker container, and docker compose runs the whole stack.

## Layout

- `deploy/docker-compose.yml`: the stack. Its build contexts point to the services (`../api`).
- Each Dockerfile lives next to its service: `api/Dockerfile`, `ui/Dockerfile`.
- `deploy/.env`: settings for compose. Ignored by git. Copy it from `deploy/.env.example`.
- `deploy/docker-compose.server.yml`: the stack for a server behind Traefik (Dokploy), where every variable is required (see "Server").
- `deploy/.env.server`: settings for the server stack. Ignored by git. Copy it from `deploy/.env.server.example`.

## Services

| Service | Image | Host port | Networks | Starts after |
|---------|-------|-----------|----------|--------------|
| `db` | SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`) | `127.0.0.1:${DB_PORT:-1433}` | `backend` | — |
| `api` | built from `api/Dockerfile` | `${API_PORT:-8080}` | `backend`, `frontend` | `db` healthy |
| `ui` | built from `ui/Dockerfile` (unprivileged nginx) | `${UI_PORT:-8081}` | `frontend` | `api` healthy |

- Every service has a health check and `restart: unless-stopped`.
- **Networks:** `ui` is only on `frontend`, so it can reach `api` but not `db`. `db` is only on `backend`.
- `db`: data lives in the `db-data` volume. `docker compose ... down -v` wipes it, and the next API start migrates and seeds again. The image is amd64 only, so on Apple silicon it runs under emulation. The host port binds to `127.0.0.1` only, so a locally run API (`dotnet run`) can use it but other machines can't.
- `api`: compose builds `ConnectionStrings__FitnessClub` from `DB_NAME` and `DB_SA_PASSWORD` (pointing at `db`). At startup the API applies the EF migrations, including the mock data (see `api/CLAUDE.md`). It runs as the non-root `app` user. Health check: `GET /health`.
- `ui`: the Auth0 values are build args baked into the JS, so rebuild (`up --build`) after changing them. nginx proxies `/api/` to `fitnessclub-api:8080` (a network alias of `api`, unique even on a shared proxy network), so the browser talks to one origin and the API needs no CORS.

## Variables (`deploy/.env`)

`deploy/.env.example` lists them all with comments. Compose stops and names the first missing required one.

| Variable | Required | Default | Used for |
|----------|----------|---------|----------|
| `AUTH0_DOMAIN` | yes | — | `Auth0__Domain` (api) and `VITE_AUTH0_DOMAIN` (ui build) |
| `AUTH0_AUDIENCE` | yes | — | `Auth0__Audience` (api) and `VITE_AUTH0_AUDIENCE` (ui build) |
| `AUTH0_UI_CLIENT_ID` | yes | — | `VITE_AUTH0_CLIENT_ID` (ui build), the Auth0 SPA client id |
| `DB_SA_PASSWORD` | yes | — | `MSSQL_SA_PASSWORD` (db) and the API connection string. SQL Server's complexity rules apply. No `;`. |
| `UI_PORT` / `API_PORT` / `DB_PORT` | no | `8081` / `8080` / `1433` | Host ports |
| `DB_NAME` | no | `FitnessClub` | Database the API creates and migrates |
| `MSSQL_PID` | no | `Developer` | SQL Server edition or product key |
| `CLUB_TIME_ZONE` | no | `UTC` | `TZ` of the api container: club-local "today", reports and the 08:00 expiry job (IANA name, e.g. `Europe/Kyiv`) |
| `ASPNETCORE_ENVIRONMENT` | no | `Production` | `Development` turns on OpenAPI, Scalar and the Hangfire dashboard. The example sets `Development`. |
| `API_LOG_LEVEL` | no | `Information` | `Logging__LogLevel__Default` |
| `AUTH0_ROLES_CLAIM` | no | `https://fitnessclub/roles` | `Auth0__RolesClaim` |
| `EXPIRY_NOTICE_DAYS` | no | `7` | `Notifications__ExpiryNoticeDays` (1–60) |
| `SMTP_HOST` | no | empty | `Smtp__Host`. Empty means expiry notices are only logged. Set (e.g. `smtp.gmail.com`) to email them |
| `SMTP_PORT` | no | `587` | `Smtp__Port`. 587 = STARTTLS, 465 = SSL |
| `SMTP_USERNAME` / `SMTP_PASSWORD` | no | empty | `Smtp__Username` / `Smtp__Password`. For Gmail, the address and an app password |
| `SMTP_FROM_ADDRESS` | if `SMTP_HOST` is set | empty | `Smtp__FromAddress`. Without it, a set host fails startup |
| `SMTP_FROM_NAME` | no | `Fitness Club` | `Smtp__FromName` |

## Commands

Run from the repo root:

```sh
cp deploy/.env.example deploy/.env   # first time only; works as is for local dev (dev Auth0 tenant)
docker compose -f deploy/docker-compose.yml up --build -d
docker compose -f deploy/docker-compose.yml ps
docker compose -f deploy/docker-compose.yml logs -f api
docker compose -f deploy/docker-compose.yml logs -f ui
docker compose -f deploy/docker-compose.yml down
```

- To run only the database for a locally run API (`dotnet run`): `docker compose -f deploy/docker-compose.yml up -d db`.

## Server (`docker-compose.server.yml`)

The server stack for Dokploy / Traefik. It runs the same `db`, `api` and `ui` with the same health checks, and builds the images on the server from the repo (tagged `fitnessclub-api:latest` and `fitnessclub-ui:latest`). The differences:

- **No defaults.** Every setting uses `${VAR:?set it in .env - <why>}`, so compose stops and names the first missing or empty variable. `deploy/.env.server.example` lists them all with comments.
- **Routing:** Traefik labels route `GYM_UI_HOST` to `ui:8080` and `GYM_API_HOST` to `api:8080` on the `GYM_TRAEFIK_ENTRYPOINT` entrypoint. Neither publishes a host port.
- **Networks:** `internal` (bridge) for all three, plus the external `GYM_PROXY_NETWORK` (Dokploy's `dokploy-network`) for `ui` and `api`. `db` is only on `internal`. nginx reaches the API as `fitnessclub-api`, so a service named `api` in another stack on the proxy network can't catch the requests.
- **Database port:** `GYM_DB_BIND:GYM_DB_PORT` → 1433, meant for `127.0.0.1` and an SSH tunnel.
- **Allowed hosts:** `AllowedHosts` is built from `GYM_UI_HOST;GYM_API_HOST;localhost`. `localhost` is for the health check.
- **More settings than local:** `API_LOG_LEVEL_ASPNETCORE` (`Logging__LogLevel__Microsoft.AspNetCore`), and `CLUB_TIME_ZONE` also sets `TZ` on `db`.
- **Email is required:** every `SMTP_*` variable must be set, so the server always emails expiry notices. An SMTP relay without a login isn't supported there.
- **Edition:** the example sets `MSSQL_PID=Express`, because the Developer edition isn't licensed for production.
- **Logs:** the `local` driver, at most 5 × 10 MB per container.
- **Project name:** `fitnessclub-deploy`, so its containers and `db-data` volume are separate from the local stack's.

**Example:** the live deployment runs this stack with `GYM_UI_HOST=gym.arsenhome.win` and `GYM_API_HOST=gym-api.arsenhome.win`, so the UI is https://gym.arsenhome.win and the API health check is https://gym-api.arsenhome.win/health. Test logins (dev Auth0 tenant, see `docs/Code/Demo Data and Users.md`):

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin.demo@example.com` | `4T9w8XKtgGxe#93` |
| Receptionist | `reception.demo@example.com` | `7UNY3Nqjisgm#73` |
| Trainer | `olena.kovalenko@example.com` | `V2adqbrHTize#11` |

In Dokploy, point a Compose service at `deploy/docker-compose.server.yml` and paste the variables into its Environment tab. By hand, from the repo root (the `GYM_PROXY_NETWORK` network must already exist):

```sh
cp deploy/.env.server.example deploy/.env.server   # first time only, then fill in every value
docker compose -f deploy/docker-compose.server.yml --env-file deploy/.env.server up --build -d
docker compose -f deploy/docker-compose.server.yml --env-file deploy/.env.server ps
docker compose -f deploy/docker-compose.server.yml --env-file deploy/.env.server logs -f api
```

## Notes

- Without `ASPNETCORE_ENVIRONMENT` the API container runs in Production, so the OpenAPI document, Scalar page and Hangfire dashboard aren't available there.
- "Today" and the seeded dates follow `CLUB_TIME_ZONE`. The seed runs once, when the database is first created, so changing the zone later doesn't move existing data.
- Still to decide: a registry so the server pulls images instead of building them.
