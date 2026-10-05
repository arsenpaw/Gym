# API (C# .NET)

Backend API for the fitness club system. It is the only owner of domain logic and data. See the root `CLAUDE.md` for the project overview. The design is in `docs/Code/Specs/2026-10-05-api-foundation-design.md`.

## Stack

- .NET 10, ASP.NET Core controllers, Clean Architecture
- EF Core: InMemory now, SQL Server later
- Auth0 JWT logins with roles
- Hangfire for scheduled jobs
- OpenAPI + Scalar API docs
- xUnit v3 tests on Microsoft.Testing.Platform

Package versions live only in `Directory.Packages.props`. Never put a `Version` on a `PackageReference`.

## Commands

Run from `api/`:

```sh
dotnet build
dotnet test                                                   # all tests
dotnet test --project tests/FitnessClub.UnitTests             # one project
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
src/FitnessClub.Domain          entities + rules (DomainException); no package references
src/FitnessClub.Application     services, request/response records, IApplicationDbContext, Roles
src/FitnessClub.Infrastructure  EF Core DbContext + configurations, Hangfire, RecurringJobs
src/FitnessClub.Api             controllers, Auth0 setup, exception → problem details, Program.cs
tests/FitnessClub.UnitTests     Domain + Application (services against the InMemory provider)
tests/FitnessClub.IntegrationTests  HTTP tests via FitnessClubApiFactory
```

Which project may reference which: Domain ← Application ← Infrastructure ← Api. Application may use EF Core's base package for `DbSet<T>`, but never a database-specific provider.

## Conventions

- **Adding a feature area:**
  1. Entity in Domain.
  2. `DbSet` on `IApplicationDbContext` and `FitnessClubDbContext`, plus an `IEntityTypeConfiguration`.
  3. Service in Application, registered in `AddApplication()`.
  4. Controller in Api.
- **Validation is two layers:**
  - DataAnnotations on request records check the shape of input. `[ApiController]` returns 400 automatically.
  - Entities check their own rules and throw `DomainException`.
- **Errors:** `ExceptionToProblemDetailsHandler` maps `DomainException` → 400, `NotFoundException` → 404, `ConflictException` → 409, and anything else → 500 with a generic message. Every error body is `application/problem+json`.
- **Auth:**
  - Every endpoint requires a logged-in user (fallback policy). Public endpoints must say `.AllowAnonymous()`.
  - Controllers use `[Authorize(Roles = ...)]` with the `Roles` constants. A method-level `[Authorize]` adds to the class-level one.
- **Time:** use the injected `TimeProvider`, never `DateTime.UtcNow`.
- **InMemory limits:** it doesn't enforce unique indexes or relationships. Check uniqueness in services, and still configure the indexes for SQL Server.
- **Recurring jobs** are registered only in `Infrastructure/BackgroundJobs/RecurringJobs.Register`.
- **Integration tests:**
  - `factory.CreateClientWithRoles(Roles.Admin)` signs in through the `X-Test-Roles` header. `factory.CreateClient()` is anonymous.
  - Each test class gets its own InMemory database, but tests in the same class share it, so use unique names.
- **Warnings are errors.** In tests, pass `TestContext.Current.CancellationToken` to every async call.

## Persistence switch

- **`ConnectionStrings:FitnessClub` empty:** EF Core and Hangfire both use in-memory storage. Data is lost on restart.
- **Connection string set:** both use SQL Server. At startup, `InitializeDatabaseAsync()` runs `Database.MigrateAsync()`.
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
