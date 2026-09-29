# Deploy (Docker / docker compose)

This file covers only deployment. Every service runs as a Docker container, and docker compose runs the whole stack.

## Layout

- The compose file(s) live in `deploy/`.
- Each Dockerfile lives next to its service: `api/Dockerfile` and `ui/Dockerfile`. Compose build contexts point to `../api` and `../ui`.

## Services (planned)

- `api`: the C# .NET API.
- `ui`: the React SPA.
- A database service. The engine is TBD.

## Commands

These work once the compose file exists. Run them from the repo root:

```sh
docker compose -f deploy/docker-compose.yml up --build
docker compose -f deploy/docker-compose.yml down
```

## TBD

Environments, env/secrets handling, ports, volumes, reverse proxy.
