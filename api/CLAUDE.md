# API (C# .NET)

Backend API for the fitness club system. It is the only owner of domain logic and data. See the root `CLAUDE.md` for the project overview. The designs are in `docs/Code/Specs/2026-10-05-api-foundation-design.md` and `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`.

## Stack

- .NET 10, ASP.NET Core controllers, Clean Architecture with DDD aggregates, repositories and a unit of work
- EF Core: InMemory now, SQL Server later
- Auth0 JWT logins with roles
- Hangfire for scheduled jobs
- OpenAPI + Scalar API docs
- xUnit v3 tests on Microsoft.Testing.Platform
- Architecture rules: NetArchTest + Roslyn in `tests/FitnessClub.ArchitectureTests`

Package versions live only in `Directory.Packages.props`. Never put a `Version` on a `PackageReference`. `Newtonsoft.Json` is pinned to 13.0.4 because Hangfire.Core would otherwise pull a vulnerable 11.x (NU1903).

## Commands

Run from `api/`:

```sh
dotnet build
dotnet test                                                   # all tests
dotnet test --project tests/FitnessClub.UnitTests             # one project
dotnet test --project tests/FitnessClub.ArchitectureTests     # layer, boundary and DDD rules
dotnet test --project tests/FitnessClub.UnitTests --filter-class "FitnessClub.UnitTests.Domain.MembershipPlanTests"
dotnet test --project tests/FitnessClub.IntegrationTests --filter-method "*Create_as_admin_returns_201_with_location"
dotnet run --project src/FitnessClub.Api                      # http://localhost:5080 (Development)
```

- **Filters need `--project`.** Filters like `--filter-class` and `--filter-method` only work together with `--project`. Run against the whole solution, the test project with no matching tests fails with "zero tests ran".
- **Development URLs:** `/scalar` (API docs), `/openapi/v1.json`, `/hangfire` (from your own machine only), `/health`.
- **Running locally needs Auth0 settings,** otherwise startup fails with `OptionsValidationException`:

```sh
dotnet user-secrets --project src/FitnessClub.Api set "Auth0:Domain" "<tenant>.eu.auth0.com"
dotnet user-secrets --project src/FitnessClub.Api set "Auth0:Audience" "https://api.fitnessclub"
```

## Layout

```
src/FitnessClub.Domain          aggregates, value objects (SharedKernel), domain services, repository interfaces; BCL only
src/FitnessClub.Application     I*Service + internal services, request/response records, IUnitOfWork, Roles
src/FitnessClub.Infrastructure  internal DbContext, configurations, repositories, UnitOfWork, Hangfire
src/FitnessClub.Api             controllers, Auth0 setup, exception → problem details, Program.cs
tests/FitnessClub.UnitTests     Domain rules + Application services against fakes (no Infrastructure)
tests/FitnessClub.IntegrationTests  HTTP tests and repository round-trips via FitnessClubApiFactory
tests/FitnessClub.ArchitectureTests layer, boundary, DDD and no-comment rules
```

Which project may reference which: Domain ← Application ← Infrastructure ← Api. Domain uses only the BCL. Application uses only Domain and DI abstractions, never EF Core or `IQueryable`. Api uses Infrastructure only in `Program` and Domain only to map `DomainException`. `FitnessClub.ArchitectureTests` enforces all of this.

## Conventions

- **Adding a feature area:**
  1. Aggregate root (sealed, private setters, factory method) and `I{Root}Repository` in Domain. Child entities and value objects stay inside the aggregate.
  2. `DbSet` on `FitnessClubDbContext`, an `IEntityTypeConfiguration` (owned types for children), and an internal `{Root}Repository : Repository<{Root}>` registered in `AddInfrastructure()`.
  3. `I{Name}Service` plus an internal `{Name}Service` in Application, registered in `AddApplication()`. Load aggregates through repositories, call domain methods, then call `IUnitOfWork.SaveChangesAsync` once.
  4. A controller in Api that injects only the `I{Name}Service`.
- **No comments** in C# code. `SourceCodeTests` fails on any `//`, `/* */` or `///`.
- **Concurrency:** every aggregate root has a shadow `Version` token, re-stamped when the root or an owned child changes. A stale save becomes `ConflictException` → 409.
- **Validation is two layers:**
  - DataAnnotations on request records check the shape of input. `[ApiController]` returns 400 automatically.
  - Entities check their own rules and throw `DomainException`.
- **Errors:** `ExceptionToProblemDetailsHandler` maps `DomainException` → 400, `NotFoundException` → 404, `ConflictException` → 409, and anything else → 500 with a generic message. Every error body is `application/problem+json`.
- **Auth:**
  - Every endpoint requires a logged-in user (fallback policy). Public endpoints must say `.AllowAnonymous()`.
  - Controllers use `[Authorize(Roles = ...)]` with the `Roles` constants. A method-level `[Authorize]` adds to the class-level one.
- **Time:** use the injected `TimeProvider`, never `DateTime.UtcNow`. Domain methods take `DateTimeOffset now` and read dates in its offset, so pass club-local time.
- **InMemory limits:** it doesn't enforce unique indexes or relationships, and it has no transactions. Check uniqueness in services, and still configure the indexes for SQL Server.
- **Recurring jobs** are registered only in `Infrastructure/BackgroundJobs/RecurringJobs.Register`, which `UseInfrastructureAsync` calls at startup.
- **Integration tests:**
  - `factory.CreateClientWithRoles(Roles.Admin)` signs in through the `X-Test-Roles` header. `factory.CreateClient()` is anonymous.
  - Each test class gets its own InMemory database, but tests in the same class share it, so use unique names.
  - Repository tests derive from `PersistenceTestBase`. Each of its helpers runs in its own DI scope.
- **Warnings are errors.** In tests, pass `TestContext.Current.CancellationToken` to every async call.

## Persistence switch

- **`ConnectionStrings:FitnessClub` empty:** EF Core and Hangfire both use in-memory storage. Data is lost on restart.
- **Connection string set:** both use SQL Server. At startup, `UseInfrastructureAsync()` runs `Database.MigrateAsync()`.
- **Before the first switch**, create the initial migration. Run from `api/`:

  ```sh
  dotnet ef migrations add InitialCreate --project src/FitnessClub.Infrastructure --startup-project src/FitnessClub.Api
  ```

  This requires `dotnet tool install --global dotnet-ef` and a connection string in user secrets. EF refuses to migrate while the model has changes without a migration.

## Auth0 setup (one time)

1. **Create an API.** Its identifier becomes `Auth0:Audience`. In its settings, turn on **RBAC**.
2. **Create roles** `Admin`, `Receptionist`, `Trainer`, and assign them to users.
3. **Add a Post-Login Action** (Actions → Triggers → post-login) that puts the roles into the token:

   ```js
   exports.onExecutePostLogin = async (event, api) => {
     api.accessToken.setCustomClaim('https://fitnessclub/roles', event.authorization?.roles ?? []);
   };
   ```

4. **Settings:**
   - `Auth0:Domain` is the tenant domain, without `https://`.
   - `Auth0:RolesClaim` defaults to `https://fitnessclub/roles`.
   - Machine-to-machine test tokens have no roles, so they get 403 on role-protected endpoints.

## Hangfire

- Storage follows the persistence switch: in memory now, SQL Server when the connection string is set.
- The dashboard is at `/hangfire`, only in Development and only from your own machine.
