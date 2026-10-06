---
tags: [spec, api]
status: implemented
date: 2026-10-05
---

# API Foundation: Design Spec

Sub-project 1 of 5 for the backend. Requirements source: [[Fitness Club System]].

| # | Sub-project | Status |
|---|---|---|
| 1 | **Foundation** (this spec) | implemented |
| 2 | Clients, memberships, visits | not started |
| 3 | Trainers, schedules, rooms, bookings | not started |
| 4 | Membership expiry notifications (Hangfire job) | not started |
| 5 | Reports | not started |

## Goal

Set up the skeleton of the `api/` backend that every later sub-project builds on. That covers the solution layout and the dependency rules between layers, persistence, authentication and roles, background jobs, error handling, tests, and Docker. **Membership plans** are built as one complete working example to prove the skeleton end to end.

**Done when:**
- `dotnet build` and `dotnet test` pass in `api/`.
- `docker compose -f deploy/docker-compose.yml up --build` starts the API, and `GET /health` returns 200.
- The membership-plan endpoints work as specified, with role checks: 401 without a token, 403 with the wrong role.
- The Hangfire server runs, and its dashboard opens in Development.

## Stack decisions

| Concern | Choice |
|---|---|
| Runtime | .NET 10, ASP.NET Core **controllers** (`[ApiController]`) |
| Architecture | Clean Architecture: Domain → Application → Infrastructure → Api |
| Validation | DataAnnotations attributes on request models. `[ApiController]` returns 400 automatically for invalid input |
| Persistence | EF Core, database built from the C# entities. **InMemory** provider now |
| Future database | **SQL Server**: adding `ConnectionStrings:FitnessClub` switches the provider (see Persistence) |
| Auth | JWT bearer tokens issued by **Auth0**, with access controlled by role |
| Background jobs | **Hangfire**, storing its data in memory now (`Hangfire.InMemory`) |
| API docs | Built-in OpenAPI document (`Microsoft.AspNetCore.OpenApi`). Interactive docs page only in Development (Scalar) |
| Tests | xUnit v3. Integration tests use `WebApplicationFactory` |
| Packages | Central package management (`Directory.Packages.props`) |

Not used, on purpose (YAGNI): MediatR/CQRS, AutoMapper, FluentValidation. The repository pattern, the unit of work and the full domain model came later in [[2026-10-05-domain-model-and-architecture-design]], which replaces this spec's `IApplicationDbContext` and layering rules.

## Solution layout

```
api/
  FitnessClub.slnx
  Directory.Build.props        # net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
  Directory.Packages.props     # central package versions
  Dockerfile
  src/
    FitnessClub.Domain/          # entities, enums, domain exceptions; no package references
    FitnessClub.Application/     # use-case services, request/response models, IApplicationDbContext
    FitnessClub.Infrastructure/  # EF Core DbContext + configurations, Hangfire setup
    FitnessClub.Api/             # controllers, auth, error handling, Program.cs
  tests/
    FitnessClub.UnitTests/         # Domain + Application
    FitnessClub.IntegrationTests/  # HTTP-level tests via WebApplicationFactory
```

**Which project may reference which:**
- **Domain** references nothing.
- **Application** references Domain, plus EF Core's base package (`Microsoft.EntityFrameworkCore`) so it can see `DbSet<T>`. No database-specific provider. (superseded, see [[2026-10-05-domain-model-and-architecture-design]])
- **Infrastructure** references Application.
- **Api** references Application and Infrastructure. Infrastructure is used only to register services at startup.

Each layer has an `AddApplication()` / `AddInfrastructure(configuration)` extension method that sets up its own services, and `Program.cs` calls them.

## Domain

- Every entity inherits `Entity`, which has a `Guid Id` generated in the constructor.
- Entities protect their own rules. Constructors and methods check arguments and throw `DomainException` when a rule is broken. Properties have private setters, and changes go through methods.
- Code never calls `DateTime.UtcNow` directly. It uses `TimeProvider`, injected, registered as `TimeProvider.System`, and faked in tests.

### Example entity: `MembershipPlan`

| Field | Type | Rule |
|---|---|---|
| `Name` | string | required, 1–100 chars, unique (case-insensitive) |
| `Price` | decimal | > 0, the club's single currency, 2 decimal places |
| `ValidityDays` | int | 1–3650. How long a membership bought on this plan lasts |
| `VisitLimit` | int? | `null` = unlimited, otherwise 1–1000 |
| `IsActive` | bool | inactive plans can't be sold, but they're kept for history |

How the plans in the requirements map to this:
- Single visit: `ValidityDays=1, VisitLimit=1`.
- Monthly: `30, null`.
- Yearly: `365, null`.
- Visit packs (for example 10 visits in 90 days) also fit.

Plans are never deleted, only set to inactive. Memberships in sub-project 2 will refer to them.

Methods:
- `MembershipPlan.Create(name, price, validityDays, visitLimit)`
- `Update(...)`
- `Activate()`
- `Deactivate()`

## Application

- **`IApplicationDbContext`** exposes `DbSet<MembershipPlan>` and `SaveChangesAsync`. Infrastructure implements it. (superseded, see [[2026-10-05-domain-model-and-architecture-design]])
- **Use-case services**, one per feature area, for example `MembershipPlanService`. They take request models and return response models, and copy fields between models and entities by hand.
- **Request and response models** are records. Request records carry the validation attributes, such as `[Required]`, `[StringLength]` and `[Range]`, using the limits in the table above.
- **Application exceptions:**
  - `NotFoundException`: the record doesn't exist.
  - `ConflictException`: for example, a duplicate plan name.

The services check name uniqueness in code, because the InMemory provider doesn't enforce unique indexes. The unique index is still configured, so SQL Server will enforce it later.

## Persistence

- `FitnessClubDbContext`, in Infrastructure, implements `IApplicationDbContext` (superseded, see [[2026-10-05-domain-model-and-architecture-design]]). Each entity has its own `IEntityTypeConfiguration<T>` class, picked up automatically with `ApplyConfigurationsFromAssembly`.
- **Choosing the provider:**
  - `ConnectionStrings:FitnessClub` empty or missing → `UseInMemoryDatabase("FitnessClub")`.
  - Connection string set → `UseSqlServer(connectionString)`.
- The SQL Server provider package is referenced from the start, so later only the connection string needs adding.
- The InMemory provider doesn't support migrations. The first EF migration is created when SQL Server is turned on, and that isn't part of this spec. The API applies pending migrations at startup only when the relational provider is active.
- Each integration test gets its own InMemory database name, so tests don't share data.

## Authentication and roles

- **JWT validation:** `AddAuthentication().AddJwtBearer(...)` with:
  - Authority `https://{Auth0:Domain}/`
  - Audience `{Auth0:Audience}`
  - `RoleClaimType = {Auth0:RolesClaim}` (default `https://fitnessclub/roles`)
- **Roles:**
  - `Admin`: manages plans, trainers, rooms; sees reports.
  - `Receptionist`: registers clients, sells memberships, records visits.
  - `Trainer`: sees their own schedule and clients.

  The role names are string constants in `Roles` (Application). Clients don't log in. The requirements don't call for a client portal.
- **Locked by default:** a fallback policy requires a logged-in user everywhere. Each controller or action adds `[Authorize(Roles = ...)]`. The only public routes are `/health` and, in Development, the OpenAPI document and docs page.
- **Settings:** `Auth0:Domain` and `Auth0:Audience` come from `dotnet user-secrets` locally and environment variables in Docker. They're never committed.
- **One-time Auth0 setup** (done by you, written up in `api/CLAUDE.md`):
  1. Create an API whose identifier is the audience, and turn on RBAC.
  2. Create the roles `Admin`, `Receptionist` and `Trainer`.
  3. Add a Post-Login Action that copies the user's roles into the `https://fitnessclub/roles` claim of the access token.

## Background jobs (Hangfire)

- `AddHangfire(...)` with in-memory storage, plus `AddHangfireServer()`, both set up in Infrastructure.
- **Persistent storage later:** when the SQL Server connection string is set, Hangfire switches to `Hangfire.SqlServer` storage using the same connection string, chosen the same way as the EF provider. Both storage packages are referenced from the start.
- **Dashboard:** at `/hangfire`, only in Development, and only to requests from the local machine (Hangfire's default filter). Your Auth0 token can't reach it, because a browser opening the dashboard doesn't send JWT bearer tokens.
- **Scheduled jobs:** recurring jobs are registered in one place, `RecurringJobs.Register(IRecurringJobManager)` in Infrastructure, called at startup. The list starts empty, and sub-project 4 adds the expiry job.

## API surface (example: membership plans)

Base route `api/membership-plans`. All endpoints use JSON.

| Method | Route | Roles | Success | Errors |
|---|---|---|---|---|
| GET | `/` (`?includeInactive=false`) | Admin, Receptionist | 200 list | 401, 403 |
| GET | `/{id}` | Admin, Receptionist | 200 | 401, 403, 404 |
| POST | `/` | Admin | 201 + `Location` | 400, 401, 403, 409 |
| PUT | `/{id}` | Admin | 200 | 400, 401, 403, 404, 409 |
| POST | `/{id}/activate` | Admin | 204 | 401, 403, 404 |
| POST | `/{id}/deactivate` | Admin | 204 | 401, 403, 404 |

`PUT` replaces `Name`, `Price`, `ValidityDays` and `VisitLimit`. `IsActive` only changes through activate/deactivate. New plans start active. Responses include every field plus `Id`.

Plus `GET /health`, anonymous, using ASP.NET Core health checks.

## Error handling

All errors return RFC 7807 problem details (`AddProblemDetails()`). A single `IExceptionHandler` maps them:

| Source | Status |
|---|---|
| Invalid attributes on a request model | 400 (`ValidationProblemDetails`, automatic) |
| `DomainException` | 400 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| Anything else | 500, generic message. Details only in logs, never in the response |

## Docker

- **`api/Dockerfile`:** multi-stage. Build with `mcr.microsoft.com/dotnet/sdk:10.0` and run on `mcr.microsoft.com/dotnet/aspnet:10.0`. Listens on port 8080 and runs as the image's non-root `app` user.
- **`deploy/docker-compose.yml`:**
  - One `api` service, built from `../api`, mapped to `8080:8080`.
  - Auth0 settings passed in as environment variables from `deploy/.env`. The `.env` file is ignored by git. A `deploy/.env.example` is committed.
  - A health check on `/health`.
  - The `ui` and database services come in later sub-projects.

## Testing

- **Unit tests:** domain rules for `MembershipPlan`, and `MembershipPlanService` behavior (duplicate name → `ConflictException`, missing → `NotFoundException`) against the InMemory database.
- **Integration tests:**
  - `WebApplicationFactory<Program>` replaces JWT bearer with a test authentication handler that takes its roles from a request header. The tests never call Auth0.
  - They cover each endpoint's success case, 400 for invalid input, 401 without a user, 403 with the wrong role, 404, and 409.
- `/health` returns 200 without a token.
- Development follows test-first coding (red → green → refactor).

## Documentation updates (part of this work)

- **`api/CLAUDE.md`:** fill in the layout, commands (build, run, test, run a single test), the persistence switch, the Auth0 setup steps, and the Hangfire notes. Replace the TBD headings that are now decided.
- **`deploy/CLAUDE.md`:** the compose file now exists. List the `api` service and the `.env` handling.
- **Root `CLAUDE.md`:** the backend stack is no longer TBD. Point to `api/CLAUDE.md`.

## Out of scope

- Clients, memberships, visits, trainers, rooms, bookings, notifications, reports (sub-projects 2–5).
- Running against a real SQL Server, EF migrations, and adding a database service to compose.
- How the UI logs in, and CORS. Both come with the UI.
- Managing users in Auth0 from the API.
- Production hosting, HTTPS termination, reverse proxy.
