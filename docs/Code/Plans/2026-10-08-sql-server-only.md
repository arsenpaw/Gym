# SQL Server Only Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make SQL Server the only database for the API and its tests: remove the EF Core InMemory and Hangfire InMemory fallbacks and the in-memory test repositories, run integration tests against a real SQL Server, map unique-index races to 409, and confirm `docker compose` starts the whole stack on a local SQL Server.

**Architecture:** The SQL Server provider, the EF repositories, the migrations and the compose stack (`db` + `api` + `ui`) already exist. What changes is that the empty-connection-string fallback goes away. The API refuses to start without `ConnectionStrings:FitnessClub`. Integration tests start one SQL Server container per test run (Testcontainers). Each `WebApplicationFactory` gets its own database, which the API migrates at startup. The factory then deletes every row so tests start empty, because the `SeedMockData` migration would otherwise put demo data in every test database.

**Tech Stack:** .NET 10, EF Core 10 SQL Server, Hangfire.SqlServer, xUnit v3 (assembly fixtures), Testcontainers.MsSql 4.15.0, Microsoft.Data.SqlClient (already transitive via EF), Docker Compose.

**Spec:** No separate spec. This plan comes from the request "docker compose which starts all services with a local MSSQL db, a real MSSQL provider and repositories, remove in-memory". The decisions are recorded under "Design decisions" below. Background: `api/CLAUDE.md` ("Persistence switch"), `deploy/CLAUDE.md`, and `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`. Line 203 of that spec postpones "mapping SQL Server unique-index violations (2601/2627) to 409" until the SQL Server switch.

## Design decisions

- **Compose already does what was asked.** `deploy/docker-compose.yml` runs `db` (SQL Server 2022), `api` (connection string → `db`, migrates and seeds at startup) and `ui` (nginx, proxies `/api/`). This plan doesn't rebuild it. Task 5 verifies it end to end. The one gap found: the local `deploy/.env` is missing `AUTH0_UI_CLIENT_ID`, so `docker compose up` currently refuses to start.
- **"Remove in-memory" covers the EF Core InMemory provider, Hangfire.InMemory, and the `InMemory*Repository` test fakes.** Service tests run the real repositories on SQL Server (Task 4).
- **No connection string is a startup error,** with a message that says how to fix it. It is read lazily inside the `AddDbContext`/`AddHangfire` callbacks, so the build-time OpenAPI generator (which never resolves the DbContext) and `WebApplicationFactory` overrides keep working.
- **Tests use Testcontainers, not the compose `db`.** `dotnet test` stays self-contained (Docker must be running) and never touches the developer's data.
- **Test databases are emptied after migrating.** The real migrations, including `SeedMockData`, run in tests just as in production. A plain SQL script then deletes all rows in `dbo` (except `__EFMigrationsHistory`), leaving Hangfire's `HangFire` schema intact. This avoids the Respawn package (YAGNI).
- **Unique-index races become 409.** Services still check uniqueness first so they can return friendly messages. `UnitOfWork` maps SQL errors 2601/2627 to `ConflictException`.

## Global Constraints

- Package versions live only in `api/Directory.Packages.props`. Never put a `Version` on a `PackageReference`.
- Warnings are errors (`TreatWarningsAsErrors`). `new MsSqlBuilder()` without an image is `[Obsolete]` in Testcontainers 4.15, so always use `new MsSqlBuilder(image)`.
- No comments in C# code (`//`, `/* */`, `///`). `SourceCodeTests` scans `src` and `tests`.
- In tests, pass `TestContext.Current.CancellationToken` to every async call.
- Filters like `--filter-class` / `--filter-method` need `--project`.
- The SQL Server image is `mcr.microsoft.com/mssql/server:2022-latest`, the same as compose. On Apple silicon it runs under amd64 emulation.
- Commit messages follow the repo style (`feat:`, `fix:`, `test:`, `docs:`) and end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Run all `dotnet` commands from `api/`.

## Review Focus

1. **Seeded demo data leaking into test databases.** The load-report tests pin the clock to 2026-10-07, and the seed schedule spans −4 to +2 weeks from today, so leftover seed rows would show up in reports. A factory database must be migrated and empty. Pinned in Task 1 (`TestDatabaseTests`).
2. **A developer runs `dotnet run` without a connection string.** They should get a startup error that names `ConnectionStrings:FitnessClub`, not a cryptic SqlClient error or silent in-memory data. Pinned in Task 2 (`Missing_connection_string_fails_at_startup`).
3. **Two clients create the same room name at the same moment.** One should get 201 and the other 409, never 500. Pinned in Task 3 (repository-level duplicate plus the concurrent HTTP test).
4. **A Debug build with no connection string.** It must still generate `ui/openapi/fitnessclub.json` and leave it unchanged. Pinned in Task 2 Step 8 (`git diff --exit-code`).
5. **Restarting the compose stack on an existing `db-data` volume.** Migrations must be a no-op and demo data must not be duplicated: the client count stays at 16. Pinned in Task 5 Step 6.

---

### Task 1: Integration tests run on SQL Server (Testcontainers)

Every integration test switches to a real SQL Server before the InMemory code is removed, so Task 2 can delete it without losing coverage.

**Files:**
- Modify: `api/Directory.Packages.props`
- Modify: `api/tests/FitnessClub.IntegrationTests/FitnessClub.IntegrationTests.csproj`
- Create: `api/tests/FitnessClub.IntegrationTests/Infrastructure/SqlServerFixture.cs`
- Create: `api/tests/FitnessClub.IntegrationTests/Infrastructure/TestDatabase.cs`
- Create: `api/tests/FitnessClub.IntegrationTests/Infrastructure/TestDatabaseTests.cs`
- Modify: `api/tests/FitnessClub.IntegrationTests/Infrastructure/FitnessClubApiFactory.cs`
- Modify: `api/tests/FitnessClub.IntegrationTests/Auth/RealJwtApiFactory.cs`

**Interfaces:**
- Consumes: nothing from other tasks.
- Produces:
  - `SqlServerFixture.NewDatabaseConnectionString(): string`. Static. Returns a connection string to the shared test container with a fresh, not yet created `InitialCatalog` (`FitnessClub_<guid>`). It throws `InvalidOperationException` if the container hasn't started.
  - `FitnessClubApiFactory.ConnectionString { get; }: string`. This factory's database. Factories derived with `WithWebHostBuilder` share it.
  - `TestDatabase.DeleteAllRows(string connectionString): void`.

- [ ] **Step 1: Add the Testcontainers package**

In `api/Directory.Packages.props`, add after the `Swashbuckle.AspNetCore.SwaggerUI` line:

```xml
    <PackageVersion Include="Testcontainers.MsSql" Version="4.15.0" />
```

In `api/tests/FitnessClub.IntegrationTests/FitnessClub.IntegrationTests.csproj`, change the first `ItemGroup` to:

```xml
  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Testcontainers.MsSql" />
  </ItemGroup>
```

Run: `dotnet restore`
Expected: succeeds with no `NU1903` (this was checked against 4.15.0 while writing the plan). If a vulnerable transitive package does appear, pin its patched version in `Directory.Packages.props`, the same way `Newtonsoft.Json` is pinned.

- [ ] **Step 2: Write the failing test**

Create `api/tests/FitnessClub.IntegrationTests/Infrastructure/TestDatabaseTests.cs`:

```csharp
using Microsoft.Data.SqlClient;

namespace FitnessClub.IntegrationTests.Infrastructure;

public class TestDatabaseTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] DomainTables =
    [
        "MembershipPlans", "Clients", "Memberships", "Visits", "Payments", "Trainers",
        "TrainerWorkingHours", "TrainerClients", "Rooms", "TrainingSessions", "Bookings", "Notifications",
    ];

    [Fact]
    public async Task Factory_database_is_migrated_with_no_rows()
    {
        _ = factory.Services;
        await using var connection = new SqlConnection(factory.ConnectionString);
        await connection.OpenAsync(Ct);

        Assert.True(await CountAsync(connection, "__EFMigrationsHistory") >= 2);
        foreach (var table in DomainTables)
            Assert.Equal(0, await CountAsync(connection, table));
    }

    private static async Task<int> CountAsync(SqlConnection connection, string table)
    {
        await using var command = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{table}]", connection);
        return (int)(await command.ExecuteScalarAsync(Ct))!;
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

Run: `dotnet build`
Expected: FAIL with `CS1061: 'FitnessClubApiFactory' does not contain a definition for 'ConnectionString'`.

- [ ] **Step 4: Add the shared SQL Server container**

Create `api/tests/FitnessClub.IntegrationTests/Infrastructure/SqlServerFixture.cs`:

```csharp
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace FitnessClub.IntegrationTests.Infrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private static string? serverConnectionString;

    private readonly MsSqlContainer container = new MsSqlBuilder(Image).Build();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        serverConnectionString = container.GetConnectionString();
    }

    public ValueTask DisposeAsync() => container.DisposeAsync();

    public static string NewDatabaseConnectionString() =>
        new SqlConnectionStringBuilder(
            serverConnectionString ?? throw new InvalidOperationException("The SQL Server test container has not started."))
        {
            InitialCatalog = $"FitnessClub_{Guid.NewGuid():N}",
        }.ConnectionString;
}
```

xUnit v3 starts assembly fixtures before it creates any class fixture, so `serverConnectionString` is set before a factory constructor reads it.

- [ ] **Step 5: Add the row-deleting helper**

Create `api/tests/FitnessClub.IntegrationTests/Infrastructure/TestDatabase.cs`:

```csharp
using Microsoft.Data.SqlClient;

namespace FitnessClub.IntegrationTests.Infrastructure;

internal static class TestDatabase
{
    private const string DeleteAllRowsSql = """
        DECLARE @tables TABLE (Name nvarchar(300));
        INSERT INTO @tables
        SELECT QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'dbo' AND t.name <> N'__EFMigrationsHistory';

        DECLARE @sql nvarchar(max) = N'';
        SELECT @sql += N'ALTER TABLE ' + Name + N' NOCHECK CONSTRAINT ALL;' FROM @tables;
        SELECT @sql += N'DELETE FROM ' + Name + N';' FROM @tables;
        SELECT @sql += N'ALTER TABLE ' + Name + N' WITH CHECK CHECK CONSTRAINT ALL;' FROM @tables;
        EXEC sp_executesql @sql;
        """;

    public static void DeleteAllRows(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand(DeleteAllRowsSql, connection);
        command.ExecuteNonQuery();
    }
}
```

It is synchronous because `WebApplicationFactory.CreateHost` is synchronous.

- [ ] **Step 6: Point the factories at SQL Server**

Replace `api/tests/FitnessClub.IntegrationTests/Infrastructure/FitnessClubApiFactory.cs` with:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitnessClub.IntegrationTests.Infrastructure;

public sealed class FitnessClubApiFactory : WebApplicationFactory<Program>
{
    public string ConnectionString { get; } = SqlServerFixture.NewDatabaseConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth0:Domain", "test.invalid");
        builder.UseSetting("Auth0:Audience", "https://api.test");
        builder.UseSetting("ConnectionStrings:FitnessClub", ConnectionString);

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        TestDatabase.DeleteAllRows(ConnectionString);
        return host;
    }

    public HttpClient CreateClientWithRoles(params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }
}
```

`base.CreateHost` returns only after `Program` has run `UseInfrastructureAsync()` (migrate, then register the recurring job) and the host has started. So the delete runs after the seed and before any test. `WithWebHostBuilder` factories (for example `ReportsApiFixture.Factory`) call this same `CreateHost` and reuse this `ConnectionString`.

In `api/tests/FitnessClub.IntegrationTests/Auth/RealJwtApiFactory.cs`, replace

```csharp
        builder.UseSetting("Database:InMemoryName", $"jwt-{Guid.NewGuid()}");
```

with

```csharp
        builder.UseSetting("ConnectionStrings:FitnessClub", SqlServerFixture.NewDatabaseConnectionString());
```

and add `using FitnessClub.IntegrationTests.Infrastructure;` to its usings. JWT tests don't read data, so this database keeps its seed rows.

- [ ] **Step 7: Run the new test to verify it passes**

Docker Desktop must be running. The first run pulls the image (about 1.5 GB).

Run: `dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.Infrastructure.TestDatabaseTests"`
Expected: PASS, 1 test.

- [ ] **Step 8: Run the whole integration suite on SQL Server**

Run: `dotnet test --project tests/FitnessClub.IntegrationTests`
Expected: all tests PASS except `HostConfigurationTests.Missing_auth0_settings_fail_at_startup`. That test builds a bare `WebApplicationFactory<Program>` with no connection string, so it still runs on InMemory and passes for now. Task 2 changes it.

Any other failure is a real SQL Server difference that InMemory hid. Fix the test data, not production code, unless the production code is actually wrong. These are the possible causes:
  - `The INSERT statement conflicted with the FOREIGN KEY constraint`: the test saved a child (visit, payment, session, booking) whose parent was never saved. Save the parent first, in the same unit of work or an earlier one.
  - `Cannot insert duplicate key row ... with unique index`: two tests in the same class reused a fixed name or phone. Use the existing `Guid.NewGuid():N` / `UniquePhone()` helpers.
  - `could not be translated`: a repository query used something only InMemory can run. Rewrite the query in the repository so it translates, and keep the test as it is.

Each fix goes in this task's commit.

- [ ] **Step 9: Commit**

```bash
git add Directory.Packages.props tests/FitnessClub.IntegrationTests
git commit -m "test: run integration tests on SQL Server via Testcontainers

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Remove the InMemory providers from the API

**Files:**
- Modify: `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`
- Modify: `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs:42-89`
- Modify: `api/src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj`
- Modify: `api/Directory.Packages.props`
- Modify: `api/tests/FitnessClub.IntegrationTests/HostConfigurationTests.cs`

**Interfaces:**
- Consumes: `SqlServerFixture.NewDatabaseConnectionString()` from Task 1.
- Produces: the API throws `InvalidOperationException` whose message contains `ConnectionStrings:FitnessClub` when the connection string is missing or blank. It is thrown the first time the DbContext or Hangfire storage is resolved, which happens during startup in `UseInfrastructureAsync()`.

- [ ] **Step 1: Write the failing test and fix the Auth0 test**

In `api/tests/FitnessClub.IntegrationTests/HostConfigurationTests.cs`, add a connection string to `Missing_auth0_settings_fail_at_startup` so it fails only because of Auth0, and add a new test below it:

```csharp
    [Fact]
    public void Missing_auth0_settings_fail_at_startup()
    {
        using var unconfigured = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Auth0:Domain", "");
            builder.UseSetting("Auth0:Audience", "");
            builder.UseSetting("ConnectionStrings:FitnessClub", SqlServerFixture.NewDatabaseConnectionString());
        });

        var exception = Assert.Throws<OptionsValidationException>(() => unconfigured.CreateClient());
        Assert.Contains("Domain", exception.Message);
        Assert.Contains("Audience", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_connection_string_fails_at_startup(string connectionString)
    {
        using var unconfigured = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Auth0:Domain", "test.invalid");
            builder.UseSetting("Auth0:Audience", "https://api.test");
            builder.UseSetting("ConnectionStrings:FitnessClub", connectionString);
        });

        var exception = Record.Exception(() => unconfigured.CreateClient());

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception.GetBaseException());
        Assert.Contains("ConnectionStrings:FitnessClub", exception.GetBaseException().Message);
    }
```

`FitnessClub.IntegrationTests.Infrastructure` is already imported in this file.

- [ ] **Step 2: Run them to verify the new one fails**

Run: `dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.HostConfigurationTests"`
Expected: `Missing_connection_string_fails_at_startup` FAILS (`Assert.NotNull() Failure`, because the API falls back to InMemory and starts). The other tests PASS.

- [ ] **Step 3: Require the connection string in `AddInfrastructure`**

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace the `AddDbContext` call with:

```csharp
        services.AddDbContext<FitnessClubDbContext>(options =>
            options.UseSqlServer(
                RequiredConnectionString(configuration),
                sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
```

Replace the `AddHangfire` call with:

```csharp
        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(RequiredConnectionString(configuration)));
```

Replace `InitializeDatabaseAsync` with:

```csharp
    private static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<FitnessClubDbContext>().Database.MigrateAsync();
    }
```

Add this method at the end of the class:

```csharp
    private static string RequiredConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is not set. Start SQL Server with "
                + "'docker compose -f deploy/docker-compose.yml up -d db' and set the connection string in user secrets (see api/CLAUDE.md).");

        return connectionString;
    }
```

Both callbacks run lazily. The DbContext's runs on the first resolve, which is `InitializeDatabaseAsync` at startup. Hangfire's runs when `JobStorage` is first resolved. The build-time OpenAPI generator never resolves either of them, so it needs no placeholder connection string.

- [ ] **Step 4: Drop the InMemory save guard from the DbContext**

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, delete `NonRelationalSaveLock`, `StoredAggregateRootsBeingChanged` and `EnsureNotStale`, and replace the two `SaveChanges` overrides with:

```csharp
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampChangedAggregates();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampChangedAggregates();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
```

SQL Server enforces the `Version` concurrency token in the `UPDATE ... WHERE` and rolls the whole save back in its transaction. The existing stale-save tests in `tests/FitnessClub.IntegrationTests/Persistence/*RepositoryTests.cs` now check exactly that. Keep `using Microsoft.EntityFrameworkCore.ChangeTracking;`, because `StampChangedAggregates` and `AggregateRootOf` still use `EntityEntry`.

- [ ] **Step 5: Remove the InMemory packages**

In `api/src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj`, delete:

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
```
```xml
    <PackageReference Include="Hangfire.InMemory" />
```

In `api/Directory.Packages.props`, delete:

```xml
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.12" />
```
```xml
    <PackageVersion Include="Hangfire.InMemory" Version="1.0.0" />
```

- [ ] **Step 6: Verify nothing references InMemory any more**

Run: `grep -rn "UseInMemory\|IsRelational\|InMemoryName\|EntityFrameworkCore.InMemory\|Hangfire.InMemory" src tests --include='*.cs' --include='*.csproj' --include='*.json' --include='*.props'; grep -n "InMemory" Directory.Packages.props`
Expected: no output. The unit-test fakes are named `InMemory*Repository` and contain none of these strings.

- [ ] **Step 7: Run all tests**

Run: `dotnet test`
Expected: all unit, architecture and integration tests PASS, including both `HostConfigurationTests` startup tests.

- [ ] **Step 8: Check the build-time OpenAPI document is unchanged**

Run: `dotnet build && git diff --exit-code ../ui/openapi/fitnessclub.json`
Expected: build succeeds and `git diff` exits 0, so the document is regenerated without a connection string and has not changed.

- [ ] **Step 9: Commit**

```bash
git add Directory.Packages.props src/FitnessClub.Infrastructure tests/FitnessClub.IntegrationTests/HostConfigurationTests.cs
git commit -m "feat(api): SQL Server only, remove EF Core and Hangfire in-memory storage

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Unique-index violations become 409

**Files:**
- Modify: `api/src/FitnessClub.Infrastructure/Persistence/UnitOfWork.cs`
- Modify: `api/tests/FitnessClub.IntegrationTests/Persistence/RoomRepositoryTests.cs`
- Modify: `api/tests/FitnessClub.IntegrationTests/Rooms/RoomsEndpointsTests.cs`

**Interfaces:**
- Consumes: `PersistenceTestBase.SaveAsync` / `ReadAsync` (existing); `ConflictException(string message)` (existing, `FitnessClub.Application.Common`). `ExceptionToProblemDetailsHandler` already maps it to 409.
- Produces: `IUnitOfWork.SaveChangesAsync` throws `ConflictException` when SQL Server rejects a save for a unique index (error 2601) or unique constraint (error 2627).

- [ ] **Step 1: Write the failing repository test**

Add to `api/tests/FitnessClub.IntegrationTests/Persistence/RoomRepositoryTests.cs` (and add `using FitnessClub.Application.Common;`):

```csharp
    [Fact]
    public async Task Saving_a_duplicate_name_past_the_service_check_is_a_conflict_and_writes_nothing()
    {
        var name = $"Room {Guid.NewGuid():N}";
        await SaveAsync<IRoomRepository>(rooms => rooms.Add(Room.Create(name, 20)));

        await Assert.ThrowsAsync<ConflictException>(() => SaveAsync<IRoomRepository>(rooms => rooms.Add(Room.Create(name, 10))));

        var all = await ReadAsync<IRoomRepository, IReadOnlyList<Room>>(rooms => rooms.ListAsync(true, Ct));
        Assert.Single(all, r => r.Name == name);
    }
```

- [ ] **Step 2: Write the concurrent HTTP test**

Add to `api/tests/FitnessClub.IntegrationTests/Rooms/RoomsEndpointsTests.cs`:

```csharp
    [Fact]
    public async Task Concurrent_creates_with_the_same_name_return_one_201_and_one_409()
    {
        var admin = factory.CreateClientWithRoles(Roles.Admin);
        var body = NewRoom();

        var responses = await Task.WhenAll(
            admin.PostAsJsonAsync(BaseUrl, body, Ct),
            admin.PostAsJsonAsync(BaseUrl, body, Ct));

        HttpStatusCode[] expected = [HttpStatusCode.Created, HttpStatusCode.Conflict];
        Assert.Equal(expected, responses.Select(response => response.StatusCode).Order().ToArray());
    }
```

Whichever request loses gets 409. That can come from the service check or, when both pass it, from the unique index. Before this task the second case returns 500, so the test fails intermittently.

- [ ] **Step 3: Run them to verify the repository test fails**

Run: `dotnet test --project tests/FitnessClub.IntegrationTests --filter-method "*Saving_a_duplicate_name_past_the_service_check_is_a_conflict_and_writes_nothing"`
Expected: FAIL. `Assert.Throws() Failure: Exception type was not an exact match`, expected `ConflictException`, actual `DbUpdateException`.

- [ ] **Step 4: Map unique violations in the unit of work**

Replace `api/src/FitnessClub.Infrastructure/Persistence/UnitOfWork.cs` with:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence;

internal sealed class UnitOfWork(FitnessClubDbContext db) : IUnitOfWork
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The data was changed by someone else. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            throw new ConflictException("This would duplicate an existing record. Reload the data and try again.");
        }
    }
}
```

`DbUpdateConcurrencyException` derives from `DbUpdateException`, so its `catch` must stay first.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.Persistence.RoomRepositoryTests"`
Expected: PASS, 2 tests.

Run: `dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.Rooms.RoomsEndpointsTests"`
Expected: PASS.

Run: `dotnet test`
Expected: all PASS (the architecture tests allow `Microsoft.Data.SqlClient` in Infrastructure).

- [ ] **Step 6: Commit**

```bash
git add src/FitnessClub.Infrastructure/Persistence/UnitOfWork.cs tests/FitnessClub.IntegrationTests/Persistence/RoomRepositoryTests.cs tests/FitnessClub.IntegrationTests/Rooms/RoomsEndpointsTests.cs
git commit -m "feat(api): map SQL Server unique-index violations to 409

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Service tests run on real repositories (no in-memory fakes)

Added at the user's request ("do not keep memory repo"). The `InMemory*Repository` fakes in `tests/FitnessClub.UnitTests/Fakes` are deleted. Every test that used them moves to `FitnessClub.IntegrationTests` and runs the real services and repositories against SQL Server.

**Files:**
- Modify: `api/src/FitnessClub.Application/FitnessClub.Application.csproj` (`InternalsVisibleTo` → `FitnessClub.IntegrationTests`)
- Create: `api/tests/FitnessClub.IntegrationTests/Services/ServiceTestBase.cs`, `CountingUnitOfWork.cs`, `FakeTimeProvider.cs`, `FakeNotificationSender.cs`, `TestData.cs`
- Move + adapt: `UnitTests/Application/{ClientServiceTests,ExpiryNotificationServiceTests,MembershipPlanServiceTests,RoomServiceTests,TrainerServiceTests,TrainingSessionServiceTests}.cs` → `IntegrationTests/Services/`
- Move + adapt: `UnitTests/Domain/TrainingSessionTests.cs` → `IntegrationTests/Services/SessionSchedulerTests.cs` (the `SessionScheduler` domain service needs `ITrainingSessionRepository`)
- Delete: `UnitTests/Fakes/` (all files) and `UnitTests/Application/`

**Interfaces:**
- Consumes: `FitnessClubApiFactory.ConnectionString`, `TestDatabase.DeleteAllRows` (Task 1); `ConflictException` mapping (Task 3).
- Produces: `ServiceTestBase(FitnessClubApiFactory factory)`, an `IClassFixture<FitnessClubApiFactory>` and `IAsyncDisposable`. For each test it empties the database and opens one DI scope (so one DbContext), and exposes:
  - `T Get<T>()`: resolves the real repository, unit of work, and so on from that scope.
  - `CountingUnitOfWork UnitOfWork`: wraps the real `IUnitOfWork` and counts `SaveCount`. It is passed to services, so the old "saves exactly once" assertions stay.
  - `Task SeedAsync()`: saves through the real, uncounted unit of work. Test helpers call it after `repository.Add(...)`, because unlike the fakes, EF queries don't see unsaved rows.

Rules for the move:
- Services are built with `new XService(Get<IXRepository>(), ..., UnitOfWork, clock)` just as before. Only the fakes are swapped for real repositories.
- Helpers that added to a fake (`_plans.Add(plan)`) become async and call `await SeedAsync()`.
- Fake-only members are replaced: `InMemoryNotificationRepository.All` → `await Get<INotificationRepository>().ListAsync(null, Ct)`.
- `TestData` phones become unique (`+380` + 9 random digits), because SQL Server enforces unique phone indexes.
- Test bodies and assertions otherwise stay the same. Each test now starts from an empty database.

- [ ] **Step 1:** Move the test files and write `ServiceTestBase`/`CountingUnitOfWork`. Run `dotnet build` and expect it to fail with the fake types missing.
- [ ] **Step 2:** Adapt each class as above until `dotnet build` passes.
- [ ] **Step 3:** Run `dotnet test --project tests/FitnessClub.IntegrationTests` and expect all PASS. The moved test count must equal the removed test count.
- [ ] **Step 4:** Run `grep -rn "InMemory" tests src --include='*.cs'` and expect no output. Run `dotnet test` and expect all PASS.
- [ ] **Step 5:** Commit: `test: run service tests on real repositories, remove in-memory fakes`.

---

### Task 5: Docs, and run the full stack with docker compose

**Files:**
- Modify: `api/CLAUDE.md`
- Modify: `CLAUDE.md` (repo root)
- Modify: `deploy/CLAUDE.md`
- Modify: `docs/Code/Demo Data and Users.md:5`
- Modify: `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md:203`
- Local only, never committed: `deploy/.env`

**Interfaces:**
- Consumes: the behavior from Tasks 1–4.
- Produces: nothing used by code.

- [ ] **Step 1: Update `api/CLAUDE.md`**

In **Stack**, replace `- EF Core: SQL Server with migrations, or InMemory when no connection string is set` with:

```markdown
- EF Core on SQL Server with migrations. There is no in-memory fallback.
```

In **Commands**, add after the `dotnet run` line's code block, before "Filters need `--project`":

```markdown
- **Tests need Docker running.** Integration tests start one SQL Server container (Testcontainers, `mcr.microsoft.com/mssql/server:2022-latest`) for the whole run. The first run pulls the image.
- **Running locally needs SQL Server:** start `db` from compose and set `ConnectionStrings:FitnessClub` (see "Persistence"). Without it, startup fails with `InvalidOperationException` naming the setting.
```

In **Conventions**, delete the whole `- **InMemory limits:** ...` bullet and add:

```markdown
- **Uniqueness:** services check unique values first so they can return a clear message. If two requests race past that check, SQL Server's unique index rejects the second save, and `UnitOfWork` turns errors 2601/2627 into `ConflictException` → 409. A save rejected for a stale `Version` → 409 as well, and SQL Server rolls the whole save back.
```

In **Conventions → Integration tests**, replace `- Each test class gets its own InMemory database, but tests in the same class share it, so use unique names.` with:

```markdown
  - Each `FitnessClubApiFactory` (one per test class) gets its own database in the shared container. The API migrates it at startup, including `SeedMockData`, and then `TestDatabase.DeleteAllRows` empties every `dbo` table, so tests start with no rows. Tests in the same class share the database, so use unique names and phones: SQL Server enforces unique indexes and foreign keys.
```

Rename the section `## Persistence switch` to `## Persistence` and replace its first two bullets with:

```markdown
- **`ConnectionStrings:FitnessClub` is required.** EF Core and Hangfire both use SQL Server. At startup, `UseInfrastructureAsync()` runs `Database.MigrateAsync()`.
```

In **Layout**, change the `tests/FitnessClub.UnitTests` line to `tests/FitnessClub.UnitTests     Domain rules only (no repositories, no Infrastructure)` and the `tests/FitnessClub.IntegrationTests` line to `tests/FitnessClub.IntegrationTests  HTTP tests, repository round-trips, and service tests (Services/) on SQL Server via FitnessClubApiFactory`.

In **Hangfire**, replace `- Storage follows the persistence switch: in memory now, SQL Server when the connection string is set.` with `- Storage is SQL Server (the `HangFire` schema in the same database).`

- [ ] **Step 2: Update the root `CLAUDE.md`, `deploy/CLAUDE.md` and the docs**

In `CLAUDE.md`, in the **Backend** status bullet, replace `With a connection string it runs on SQL Server, and EF migrations create the schema and seed mock data.` with:

```markdown
It runs on SQL Server only (locally through `deploy/docker-compose.yml`), and EF migrations create the schema and seed mock data.
```

In `deploy/CLAUDE.md`, under **Commands**, add after the code block:

```markdown
- To run only the database for a locally run API (`dotnet run`): `docker compose -f deploy/docker-compose.yml up -d db`.
- `deploy/.env` needs every key in `deploy/.env.example`. Compose names the first missing one and stops.
```

In `docs/Code/Demo Data and Users.md`, line 5, delete the sentence `The InMemory database (no connection string) stays empty.`

In `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`, line 203, append ` Done in 2026-10-08 (docs/Code/Plans/2026-10-08-sql-server-only.md).` to the end of that bullet.

- [ ] **Step 3: Commit the docs**

```bash
cd ..
git add CLAUDE.md api/CLAUDE.md deploy/CLAUDE.md "docs/Code/Demo Data and Users.md" docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md
git commit -m "docs: SQL Server is the only database

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Complete `deploy/.env` (human step)**

`deploy/.env` currently has `AUTH0_DOMAIN`, `AUTH0_AUDIENCE` and `DB_SA_PASSWORD`, but no `AUTH0_UI_CLIENT_ID`, so compose stops with `Set AUTH0_UI_CLIENT_ID in deploy/.env`. Ask the user to add the line `AUTH0_UI_CLIENT_ID=<Auth0 SPA client id>` (the same value as `VITE_AUTH0_CLIENT_ID` in `ui/.env`). Don't print or commit `.env` values.

- [ ] **Step 5: Start the stack and check it**

Run from the repo root:

```bash
docker compose -f deploy/docker-compose.yml up --build -d
docker compose -f deploy/docker-compose.yml ps
```

Expected: `db`, `api` and `ui` all `running`, with `db` and `api` `(healthy)`. The first start can take a few minutes on Apple silicon (SQL Server under emulation).

```bash
curl -fsS http://localhost:8080/health
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8081/api/membership-plans
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8081/
docker compose -f deploy/docker-compose.yml exec db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d FitnessClub -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Clients"'
```

Expected, in order: `Healthy`; `401` (nginx proxies `/api/` to the API, and the API requires a login); `200` (the SPA); `16` (the seeded clients).

- [ ] **Step 6: Restart on the existing volume**

```bash
docker compose -f deploy/docker-compose.yml restart api
docker compose -f deploy/docker-compose.yml ps api
docker compose -f deploy/docker-compose.yml exec db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d FitnessClub -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Clients"'
```

Expected: `api` returns to `(healthy)`, and the count is still `16` (migrations already applied, nothing re-seeded).

- [ ] **Step 7: Sign in through the UI (human step)**

Open http://localhost:8081 and sign in as a demo user from `docs/Code/Demo Data and Users.md`. The clients page should list the 16 seeded clients. If Auth0 rejects the callback, add `http://localhost:8081` to the SPA's Allowed Callback URLs, Logout URLs and Web Origins in Auth0. That is tenant configuration, not a code change.

- [ ] **Step 8: Stop the stack**

Run: `docker compose -f deploy/docker-compose.yml down`
Expected: containers removed and the `db-data` volume kept. Use `down -v` only to wipe the data on purpose.
