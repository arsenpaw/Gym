# Deploy (Docker / docker compose)

This file covers only deployment. Every service runs as a Docker container, and docker compose runs the whole stack.

## Layout

- `deploy/docker-compose.yml`: the stack. Its build contexts point to the services (`../api`).
- Each Dockerfile lives next to its service: `api/Dockerfile`, `ui/Dockerfile`.
- `deploy/.env`: settings for compose. Ignored by git. Copy it from `deploy/.env.example`.

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
- `ui`: the Auth0 values are build args baked into the JS, so rebuild (`up --build`) after changing them. nginx proxies `/api/` to `api:8080`, so the browser talks to one origin and the API needs no CORS.

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
| `ASPNETCORE_ENVIRONMENT` | no | `Production` | `Development` turns on OpenAPI, Scalar and the Hangfire dashboard |
| `API_LOG_LEVEL` | no | `Information` | `Logging__LogLevel__Default` |
| `AUTH0_ROLES_CLAIM` | no | `https://fitnessclub/roles` | `Auth0__RolesClaim` |
| `EXPIRY_NOTICE_DAYS` | no | `7` | `Notifications__ExpiryNoticeDays` (1–60) |

## Commands

Run from the repo root:

```sh
cp deploy/.env.example deploy/.env   # first time only, then fill in real Auth0 values
docker compose -f deploy/docker-compose.yml up --build -d
docker compose -f deploy/docker-compose.yml ps
docker compose -f deploy/docker-compose.yml logs -f api
docker compose -f deploy/docker-compose.yml logs -f ui
docker compose -f deploy/docker-compose.yml down
```

- To run only the database for a locally run API (`dotnet run`): `docker compose -f deploy/docker-compose.yml up -d db`.

## Notes

- By default the API container runs in Production, so the OpenAPI document, Scalar page and Hangfire dashboard aren't available there.
- "Today" and the seeded dates follow `CLUB_TIME_ZONE`. The seed runs once, when the database is first created, so changing the zone later doesn't move existing data.
- Still to decide: environments and TLS in front of the UI.
