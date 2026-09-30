---
tags: [plan, api]
date: 2026-10-05
spec: "[[2026-10-05-api-foundation-design]]"
---

# API Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `api/` backend foundation: the layered .NET 10 solution, EF Core persistence, Auth0 logins with roles, Hangfire, problem-details error handling, tests and Docker. Membership plans are built end to end as the working example.

**Architecture:** Clean Architecture with four projects:
- **Domain:** entities and their rules.
- **Application:** use-case services, request/response records, and `IApplicationDbContext`.
- **Infrastructure:** EF Core and Hangfire.
- **Api:** controllers, auth, error handling.

When the `FitnessClub` connection string is empty, EF Core and Hangfire use in-memory storage. When it's set, both switch to SQL Server. Every endpoint requires a logged-in user by default, and controllers name the roles they accept.

**Tech Stack:**
- .NET 10 (SDK 10.0.300), ASP.NET Core controllers
- EF Core 10.0.12 (InMemory + SQL Server)
- JWT bearer authentication (Auth0)
- Hangfire 1.8.25 + Hangfire.InMemory 1.0.0
- Microsoft.AspNetCore.OpenApi + Scalar 2.17.13
- xUnit v3 (`xunit.v3` 4.0.1) on Microsoft.Testing.Platform
- Docker / docker compose

**Spec:** `docs/Code/Specs/2026-10-05-api-foundation-design.md`

> All code in this plan was compiled and tested in a scratch copy before the plan was written: 50 tests pass, the app runs locally, and the Docker image builds and passes its health check. Copy code blocks exactly.

## Global Constraints

- **SDK:** pinned by `api/global.json` to `10.0.300` with `rollForward: latestFeature`. All projects target `net10.0`.
- **Build settings:** `Nullable`, `ImplicitUsings` and `TreatWarningsAsErrors` are on for every project (`api/Directory.Build.props`).
- **Package versions** live only in `api/Directory.Packages.props`. A `<PackageReference>` never has a `Version` attribute.
- **Newtonsoft.Json:** `CentralPackageTransitivePinningEnabled` is on and pins it to `13.0.4`. Hangfire.Core would otherwise pull 11.0.1, which has a known security vulnerability (NU1903), and that's a build error under warnings-as-errors.
- **Test runner:** tests run on Microsoft.Testing.Platform, enabled in `global.json`. Don't add `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio`.
- **Cancellation tokens:** test code passes `TestContext.Current.CancellationToken` to every async call. xUnit's analyzer flags missing tokens, and warnings are errors.
- **Which project may reference which:**
  - Domain references nothing.
  - Application references Domain plus `Microsoft.EntityFrameworkCore`.
  - Infrastructure references Application.
  - Api references Application and Infrastructure.
- **Roles:** `Admin`, `Receptionist`, `Trainer`. The Auth0 roles claim is `https://fitnessclub/roles`.
- **MembershipPlan rules:**
  - `Name`: required, 1–100 chars, unique ignoring case and surrounding spaces.
  - `Price`: > 0, at most 2 decimal places.
  - `ValidityDays`: 1–3650.
  - `VisitLimit`: `null` (unlimited) or 1–1000.
  - Plans are never deleted, only activated or deactivated.
- **Not used:** MediatR, AutoMapper, FluentValidation, repositories.
- **Secrets:** Auth0 values are never committed. `deploy/.env` is ignored by git.

## Review Focus

1. **Wrong JSON types or overflowing numbers** (`"price": "abc"`, `"price": 1e30`) must return a 400 problem-details response, not a 500. Tested in Task 5.
2. **A name that's only spaces, or longer than 100 characters,** must be rejected with 400 at the API, before it reaches the domain. Tested in Task 5.
3. **A malformed id in the URL** (`/api/membership-plans/not-a-guid`) must return 404 for a logged-in user, not 500. Tested in Task 5.
4. **The Hangfire dashboard, OpenAPI document and Scalar page** must not exist outside Development, even for an Admin. Tested in Task 4.
5. **Missing Auth0 settings** must stop the app at startup with an error naming `Domain` and `Audience`, instead of failing later on the first logged-in request. Tested in Task 4.

---

### Task 1: Initialize the git repository

**Files:**
- Create: `.gitignore`

**Interfaces:**
- Consumes: nothing
- Produces: a git repo on branch `main` holding the existing CLAUDE.md files, the requirements note, the spec and this plan.

- [ ] **Step 1: Create `.gitignore` at the repo root**

```gitignore
# .NET
bin/
obj/
*.user
.vs/
.idea/
TestResults/

# Node / UI
node_modules/
dist/

# Secrets
deploy/.env

# Obsidian per-device state
docs/.obsidian/workspace*.json

# OS
.DS_Store
```

- [ ] **Step 2: Initialize the repo and commit**

Run from the repo root (`/Users/arsen/Personal/Gym`):

```bash
git init -b main
git add -A
git commit -m "chore: initial project structure, requirements and API foundation spec"
```

Expected: the commit contains `.gitignore`, `CLAUDE.md`, `api/CLAUDE.md`, `ui/CLAUDE.md`, `deploy/CLAUDE.md`, `docs/CLAUDE.md`, `docs/Requirements/Fitness Club System.md`, the spec and this plan.

---

### Task 2: Solution skeleton and the `MembershipPlan` domain entity

**Files:**
- Create: `api/global.json`, `api/Directory.Build.props`, `api/Directory.Packages.props`, `api/FitnessClub.slnx`
- Create: `api/src/FitnessClub.Domain/FitnessClub.Domain.csproj`
- Create: `api/src/FitnessClub.Domain/Common/Entity.cs`, `api/src/FitnessClub.Domain/Common/DomainException.cs`
- Create: `api/src/FitnessClub.Domain/MembershipPlans/MembershipPlan.cs`
- Create: `api/tests/FitnessClub.UnitTests/FitnessClub.UnitTests.csproj`
- Test: `api/tests/FitnessClub.UnitTests/Domain/MembershipPlanTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces:
  - `FitnessClub.Domain.Common.Entity` with `Guid Id { get; }`
  - `FitnessClub.Domain.Common.DomainException(string message)`
  - `FitnessClub.Domain.MembershipPlans.MembershipPlan`:
    - constants `NameMaxLength = 100`, `MaxValidityDays = 3650`, `MaxVisitLimit = 1000`
    - properties `Name`, `Price`, `ValidityDays`, `VisitLimit` (int?), `IsActive`
    - `static MembershipPlan Create(string name, decimal price, int validityDays, int? visitLimit)`
    - `void Update(string name, decimal price, int validityDays, int? visitLimit)`
    - `void Activate()`, `void Deactivate()`

- [ ] **Step 1: Create the solution-wide config files**

`api/global.json`:

```json
{
  "sdk": {
    "version": "10.0.300",
    "rollForward": "latestFeature"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

`api/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

`api/Directory.Packages.props` (lists every package the foundation uses, including those added in later tasks):

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.12" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.12" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.12" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.12" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    <PackageVersion Include="Hangfire.AspNetCore" Version="1.8.25" />
    <PackageVersion Include="Hangfire.InMemory" Version="1.0.0" />
    <PackageVersion Include="Hangfire.SqlServer" Version="1.8.25" />
    <!-- Pinned: Hangfire.Core pulls a vulnerable Newtonsoft.Json 11.x transitively -->
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />
    <PackageVersion Include="Scalar.AspNetCore" Version="2.17.13" />
    <PackageVersion Include="xunit.v3" Version="4.0.1" />
  </ItemGroup>
</Project>
```

`api/src/FitnessClub.Domain/FitnessClub.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

`api/tests/FitnessClub.UnitTests/FitnessClub.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\FitnessClub.Domain\FitnessClub.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

Create the solution. Run from `api/`:

```bash
dotnet new sln --format slnx -n FitnessClub
dotnet sln FitnessClub.slnx add src/FitnessClub.Domain/FitnessClub.Domain.csproj tests/FitnessClub.UnitTests/FitnessClub.UnitTests.csproj
```

- [ ] **Step 2: Write the failing domain tests**

`api/tests/FitnessClub.UnitTests/Domain/MembershipPlanTests.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;

namespace FitnessClub.UnitTests.Domain;

public class MembershipPlanTests
{
    [Fact]
    public void Create_with_valid_values_sets_fields_and_is_active()
    {
        var plan = MembershipPlan.Create("  Monthly  ", 800m, 30, null);

        Assert.NotEqual(Guid.Empty, plan.Id);
        Assert.Equal("Monthly", plan.Name);
        Assert.Equal(800m, plan.Price);
        Assert.Equal(30, plan.ValidityDays);
        Assert.Null(plan.VisitLimit);
        Assert.True(plan.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_blank_name_throws(string name)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create(name, 100m, 30, null));
    }

    [Fact]
    public void Create_with_name_longer_than_max_throws()
    {
        var name = new string('a', MembershipPlan.NameMaxLength + 1);

        Assert.Throws<DomainException>(() => MembershipPlan.Create(name, 100m, 30, null));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("10.001")]
    public void Create_with_invalid_price_throws(string price)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), 30, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MembershipPlan.MaxValidityDays + 1)]
    public void Create_with_validity_out_of_range_throws(int validityDays)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", 100m, validityDays, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MembershipPlan.MaxVisitLimit + 1)]
    public void Create_with_visit_limit_out_of_range_throws(int visitLimit)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", 100m, 30, visitLimit));
    }

    [Fact]
    public void Update_with_invalid_values_leaves_plan_unchanged()
    {
        var plan = MembershipPlan.Create("Monthly", 800m, 30, null);

        Assert.Throws<DomainException>(() => plan.Update("Yearly", -1m, 365, null));

        Assert.Equal("Monthly", plan.Name);
        Assert.Equal(800m, plan.Price);
    }

    [Fact]
    public void Deactivate_then_activate_toggles_IsActive()
    {
        var plan = MembershipPlan.Create("Monthly", 800m, 30, null);

        plan.Deactivate();
        Assert.False(plan.IsActive);

        plan.Activate();
        Assert.True(plan.IsActive);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run from `api/`: `dotnet test`
Expected: build FAILS with `CS0234`/`CS0246`. The namespace `FitnessClub.Domain.Common` and the type `MembershipPlan` don't exist yet.

- [ ] **Step 4: Implement the domain types**

`api/src/FitnessClub.Domain/Common/Entity.cs`:

```csharp
namespace FitnessClub.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
}
```

`api/src/FitnessClub.Domain/Common/DomainException.cs`:

```csharp
namespace FitnessClub.Domain.Common;

public sealed class DomainException(string message) : Exception(message);
```

`api/src/FitnessClub.Domain/MembershipPlans/MembershipPlan.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.MembershipPlans;

public sealed class MembershipPlan : Entity
{
    public const int NameMaxLength = 100;
    public const int MaxValidityDays = 3650;
    public const int MaxVisitLimit = 1000;

    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }
    public int ValidityDays { get; private set; }
    public int? VisitLimit { get; private set; }
    public bool IsActive { get; private set; }

    private MembershipPlan()
    {
    }

    public static MembershipPlan Create(string name, decimal price, int validityDays, int? visitLimit)
    {
        var plan = new MembershipPlan { IsActive = true };
        plan.Update(name, price, validityDays, visitLimit);
        return plan;
    }

    public void Update(string name, decimal price, int validityDays, int? visitLimit)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Plan name is required.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
            throw new DomainException($"Plan name must be at most {NameMaxLength} characters.");

        if (price <= 0)
            throw new DomainException("Price must be greater than zero.");

        if (decimal.Round(price, 2) != price)
            throw new DomainException("Price can have at most 2 decimal places.");

        if (validityDays is < 1 or > MaxValidityDays)
            throw new DomainException($"Validity must be between 1 and {MaxValidityDays} days.");

        if (visitLimit is < 1 or > MaxVisitLimit)
            throw new DomainException($"Visit limit must be between 1 and {MaxVisitLimit}, or empty for unlimited.");

        Name = trimmedName;
        Price = price;
        ValidityDays = validityDays;
        VisitLimit = visitLimit;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run from `api/`: `dotnet test`
Expected: `total: 13, failed: 0, succeeded: 13`.

- [ ] **Step 6: Commit**

```bash
git add api
git commit -m "feat(api): solution skeleton and MembershipPlan domain entity"
```

---

### Task 3: Application service and EF Core persistence

**Files:**
- Create: `api/src/FitnessClub.Application/FitnessClub.Application.csproj`
- Create: `api/src/FitnessClub.Application/Abstractions/IApplicationDbContext.cs`
- Create: `api/src/FitnessClub.Application/Common/NotFoundException.cs`, `ConflictException.cs`, `Roles.cs`
- Create: `api/src/FitnessClub.Application/MembershipPlans/MembershipPlanRequest.cs`, `MembershipPlanResponse.cs`, `MembershipPlanService.cs`
- Create: `api/src/FitnessClub.Application/DependencyInjection.cs`
- Create: `api/src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj`
- Create: `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`
- Create: `api/src/FitnessClub.Infrastructure/Persistence/Configurations/MembershipPlanConfiguration.cs`
- Create: `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`
- Modify: `api/tests/FitnessClub.UnitTests/FitnessClub.UnitTests.csproj` (add project references)
- Test: `api/tests/FitnessClub.UnitTests/Application/MembershipPlanServiceTests.cs`

**Interfaces:**
- Consumes: `MembershipPlan`, `DomainException` from Task 2
- Produces:
  - `IApplicationDbContext` with `DbSet<MembershipPlan> MembershipPlans` and `Task<int> SaveChangesAsync(CancellationToken = default)`
  - `NotFoundException(string)`, `ConflictException(string)`
  - `Roles.Admin`, `Roles.Receptionist`, `Roles.Trainer`
  - `MembershipPlanRequest` (record, init props): `Name`, `Price`, `ValidityDays`, `VisitLimit`
  - `MembershipPlanResponse(Guid Id, string Name, decimal Price, int ValidityDays, int? VisitLimit, bool IsActive)`
  - `MembershipPlanService`:
    - `ListAsync(bool includeInactive, CancellationToken)`
    - `GetAsync(Guid, CancellationToken)`
    - `CreateAsync(MembershipPlanRequest, CancellationToken)`
    - `UpdateAsync(Guid, MembershipPlanRequest, CancellationToken)`
    - `ActivateAsync(Guid, CancellationToken)`
    - `DeactivateAsync(Guid, CancellationToken)`
  - `IServiceCollection.AddApplication()`
  - `FitnessClubDbContext(DbContextOptions<FitnessClubDbContext>)`
  - `IServiceCollection.AddInfrastructure(IConfiguration)`
  - `DependencyInjection.ConnectionStringName = "FitnessClub"`
  - The in-memory database name is read from the setting `Database:InMemoryName`, default `"FitnessClub"`.

- [ ] **Step 1: Create the project files and register them**

`api/src/FitnessClub.Application/FitnessClub.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\FitnessClub.Domain\FitnessClub.Domain.csproj" />
  </ItemGroup>
</Project>
```

`api/src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj` (the Hangfire packages are used in Task 4):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Hangfire.AspNetCore" />
    <PackageReference Include="Hangfire.InMemory" />
    <PackageReference Include="Hangfire.SqlServer" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\FitnessClub.Application\FitnessClub.Application.csproj" />
  </ItemGroup>
</Project>
```

Replace the `<ItemGroup>` with the project references in `api/tests/FitnessClub.UnitTests/FitnessClub.UnitTests.csproj` with:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\src\FitnessClub.Domain\FitnessClub.Domain.csproj" />
    <ProjectReference Include="..\..\src\FitnessClub.Application\FitnessClub.Application.csproj" />
    <ProjectReference Include="..\..\src\FitnessClub.Infrastructure\FitnessClub.Infrastructure.csproj" />
  </ItemGroup>
```

Run from `api/`:

```bash
dotnet sln FitnessClub.slnx add src/FitnessClub.Application/FitnessClub.Application.csproj src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj
```

- [ ] **Step 2: Write the failing service tests**

`api/tests/FitnessClub.UnitTests/Application/MembershipPlanServiceTests.cs`:

```csharp
using FitnessClub.Application.Common;
using FitnessClub.Application.MembershipPlans;
using FitnessClub.Domain.Common;
using FitnessClub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.UnitTests.Application;

public class MembershipPlanServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly MembershipPlanService _service;

    public MembershipPlanServiceTests()
    {
        var options = new DbContextOptionsBuilder<FitnessClubDbContext>()
            .UseInMemoryDatabase($"unit-{Guid.NewGuid()}")
            .Options;
        _service = new MembershipPlanService(new FitnessClubDbContext(options));
    }

    private static MembershipPlanRequest Request(string name = "Monthly", decimal price = 800m, int validityDays = 30, int? visitLimit = null) =>
        new() { Name = name, Price = price, ValidityDays = validityDays, VisitLimit = visitLimit };

    [Fact]
    public async Task CreateAsync_returns_saved_plan()
    {
        var created = await _service.CreateAsync(Request(), Ct);

        var loaded = await _service.GetAsync(created.Id, Ct);
        Assert.Equal(created, loaded);
        Assert.True(loaded.IsActive);
    }

    [Fact]
    public async Task CreateAsync_with_duplicate_name_ignoring_case_and_spaces_throws_conflict()
    {
        await _service.CreateAsync(Request("Monthly"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(Request("  MONTHLY "), Ct));
    }

    [Fact]
    public async Task CreateAsync_with_invalid_domain_values_throws_domain_exception()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(Request(price: 10.001m), Ct));
    }

    [Fact]
    public async Task GetAsync_for_missing_plan_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task UpdateAsync_keeping_own_name_succeeds()
    {
        var created = await _service.CreateAsync(Request("Monthly", 800m), Ct);

        var updated = await _service.UpdateAsync(created.Id, Request("Monthly", 900m), Ct);

        Assert.Equal(900m, updated.Price);
    }

    [Fact]
    public async Task UpdateAsync_to_another_plans_name_throws_conflict()
    {
        await _service.CreateAsync(Request("Monthly"), Ct);
        var yearly = await _service.CreateAsync(Request("Yearly", 8000m, 365), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(yearly.Id, Request("monthly"), Ct));
    }

    [Fact]
    public async Task UpdateAsync_for_missing_plan_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(Guid.NewGuid(), Request(), Ct));
    }

    [Fact]
    public async Task ListAsync_hides_inactive_plans_unless_requested()
    {
        var monthly = await _service.CreateAsync(Request("Monthly"), Ct);
        await _service.CreateAsync(Request("Yearly", 8000m, 365), Ct);
        await _service.DeactivateAsync(monthly.Id, Ct);

        var activeOnly = await _service.ListAsync(includeInactive: false, Ct);
        var all = await _service.ListAsync(includeInactive: true, Ct);

        Assert.Equal(["Yearly"], activeOnly.Select(p => p.Name));
        Assert.Equal(["Monthly", "Yearly"], all.Select(p => p.Name));
    }

    [Fact]
    public async Task ActivateAsync_reactivates_plan()
    {
        var plan = await _service.CreateAsync(Request(), Ct);
        await _service.DeactivateAsync(plan.Id, Ct);

        await _service.ActivateAsync(plan.Id, Ct);

        Assert.True((await _service.GetAsync(plan.Id, Ct)).IsActive);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run from `api/`: `dotnet test`
Expected: build FAILS with `CS0234`/`CS0246` for `FitnessClub.Application.Common`, `MembershipPlanService` and `FitnessClubDbContext`.

- [ ] **Step 4: Implement the Application layer**

`api/src/FitnessClub.Application/Abstractions/IApplicationDbContext.cs`:

```csharp
using FitnessClub.Domain.MembershipPlans;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<MembershipPlan> MembershipPlans { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

`api/src/FitnessClub.Application/Common/NotFoundException.cs`:

```csharp
namespace FitnessClub.Application.Common;

public sealed class NotFoundException(string message) : Exception(message);
```

`api/src/FitnessClub.Application/Common/ConflictException.cs`:

```csharp
namespace FitnessClub.Application.Common;

public sealed class ConflictException(string message) : Exception(message);
```

`api/src/FitnessClub.Application/Common/Roles.cs`:

```csharp
namespace FitnessClub.Application.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Receptionist = "Receptionist";
    public const string Trainer = "Trainer";
}
```

`api/src/FitnessClub.Application/MembershipPlans/MembershipPlanRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using FitnessClub.Domain.MembershipPlans;

namespace FitnessClub.Application.MembershipPlans;

public sealed record MembershipPlanRequest
{
    [Required]
    [StringLength(MembershipPlan.NameMaxLength)]
    public string Name { get; init; } = "";

    [Range(0.01, 1_000_000)]
    public decimal Price { get; init; }

    [Range(1, MembershipPlan.MaxValidityDays)]
    public int ValidityDays { get; init; }

    [Range(1, MembershipPlan.MaxVisitLimit)]
    public int? VisitLimit { get; init; }
}
```

`api/src/FitnessClub.Application/MembershipPlans/MembershipPlanResponse.cs`:

```csharp
using FitnessClub.Domain.MembershipPlans;

namespace FitnessClub.Application.MembershipPlans;

public sealed record MembershipPlanResponse(
    Guid Id,
    string Name,
    decimal Price,
    int ValidityDays,
    int? VisitLimit,
    bool IsActive)
{
    public static MembershipPlanResponse FromEntity(MembershipPlan plan) =>
        new(plan.Id, plan.Name, plan.Price, plan.ValidityDays, plan.VisitLimit, plan.IsActive);
}
```

`api/src/FitnessClub.Application/MembershipPlans/MembershipPlanService.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.MembershipPlans;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Application.MembershipPlans;

public sealed class MembershipPlanService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<MembershipPlanResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var query = db.MembershipPlans.AsNoTracking();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        var plans = await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        return plans.Select(MembershipPlanResponse.FromEntity).ToList();
    }

    public async Task<MembershipPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        MembershipPlanResponse.FromEntity(await FindAsync(id, cancellationToken));

    public async Task<MembershipPlanResponse> CreateAsync(MembershipPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = MembershipPlan.Create(request.Name, request.Price, request.ValidityDays, request.VisitLimit);
        await EnsureNameIsUniqueAsync(plan.Name, excludeId: null, cancellationToken);

        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return MembershipPlanResponse.FromEntity(plan);
    }

    public async Task<MembershipPlanResponse> UpdateAsync(Guid id, MembershipPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        plan.Update(request.Name, request.Price, request.ValidityDays, request.VisitLimit);
        await EnsureNameIsUniqueAsync(plan.Name, plan.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return MembershipPlanResponse.FromEntity(plan);
    }

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        plan.Activate();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        plan.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<MembershipPlan> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw new NotFoundException($"Membership plan '{id}' was not found.");

    // InMemory does not enforce unique indexes, so uniqueness is checked here.
    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name.ToLower();
        var nameTaken = await db.MembershipPlans.AnyAsync(
            p => p.Id != excludeId && p.Name.ToLower() == normalizedName,
            cancellationToken);

        if (nameTaken)
            throw new ConflictException($"A membership plan named '{name}' already exists.");
    }
}
```

`api/src/FitnessClub.Application/DependencyInjection.cs`:

```csharp
using FitnessClub.Application.MembershipPlans;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<MembershipPlanService>();
        return services;
    }
}
```

- [ ] **Step 5: Implement persistence**

`api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.MembershipPlans;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence;

public sealed class FitnessClubDbContext(DbContextOptions<FitnessClubDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FitnessClubDbContext).Assembly);
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Configurations/MembershipPlanConfiguration.cs`:

```csharp
using FitnessClub.Domain.MembershipPlans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    public void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        builder.ToTable("MembershipPlans");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(MembershipPlan.NameMaxLength).IsRequired();
        builder.HasIndex(p => p.Name).IsUnique();
        builder.Property(p => p.Price).HasPrecision(18, 2);
    }
}
```

`api/src/FitnessClub.Infrastructure/DependencyInjection.cs` (persistence only for now; Task 4 adds Hangfire):

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "FitnessClub";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FitnessClubDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                options.UseInMemoryDatabase(configuration["Database:InMemoryName"] ?? "FitnessClub");
            else
                options.UseSqlServer(connectionString);
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<FitnessClubDbContext>());

        return services;
    }

    // Applies pending EF migrations when a relational database is configured; the InMemory provider has no migrations.
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FitnessClubDbContext>();
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run from `api/`: `dotnet test`
Expected: `total: 22, failed: 0, succeeded: 22`.

- [ ] **Step 7: Commit**

```bash
git add api
git commit -m "feat(api): membership plan service and EF Core persistence"
```

---

### Task 4: API host: auth, error handling, Hangfire, health

**Files:**
- Modify: `api/src/FitnessClub.Infrastructure/DependencyInjection.cs` (add Hangfire)
- Create: `api/src/FitnessClub.Infrastructure/BackgroundJobs/RecurringJobs.cs`
- Create: `api/src/FitnessClub.Api/FitnessClub.Api.csproj`, `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`
- Create: `api/src/FitnessClub.Api/Auth/Auth0Options.cs`, `api/src/FitnessClub.Api/Auth/AuthenticationSetup.cs`
- Create: `api/src/FitnessClub.Api/ErrorHandling/ExceptionToProblemDetailsHandler.cs`
- Create: `api/tests/FitnessClub.IntegrationTests/FitnessClub.IntegrationTests.csproj`
- Create: `api/tests/FitnessClub.IntegrationTests/Infrastructure/TestAuthHandler.cs`, `FitnessClubApiFactory.cs`
- Test: `api/tests/FitnessClub.IntegrationTests/HealthTests.cs`, `api/tests/FitnessClub.IntegrationTests/HostConfigurationTests.cs`

**Interfaces:**
- Consumes: `AddApplication()`, `AddInfrastructure(IConfiguration)`, `InitializeDatabaseAsync()`, `Roles`, `DomainException`, `NotFoundException`, `ConflictException`
- Produces:
  - `RecurringJobs.Register(IRecurringJobManager)`
  - `Auth0Options`: `Domain`, `Audience`, `RolesClaim`; section `"Auth0"`
  - `IServiceCollection.AddAuth0Authentication(IConfiguration)`
  - `ExceptionToProblemDetailsHandler`
  - `public partial class Program`
  - Test helpers:
    - `FitnessClubApiFactory` (`WebApplicationFactory<Program>`) with `HttpClient CreateClientWithRoles(params string[] roles)`
    - `TestAuthHandler`: `SchemeName = "Test"`, `RolesHeader = "X-Test-Roles"`

- [ ] **Step 1: Create the project files and register them**

`api/src/FitnessClub.Api/FitnessClub.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <UserSecretsId>fitnessclub-api</UserSecretsId>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Scalar.AspNetCore" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\FitnessClub.Application\FitnessClub.Application.csproj" />
    <ProjectReference Include="..\FitnessClub.Infrastructure\FitnessClub.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

`api/tests/FitnessClub.IntegrationTests/FitnessClub.IntegrationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\FitnessClub.Api\FitnessClub.Api.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

Run from `api/`:

```bash
dotnet sln FitnessClub.slnx add src/FitnessClub.Api/FitnessClub.Api.csproj tests/FitnessClub.IntegrationTests/FitnessClub.IntegrationTests.csproj
```

- [ ] **Step 2: Write the test helpers**

`api/tests/FitnessClub.IntegrationTests/Infrastructure/TestAuthHandler.cs`:

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FitnessClub.IntegrationTests.Infrastructure;

// Signs the request in when the X-Test-Roles header is present; its comma-separated value becomes the user's roles.
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out var header))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new("sub", "test-user") };
        claims.AddRange(header.ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, SchemeName, "sub", ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

`api/tests/FitnessClub.IntegrationTests/Infrastructure/FitnessClubApiFactory.cs`:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Infrastructure;

public sealed class FitnessClubApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth0:Domain", "test.invalid");
        builder.UseSetting("Auth0:Audience", "https://api.test");
        builder.UseSetting("Database:InMemoryName", $"integration-{Guid.NewGuid()}");

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

    public HttpClient CreateClientWithRoles(params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }
}
```

- [ ] **Step 3: Write the failing host tests**

`api/tests/FitnessClub.IntegrationTests/HealthTests.cs`:

```csharp
using System.Net;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests;

public class HealthTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    [Fact]
    public async Task Health_is_public_and_returns_200()
    {
        var response = await factory.CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

`api/tests/FitnessClub.IntegrationTests/HostConfigurationTests.cs` (Review Focus items 4 and 5):

```csharp
using System.Net;
using FitnessClub.Application.Common;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace FitnessClub.IntegrationTests;

public class HostConfigurationTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    [Theory]
    [InlineData("/hangfire")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task Development_only_endpoints_are_not_mapped_outside_development(string path)
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Missing_auth0_settings_fail_at_startup()
    {
        using var unconfigured = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Auth0:Domain", "");
            builder.UseSetting("Auth0:Audience", "");
        });

        var exception = Assert.Throws<OptionsValidationException>(() => unconfigured.CreateClient());
        Assert.Contains("Domain", exception.Message);
        Assert.Contains("Audience", exception.Message);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run from `api/`: `dotnet test`
Expected: build FAILS. `FitnessClub.Api` has no entry point (`CS5001: Program does not contain a static 'Main' method`), and the tests can't resolve `Program`.

- [ ] **Step 5: Add Hangfire to Infrastructure**

`api/src/FitnessClub.Infrastructure/BackgroundJobs/RecurringJobs.cs`:

```csharp
using Hangfire;

namespace FitnessClub.Infrastructure.BackgroundJobs;

public static class RecurringJobs
{
    // Single place where recurring jobs are scheduled. Sub-project 4 adds the membership expiry job here.
    public static void Register(IRecurringJobManager recurringJobs)
    {
    }
}
```

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, add `using Hangfire;` after `using FitnessClub.Infrastructure.Persistence;`. Then, in `AddInfrastructure`, add this block between the `AddScoped<IApplicationDbContext>` line and `return services;`:

```csharp
        services.AddHangfire(hangfire =>
        {
            hangfire
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();

            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                hangfire.UseInMemoryStorage();
            else
                hangfire.UseSqlServerStorage(connectionString);
        });
        services.AddHangfireServer();
```

- [ ] **Step 6: Implement auth, error handling and the host**

`api/src/FitnessClub.Api/Auth/Auth0Options.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace FitnessClub.Api.Auth;

public sealed class Auth0Options
{
    public const string SectionName = "Auth0";

    [Required]
    public string Domain { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    [Required]
    public string RolesClaim { get; set; } = "https://fitnessclub/roles";
}
```

`api/src/FitnessClub.Api/Auth/AuthenticationSetup.cs`:

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FitnessClub.Api.Auth;

public static class AuthenticationSetup
{
    public static IServiceCollection AddAuth0Authentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<Auth0Options>()
            .Bind(configuration.GetSection(Auth0Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<Auth0Options>>((jwt, auth0Options) =>
            {
                var auth0 = auth0Options.Value;
                jwt.Authority = $"https://{auth0.Domain}/";
                jwt.Audience = auth0.Audience;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.NameClaimType = "sub";
                jwt.TokenValidationParameters.RoleClaimType = auth0.RolesClaim;
            });

        // Every endpoint requires a signed-in user unless it opts out with AllowAnonymous.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
```

`api/src/FitnessClub.Api/ErrorHandling/ExceptionToProblemDetailsHandler.cs`:

```csharp
using FitnessClub.Application.Common;
using FitnessClub.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace FitnessClub.Api.ErrorHandling;

public sealed class ExceptionToProblemDetailsHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ExceptionToProblemDetailsHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            DomainException e => (StatusCodes.Status400BadRequest, "Invalid request", e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Status = status, Title = title, Detail = detail },
        });
    }
}
```

`api/src/FitnessClub.Api/Program.cs`:

```csharp
using FitnessClub.Api.Auth;
using FitnessClub.Api.ErrorHandling;
using FitnessClub.Application;
using FitnessClub.Infrastructure;
using FitnessClub.Infrastructure.BackgroundJobs;
using Hangfire;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAuth0Authentication(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionToProblemDetailsHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
    // Hangfire's default dashboard filter only allows requests from the local machine.
    app.MapHangfireDashboard("/hangfire").AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

await app.Services.InitializeDatabaseAsync();
RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>());

await app.RunAsync();

public partial class Program;
```

`api/src/FitnessClub.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "FitnessClub": ""
  },
  "Auth0": {
    "Domain": "",
    "Audience": "",
    "RolesClaim": "https://fitnessclub/roles"
  }
}
```

`api/src/FitnessClub.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

`api/src/FitnessClub.Api/Properties/launchSettings.json`:

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run from `api/`: `dotnet test`
Expected: `total: 27, failed: 0, succeeded: 27` (22 unit + 5 integration).

- [ ] **Step 8: Run the real app in Development**

Run from `api/`:

```bash
Auth0__Domain=example.eu.auth0.com Auth0__Audience=https://api.fitnessclub dotnet run --project src/FitnessClub.Api
```

In another terminal:

```bash
for u in /health /openapi/v1.json /hangfire /api/membership-plans; do echo "$u $(curl -s -o /dev/null -w '%{http_code}' http://localhost:5080$u)"; done
```

Expected: `/health 200`, `/openapi/v1.json 200`, `/hangfire 200`, `/api/membership-plans 401`. Stop the app with Ctrl+C.

Then run `dotnet run --project src/FitnessClub.Api` without the variables. Expected: it exits with `OptionsValidationException ... 'Domain' ... 'Audience'`.

- [ ] **Step 9: Commit**

```bash
git add api
git commit -m "feat(api): host with Auth0 JWT auth, problem details, Hangfire and health check"
```

---

### Task 5: Membership plans endpoints

**Files:**
- Create: `api/src/FitnessClub.Api/Controllers/MembershipPlansController.cs`
- Test: `api/tests/FitnessClub.IntegrationTests/MembershipPlans/MembershipPlansEndpointsTests.cs`

**Interfaces:**
- Consumes: `MembershipPlanService`, `MembershipPlanRequest`, `MembershipPlanResponse`, `Roles` (Task 3); `FitnessClubApiFactory.CreateClientWithRoles` (Task 4)
- Produces: HTTP endpoints under `api/membership-plans`, as listed in the spec's API table

- [ ] **Step 1: Write the failing endpoint tests**

`api/tests/FitnessClub.IntegrationTests/MembershipPlans/MembershipPlansEndpointsTests.cs` (includes Review Focus items 1–3):

```csharp
using System.Net;
using System.Net.Http.Json;
using FitnessClub.Application.Common;
using FitnessClub.Application.MembershipPlans;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.IntegrationTests.MembershipPlans;

public class MembershipPlansEndpointsTests(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    private const string BaseUrl = "/api/membership-plans";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Tests in this class share one in-memory database, so every plan gets a unique name.
    private static object NewPlan(decimal price = 800m, int validityDays = 30, int? visitLimit = null) =>
        new { name = $"Plan {Guid.NewGuid():N}", price, validityDays, visitLimit };

    private async Task<MembershipPlanResponse> CreatePlanAsync(object? body = null)
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(BaseUrl, body ?? NewPlan(), Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MembershipPlanResponse>(Ct))!;
    }

    [Fact]
    public async Task Create_as_admin_returns_201_with_location()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(BaseUrl, NewPlan(visitLimit: 10), Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var plan = await response.Content.ReadFromJsonAsync<MembershipPlanResponse>(Ct);
        Assert.NotNull(plan);
        Assert.Equal(10, plan.VisitLimit);
        Assert.True(plan.IsActive);
        Assert.Equal($"{BaseUrl}/{plan.Id}", response.Headers.Location?.AbsolutePath, ignoreCase: true);
    }

    [Fact]
    public async Task Create_without_user_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(BaseUrl, NewPlan(), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_as_receptionist_returns_403()
    {
        var response = await factory.CreateClientWithRoles(Roles.Receptionist).PostAsJsonAsync(BaseUrl, NewPlan(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_as_trainer_returns_403()
    {
        var response = await factory.CreateClientWithRoles(Roles.Trainer).GetAsync(BaseUrl, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "name": "", "price": 100, "validityDays": 30 }""")]
    [InlineData("""{ "name": "No price", "validityDays": 30 }""")]
    [InlineData("""{ "name": "Bad validity", "price": 100, "validityDays": 0 }""")]
    [InlineData("""{ "name": "Bad limit", "price": 100, "validityDays": 30, "visitLimit": 0 }""")]
    [InlineData("""{ "name": "   ", "price": 100, "validityDays": 30 }""")]
    [InlineData("""{ "name": "Text price", "price": "abc", "validityDays": 30 }""")]
    [InlineData("""{ "name": "Huge price", "price": 1e30, "validityDays": 30 }""")]
    [InlineData("""{ "name": "Not json" """)]
    public async Task Create_with_invalid_body_returns_400_problem_details(string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsync(BaseUrl, content, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_with_name_longer_than_100_chars_returns_400()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin)
            .PostAsJsonAsync(BaseUrl, new { name = new string('a', 101), price = 100m, validityDays = 30 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_with_malformed_id_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{BaseUrl}/not-a-guid", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_more_than_two_decimals_returns_400_from_domain_rule()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsJsonAsync(BaseUrl, NewPlan(price: 10.001m), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        Assert.Equal("Price can have at most 2 decimal places.", problem?.Detail);
    }

    [Fact]
    public async Task Create_with_duplicate_name_returns_409()
    {
        var existing = await CreatePlanAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin)
            .PostAsJsonAsync(BaseUrl, new { name = existing.Name.ToUpperInvariant(), price = 100m, validityDays = 30 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Get_as_receptionist_returns_plan()
    {
        var created = await CreatePlanAsync();

        var plan = await factory.CreateClientWithRoles(Roles.Receptionist)
            .GetFromJsonAsync<MembershipPlanResponse>($"{BaseUrl}/{created.Id}", Ct);

        Assert.Equal(created, plan);
    }

    [Fact]
    public async Task Get_missing_plan_returns_404_problem_details()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).GetAsync($"{BaseUrl}/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Update_as_admin_returns_updated_plan()
    {
        var created = await CreatePlanAsync();

        var response = await factory.CreateClientWithRoles(Roles.Admin).PutAsJsonAsync(
            $"{BaseUrl}/{created.Id}", new { name = created.Name, price = 950.50m, validityDays = 60, visitLimit = 20 }, Ct);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<MembershipPlanResponse>(Ct);
        Assert.Equal(created with { Price = 950.50m, ValidityDays = 60, VisitLimit = 20 }, updated);
    }

    [Fact]
    public async Task Update_missing_plan_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PutAsJsonAsync($"{BaseUrl}/{Guid.NewGuid()}", NewPlan(), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivated_plan_is_hidden_from_default_list_and_back_after_activate()
    {
        var plan = await CreatePlanAsync();
        var admin = factory.CreateClientWithRoles(Roles.Admin);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"{BaseUrl}/{plan.Id}/deactivate", null, Ct)).StatusCode);
        var activeOnly = await admin.GetFromJsonAsync<List<MembershipPlanResponse>>(BaseUrl, Ct);
        var all = await admin.GetFromJsonAsync<List<MembershipPlanResponse>>($"{BaseUrl}?includeInactive=true", Ct);
        Assert.DoesNotContain(activeOnly!, p => p.Id == plan.Id);
        Assert.Contains(all!, p => p.Id == plan.Id && !p.IsActive);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"{BaseUrl}/{plan.Id}/activate", null, Ct)).StatusCode);
        var afterActivate = await admin.GetFromJsonAsync<List<MembershipPlanResponse>>(BaseUrl, Ct);
        Assert.Contains(afterActivate!, p => p.Id == plan.Id);
    }

    [Fact]
    public async Task Deactivate_missing_plan_returns_404()
    {
        var response = await factory.CreateClientWithRoles(Roles.Admin).PostAsync($"{BaseUrl}/{Guid.NewGuid()}/deactivate", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_as_receptionist_returns_403()
    {
        var plan = await CreatePlanAsync();

        var response = await factory.CreateClientWithRoles(Roles.Receptionist).PostAsync($"{BaseUrl}/{plan.Id}/deactivate", null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run from `api/`: `dotnet test --project tests/FitnessClub.IntegrationTests --filter-class "FitnessClub.IntegrationTests.MembershipPlans.MembershipPlansEndpointsTests"`
Expected: FAIL. Most tests get `404 Not Found` because no controller maps `api/membership-plans` yet, so 201/400/409/403 assertions fail.

- [ ] **Step 3: Implement the controller**

`api/src/FitnessClub.Api/Controllers/MembershipPlansController.cs`:

```csharp
using FitnessClub.Application.Common;
using FitnessClub.Application.MembershipPlans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessClub.Api.Controllers;

[ApiController]
[Route("api/membership-plans")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Receptionist}")]
public sealed class MembershipPlansController(MembershipPlanService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<MembershipPlanResponse>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        service.ListAsync(includeInactive, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<MembershipPlanResponse> Get(Guid id, CancellationToken cancellationToken) =>
        service.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<MembershipPlanResponse>> Create(MembershipPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = plan.Id }, plan);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public Task<MembershipPlanResponse> Update(Guid id, MembershipPlanRequest request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await service.ActivateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await service.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
```

The class-level `[Authorize]` lets in Admin and Receptionist. A method-level `[Authorize(Roles = Roles.Admin)]` adds a second requirement on top, so write endpoints accept Admin only.

- [ ] **Step 4: Run all tests to verify they pass**

Run from `api/`: `dotnet test`
Expected: `total: 50, failed: 0, succeeded: 50`.

- [ ] **Step 5: Commit**

```bash
git add api
git commit -m "feat(api): membership plan endpoints with role-based access"
```

---

### Task 6: Docker image and compose

**Files:**
- Create: `api/Dockerfile`, `api/.dockerignore`
- Create: `deploy/docker-compose.yml`, `deploy/.env.example`
- Modify: `deploy/CLAUDE.md`

**Interfaces:**
- Consumes: the `FitnessClub.Api` project (Task 4), `/health` (Task 4)
- Produces: a compose service named `api` on port `8080`, configured through `deploy/.env` (`AUTH0_DOMAIN`, `AUTH0_AUDIENCE`)

- [ ] **Step 1: Write the Dockerfile and ignore file**

`api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/FitnessClub.Domain/FitnessClub.Domain.csproj src/FitnessClub.Domain/
COPY src/FitnessClub.Application/FitnessClub.Application.csproj src/FitnessClub.Application/
COPY src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj src/FitnessClub.Infrastructure/
COPY src/FitnessClub.Api/FitnessClub.Api.csproj src/FitnessClub.Api/
RUN dotnet restore src/FitnessClub.Api/FitnessClub.Api.csproj

COPY src/ src/
RUN dotnet publish src/FitnessClub.Api/FitnessClub.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl is only used by the compose health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FitnessClub.Api.dll"]
```

`api/.dockerignore`:

```
**/bin/
**/obj/
tests/
*.md
```

- [ ] **Step 2: Write the compose file and env template**

`deploy/docker-compose.yml`:

```yaml
services:
  api:
    build:
      context: ../api
    ports:
      - "8080:8080"
    environment:
      Auth0__Domain: ${AUTH0_DOMAIN:?Set AUTH0_DOMAIN in deploy/.env}
      Auth0__Audience: ${AUTH0_AUDIENCE:?Set AUTH0_AUDIENCE in deploy/.env}
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:8080/health"]
      interval: 10s
      timeout: 3s
      retries: 5
      start_period: 10s
```

`deploy/.env.example`:

```
# Copy to deploy/.env and fill in from your Auth0 API settings.
AUTH0_DOMAIN=your-tenant.eu.auth0.com
AUTH0_AUDIENCE=https://api.fitnessclub
```

- [ ] **Step 3: Verify that compose requires the env file**

Run from the repo root, without a `deploy/.env` file: `docker compose -f deploy/docker-compose.yml config`
Expected: error `required variable AUTH0_... is missing a value: Set AUTH0_... in deploy/.env`.

- [ ] **Step 4: Build, run and check health**

Run from the repo root:

```bash
cp deploy/.env.example deploy/.env
docker compose -f deploy/docker-compose.yml up --build -d
sleep 20
docker compose -f deploy/docker-compose.yml ps
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/health
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/api/membership-plans
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/hangfire
docker compose -f deploy/docker-compose.yml exec api id -un
docker compose -f deploy/docker-compose.yml down
```

Expected:
- `ps` shows `api` as `(healthy)`.
- `200` for `/health`.
- `401` for the API without a token.
- `401` for `/hangfire`, because the dashboard isn't mapped in Production.
- `app` (non-root) from `id -un`.

`deploy/.env` stays untracked, because `.gitignore` already covers it.

- [ ] **Step 5: Update `deploy/CLAUDE.md`**

Replace the whole file with:

````markdown
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
````

- [ ] **Step 6: Commit**

```bash
git add api/Dockerfile api/.dockerignore deploy/docker-compose.yml deploy/.env.example deploy/CLAUDE.md
git commit -m "feat(deploy): Docker image for the API and compose stack"
```

---

### Task 7: Update project documentation

**Files:**
- Modify: `api/CLAUDE.md` (full rewrite)
- Modify: `CLAUDE.md` (Status section)
- Modify: `docs/Code/Specs/2026-10-05-api-foundation-design.md` (status)

**Interfaces:**
- Consumes: everything from Tasks 2–6
- Produces: docs that future sessions rely on

- [ ] **Step 1: Rewrite `api/CLAUDE.md`**

Replace the whole file with:

````markdown
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
````

- [ ] **Step 2: Update the Status section of the root `CLAUDE.md`**

Replace this paragraph:

```markdown
Nothing has been set up yet. The database, libraries, and tooling are TBD. Ask before choosing any of them.
```

with:

```markdown
- **Backend (`api/`):** foundation built, with membership plans as the first feature. Stack and commands are in `api/CLAUDE.md`. Next backend parts: clients/memberships/visits, trainers/bookings, expiry notifications, reports. Each gets its own spec in `docs/Code/Specs/`.
- **UI (`ui/`):** not started. Its libraries and tooling are TBD. Ask before choosing any.
- **Specs and plans:** design specs go in `docs/Code/Specs/` and implementation plans in `docs/Code/Plans/`, not in Superpowers' default `docs/superpowers/`.
```

- [ ] **Step 3: Mark the spec as implemented**

In `docs/Code/Specs/2026-10-05-api-foundation-design.md`:
- Change the frontmatter line `status: draft` to `status: implemented`.
- Change the table row `| 1 | **Foundation** (this spec) | draft |` to `| 1 | **Foundation** (this spec) | implemented |`.

- [ ] **Step 4: Verify that nothing regressed**

Run from `api/`: `dotnet build && dotnet test`
Expected: `Build succeeded`, `0 Warning(s)`, `total: 50, failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md api/CLAUDE.md docs/Code/Specs/2026-10-05-api-foundation-design.md
git commit -m "docs: document API foundation stack, commands and conventions"
```
