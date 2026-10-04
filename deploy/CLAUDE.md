# Deploy (Docker / docker compose)

This file covers only deployment. Every service runs as a Docker container, and docker compose runs the whole stack.

## Layout

- `deploy/docker-compose.yml`: the stack. Its build contexts point to the services (`../api`).
- Each Dockerfile lives next to its service: `api/Dockerfile`. The UI's comes later.
- `deploy/.env`: settings for compose. Ignored by git. Copy it from `deploy/.env.example`.

## Services

- `api`: the C# .NET API, on port `8080`.
  - Needs `AUTH0_DOMAIN` and `AUTH0_AUDIENCE` in `deploy/.env`. Compose refuses to start without them.
  - Health check: `GET /health`.
  - Runs as the non-root `app` user.
- `ui`: added with the UI.
- Database: SQL Server, added when the API switches off in-memory storage. The API then needs `ConnectionStrings__FitnessClub`.

## Commands

Run from the repo root:

```sh
cp deploy/.env.example deploy/.env   # first time only, then fill in real Auth0 values
docker compose -f deploy/docker-compose.yml up --build -d
docker compose -f deploy/docker-compose.yml ps
docker compose -f deploy/docker-compose.yml logs -f api
docker compose -f deploy/docker-compose.yml down
```

## Notes

- The API container runs in Production, so the OpenAPI document, Scalar page and Hangfire dashboard aren't available there.
- Data is in memory until a database is added. Restarting the container wipes it.
- Still to decide: environments, ports and the reverse proxy for the UI, and volumes for the database.
