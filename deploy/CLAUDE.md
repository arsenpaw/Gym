# Deploy (Docker / docker compose)

This file covers only deployment. Every service runs as a Docker container, and docker compose runs the whole stack.

## Layout

- `deploy/docker-compose.yml`: the stack. Its build contexts point to the services (`../api`).
- Each Dockerfile lives next to its service: `api/Dockerfile`, `ui/Dockerfile`.
- `deploy/.env`: settings for compose. Ignored by git. Copy it from `deploy/.env.example`.

## Services

- `api`: the C# .NET API, on port `8080`.
  - Needs `AUTH0_DOMAIN` and `AUTH0_AUDIENCE` in `deploy/.env`. Compose refuses to start without them.
  - Health check: `GET /health`.
  - Runs as the non-root `app` user.
- `ui`: the React app served by unprivileged nginx, on port `8081`.
  - Built with `AUTH0_DOMAIN`, `AUTH0_AUDIENCE` and `AUTH0_UI_CLIENT_ID` from `deploy/.env` as build args. They are baked into the JS, so rebuild after changing them.
  - nginx proxies `/api/` to `api:8080`, so the browser talks to one origin and the API needs no CORS.
  - Starts after the API is healthy.
- Database: SQL Server, added when the API switches off in-memory storage. The API then needs `ConnectionStrings__FitnessClub`.

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

## Notes

- The API container runs in Production, so the OpenAPI document, Scalar page and Hangfire dashboard aren't available there.
- Data is in memory until a database is added. Restarting the container wipes it.
- Still to decide: environments, TLS in front of the UI, and volumes for the database.
