---
tags: [plan, api, ddd]
date: 2026-10-05
spec: "[[2026-10-05-domain-model-and-architecture-design]]"
---

# Domain Model and Clean Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Model the whole fitness club domain as DDD aggregates and put the backend behind enforced Clean Architecture boundaries: repositories, a unit of work and interfaces between every layer, with no comments in code.

**Architecture:**
- **Domain:** eight aggregate roots, each with an `I{Root}Repository`:
  - `MembershipPlan`
  - `Client`, with `Membership` children
  - `Visit`
  - `Payment`
  - `Trainer`, with `WorkingHours` and `ClientAssignment`
  - `Room`
  - `TrainingSession`, with `Booking` children and the `ISessionScheduler` domain service
  - `Notification`

  Value objects live in `SharedKernel`. Domain uses only the BCL.
- **Application:** depends only on Domain. Its public `I*Service` interfaces are implemented by internal services, and it persists only through repositories and `IUnitOfWork`.
- **Infrastructure:** implements everything internally with EF Core:
  - owned types for an aggregate's children
  - an optimistic `Version` token per aggregate root
  - exactly one public class, `DependencyInjection`
- **Api:** controllers inject only Application interfaces.

A new `FitnessClub.ArchitectureTests` project enforces these rules with reflection, NetArchTest and Roslyn.

**Tech Stack:**
- .NET 10 (SDK 10.0.300), ASP.NET Core controllers
- EF Core 10.0.12 (InMemory now, SQL Server later)
- Hangfire 1.8.25
- xUnit v3 4.0.1 on Microsoft.Testing.Platform
- New packages:
  - `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.12
  - `NetArchTest.Rules` 1.3.2
  - `Microsoft.CodeAnalysis.CSharp` 5.0.0

**Spec:** `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`

> **This plan was executed before it was written.** A script applied every task, step by step, to a fresh checkout of `HEAD` (`3cf1895`):
> - Every "confirm it fails" step failed, and every "confirm it passes" step passed with the stated test count.
> - The final tree is byte-identical to the reviewed code: 179 tests, 0 warnings.
> - The Docker image builds, `/health` returns `Healthy`, and `/api/membership-plans` returns 401 without a token.
> - Four parallel review agents (Domain, Application, Infrastructure, Api and tests) audited the code, and their real findings are already folded in.
>
> Copy code blocks exactly. The replacements in later tasks depend on the text from earlier ones.

## Global Constraints

- **Working directory:** run commands from `api/` unless a step says otherwise. Commits run from the repo root.
- **Branch:** start from `dev` at `3cf1895` in an isolated worktree (superpowers:using-git-worktrees). Commit after every task.
- **SDK and build:**
  - `api/global.json` pins SDK `10.0.300` with `rollForward: latestFeature`.
  - Every project targets `net10.0`, with `Nullable`, `ImplicitUsings` and `TreatWarningsAsErrors` on.
- **Packages:**
  - Versions live only in `api/Directory.Packages.props`. A `<PackageReference>` never has a `Version`.
  - `Newtonsoft.Json` stays pinned to `13.0.4`.
- **Test runner:** Microsoft.Testing.Platform. Filters such as `--filter-class` only work together with `--project`.
- **Cancellation tokens:** test code passes `TestContext.Current.CancellationToken` to every async call.
- **No comments:** no `//`, `/* */` or `///` in any `.cs` file under `api/src` or `api/tests`. Names carry the meaning.
- **Layers:**
  - Domain uses only the BCL, never `IQueryable` or expression trees.
  - Application uses only Domain and `Microsoft.Extensions.DependencyInjection.Abstractions`.
  - Infrastructure's only public type is `DependencyInjection` (`AddInfrastructure`, `UseInfrastructureAsync`).
  - Api uses Infrastructure only in `Program`, and Domain only in `ExceptionToProblemDetailsHandler`.
  - Controllers inject only Application `I*Service` interfaces.
- **Aggregates:**
  - Entities and value objects are `sealed`, with private setters, no public fields, and private constructors behind static factories.
  - Children change only through their root.
  - Other aggregates are referenced by `Guid` id. Passing another aggregate into a method to read it is fine.
- **Persistence:**
  - Repositories exist only for aggregate roots and never save.
  - Each use case calls `IUnitOfWork.SaveChangesAsync` exactly once, after all changes.
  - A stale save raises `ConflictException` (409).
- **Time:** domain methods take `DateTimeOffset now`, and calendar dates are read in that value's own offset.
- **HTTP contract:** membership plans keep their routes, roles and payloads. The only change is the problem-details text for sub-cent prices, `Amount can have at most 2 decimal places.`

## Review Focus

1. **Two receptionists save the same client or plan at the same time.** The second save gets a 409, never a silent lost update. This holds even when only a child row (a membership) changed. Pinned in Task 3 by `Saving_a_stale_copy_raises_conflict`, and in Task 5 by `Concurrent_changes_to_the_same_client_raise_conflict` and `Changing_only_a_child_entity_still_bumps_the_aggregate_version`.
2. **A client's card is scanned twice in one day.** The second check-in is rejected and doesn't use up a visit from a pack. Pinned in Task 5 by `CheckIn_twice_on_the_same_day_throws_and_uses_one_visit`.
3. **A booking that can't happen is refused:**
   - a client booked into two overlapping sessions
   - a booking for a full session or a cancelled session
   - a booking after the session started

   Pinned in Task 7 by `Book_client_into_overlapping_session_throws`, `Book_when_full_throws`, `Cancelled_session_rejects_bookings_and_second_cancel` and `Book_after_session_started_throws`.
4. **No expiry notice goes out for a membership that was already renewed, cancelled, ended or used up.** Pinned in Task 5 by the `NeedsExpiryNotice_*` tests and in Task 8 by `MembershipExpiring_after_renewal_throws` and `MembershipExpiring_for_cancelled_membership_throws`.
5. **Malformed input becomes a 400, never a 500.** This covers a phone number with letters, an email with a display name, and a price with sub-cent digits. Pinned in Task 2 by `ValueObjectTests` and the HTTP test `Create_with_more_than_two_decimals_returns_400_from_domain_rule`.

---


### Task 1: Architecture test project and the "no comments" rule

**Files:**
- Modify: `api/Directory.Packages.props`, `api/FitnessClub.slnx`
- Create: `api/tests/FitnessClub.ArchitectureTests/FitnessClub.ArchitectureTests.csproj`, `Layers.cs`, `SourceCodeTests.cs`
- Modify (delete comment lines): `api/src/FitnessClub.Api/Program.cs`, `api/src/FitnessClub.Api/Auth/AuthenticationSetup.cs`, `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, `api/src/FitnessClub.Infrastructure/BackgroundJobs/RecurringJobs.cs`, `api/src/FitnessClub.Application/MembershipPlans/MembershipPlanService.cs`, `api/tests/FitnessClub.IntegrationTests/Auth/Auth0JwtRoleMappingTests.cs`, `api/tests/FitnessClub.IntegrationTests/Auth/RealJwtApiFactory.cs`, `api/tests/FitnessClub.IntegrationTests/Infrastructure/TestAuthHandler.cs`, `api/tests/FitnessClub.IntegrationTests/MembershipPlans/MembershipPlansEndpointsTests.cs`

**Interfaces:**
- Consumes: the current `HEAD` of branch `dev` (`3cf1895`).
- Produces: `FitnessClub.ArchitectureTests` project (references Api, so it sees every layer), with `internal static class Layers` exposing `Assembly Domain`, `Application`, `Infrastructure`, `Api`, the extension methods `ReferencedAssemblyNames()`, `DeclaredTypes()`, `ConcreteClasses()` and `NetArchTest.Rules.TestResult.Describe()`. Later tasks add rule classes to this project.

- [ ] **Step 1: Start from a clean worktree**

Work in a fresh worktree from `3cf1895`. The main checkout has an uncommitted edit to `api/tests/FitnessClub.IntegrationTests/FitnessClub.IntegrationTests.csproj` that drops the `FitnessClub.Api` project reference (likely from the IDE), and with it nothing builds (`CS0246: The type or namespace name 'Program' could not be found`). The committed file is correct. Don't carry that edit over.

- [ ] **Step 2: Run and confirm it passes**

Run from `api/`:

```sh
dotnet test
```

Expected: PASS: 54 tests (the baseline).

- [ ] **Step 3: Write the failing tests**

Add the architecture test project with the first rule: no comments in any `.cs` file under `src/` or `tests/`. It parses each file with Roslyn and looks at comment trivia, so URLs and `//` inside strings are not false positives. The Newtonsoft pin keeps working without its XML comment; its reason moves to `api/CLAUDE.md` in Task 10.

In `api/Directory.Packages.props`, replace:

````xml
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
````

with:

````xml
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />
````

In `api/Directory.Packages.props`, delete:

````xml
    <!-- Pinned: Hangfire.Core pulls a vulnerable Newtonsoft.Json 11.x transitively -->
````

In `api/Directory.Packages.props`, replace:

````xml
    <PackageVersion Include="Scalar.AspNetCore" Version="2.17.13" />
````

with:

````xml
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
    <PackageVersion Include="Scalar.AspNetCore" Version="2.17.13" />
````

In `api/FitnessClub.slnx`, replace:

````xml
  <Folder Name="/tests/">
````

with:

````xml
  <Folder Name="/tests/">
    <Project Path="tests/FitnessClub.ArchitectureTests/FitnessClub.ArchitectureTests.csproj" />
````

`api/tests/FitnessClub.ArchitectureTests/FitnessClub.ArchitectureTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="NetArchTest.Rules" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\FitnessClub.Api\FitnessClub.Api.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

`api/tests/FitnessClub.ArchitectureTests/Layers.cs`:

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using FitnessClub.Domain.Common;

namespace FitnessClub.ArchitectureTests;

internal static class Layers
{
    public static readonly Assembly Domain = typeof(Entity).Assembly;
    public static readonly Assembly Application = typeof(FitnessClub.Application.DependencyInjection).Assembly;
    public static readonly Assembly Infrastructure = typeof(FitnessClub.Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly Api = typeof(Program).Assembly;

    public static IEnumerable<string> ReferencedAssemblyNames(this Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name!);

    public static IEnumerable<Type> DeclaredTypes(this Assembly assembly) =>
        assembly.GetTypes().Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) && !type.Name.Contains('<'));

    public static IEnumerable<Type> ConcreteClasses(this Assembly assembly) =>
        assembly.DeclaredTypes().Where(type => type is { IsClass: true, IsAbstract: false });

    public static string Describe(this NetArchTest.Rules.TestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);
}
```

`api/tests/FitnessClub.ArchitectureTests/SourceCodeTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FitnessClub.ArchitectureTests;

public class SourceCodeTests
{
    private static readonly SyntaxKind[] CommentKinds =
    [
        SyntaxKind.SingleLineCommentTrivia,
        SyntaxKind.MultiLineCommentTrivia,
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia,
    ];

    [Fact]
    public void Source_files_contain_no_comments()
    {
        var root = SolutionRoot();
        var files = new[] { "src", "tests" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .ToList();

        var offenders = files
            .SelectMany(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)
                .GetRoot(TestContext.Current.CancellationToken)
                .DescendantTrivia(descendIntoTrivia: true)
                .Where(trivia => CommentKinds.Contains(trivia.Kind()))
                .Select(trivia => $"{Path.GetRelativePath(root, path)}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"));

        Assert.NotEmpty(files);
        Assert.Empty(offenders);
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj");

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FitnessClub.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("FitnessClub.slnx was not found.");
    }
}
```

- [ ] **Step 4: Run and confirm it fails**

Run from `api/`:

```sh
dotnet test --project tests/FitnessClub.ArchitectureTests
```

Expected: FAIL: `Source_files_contain_no_comments` lists 10 `file:line` locations, one per comment line deleted in the next step (for example `src/FitnessClub.Api/Program.cs:32`).

- [ ] **Step 5: Implement**

Delete these comment lines exactly (and nothing else):

In `api/src/FitnessClub.Api/Program.cs`, delete:

````csharp
    // Hangfire's default dashboard filter only allows requests from the local machine.
````

In `api/src/FitnessClub.Api/Auth/AuthenticationSetup.cs`, delete:

````csharp
        // Every endpoint requires a signed-in user unless it opts out with AllowAnonymous.
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, delete:

````csharp
    // Applies pending EF migrations when a relational database is configured; the InMemory provider has no migrations.
````

In `api/src/FitnessClub.Infrastructure/BackgroundJobs/RecurringJobs.cs`, delete:

````csharp
    // Single place where recurring jobs are scheduled. Sub-project 4 adds the membership expiry job here.
````

In `api/src/FitnessClub.Application/MembershipPlans/MembershipPlanService.cs`, delete:

````csharp
    // InMemory does not enforce unique indexes, so uniqueness is checked here.
````

In `api/tests/FitnessClub.IntegrationTests/Auth/Auth0JwtRoleMappingTests.cs`, delete:

````csharp
// Exercises the real JWT bearer pipeline: roles must come from the Auth0 custom claim.
````

In `api/tests/FitnessClub.IntegrationTests/Auth/RealJwtApiFactory.cs`, delete:

````csharp
// Keeps the real JWT bearer setup from AuthenticationSetup; only the Auth0 metadata download
// is replaced by a local issuer and signing key, so tokens can be minted in-process.
````

In `api/tests/FitnessClub.IntegrationTests/Infrastructure/TestAuthHandler.cs`, delete:

````csharp
// Signs the request in when the X-Test-Roles header is present; its comma-separated value becomes the user's roles.
````

In `api/tests/FitnessClub.IntegrationTests/MembershipPlans/MembershipPlansEndpointsTests.cs`, delete:

````csharp
    // Tests in this class share one in-memory database, so every plan gets a unique name.
````

- [ ] **Step 6: Run and confirm it passes**

Run from `api/`:

```sh
dotnet test
```

Expected: PASS: 55 tests.

- [ ] **Step 7: Commit**

From the repo root:

```sh
git add api/Directory.Packages.props api/FitnessClub.slnx api/src api/tests
git commit -m "build(api): add architecture tests and forbid code comments"
```


### Task 2: Domain building blocks, shared kernel and `MembershipPlan` as an aggregate root

**Files:**
- Create: `api/src/FitnessClub.Domain/Common/AggregateRoot.cs`, `IRepository.cs`, `DateTimeOffsetExtensions.cs`
- Create: `api/src/FitnessClub.Domain/SharedKernel/Money.cs`, `PhoneNumber.cs`, `EmailAddress.cs`, `PersonName.cs`, `TimeSlot.cs`
- Modify: `api/src/FitnessClub.Domain/MembershipPlans/MembershipPlan.cs`; Create: `IMembershipPlanRepository.cs`
- Create: `api/src/FitnessClub.Infrastructure/Persistence/Configurations/ValueConversions.cs`; Modify: `MembershipPlanConfiguration.cs`
- Modify: `api/src/FitnessClub.Application/MembershipPlans/MembershipPlanService.cs`, `MembershipPlanResponse.cs` (call sites only)
- Test: `api/tests/FitnessClub.UnitTests/Domain/TestData.cs` (new), `ValueObjectTests.cs` (new), `MembershipPlanTests.cs` (rewritten), `api/tests/FitnessClub.IntegrationTests/MembershipPlans/MembershipPlansEndpointsTests.cs` (one message)

**Interfaces:**
- Consumes: `FitnessClub.Domain.Common.Entity` (`Guid Id`), `DomainException(string)`.
- Produces (namespace `FitnessClub.Domain.Common`): `abstract class AggregateRoot : Entity`; `interface IRepository<TAggregate> where TAggregate : AggregateRoot { Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken); void Add(TAggregate aggregate); }`; `internal static class DateTimeOffsetExtensions { DateOnly ToDateOnly(this DateTimeOffset); TimeOnly ToTimeOnly(this DateTimeOffset); }`.
- Produces (namespace `FitnessClub.Domain.SharedKernel`, all `sealed record`, private ctor): `Money.Of(decimal)` → `.Amount`; `PhoneNumber.Create(string)` → `.Value`, `MaxLength = 16`; `EmailAddress.Create(string)` → `.Value`, `MaxLength = 254`; `PersonName.Create(first, last, middle?)` → `.FirstName`, `.LastName`, `.MiddleName`, `.FullName`, `PartMaxLength = 100`; `TimeSlot.Create(start, end)` → `.Start`, `.End`, `.Duration`, `Overlaps(TimeSlot)`.
- Produces: `MembershipPlan : AggregateRoot` with `Money Price`, `Create(string name, Money price, int validityDays, int? visitLimit)`, `Update(...)` same shape; `interface IMembershipPlanRepository : IRepository<MembershipPlan> { Task<IReadOnlyList<MembershipPlan>> ListAsync(bool includeInactive, CancellationToken); Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken); }`.
- Produces (Infrastructure, internal): `ValueConversions.HasMoneyConversion()`, `HasPhoneConversion()`, `HasEmailConversion()`, `OwnsPersonName(...)` extension methods for entity configurations.

- [ ] **Step 1: Write the failing tests**

Write the failing tests. `TestData` starts small and grows in later tasks. The HTTP contract changes in one place only: the sub-cent price message now comes from `Money`.

`api/tests/FitnessClub.UnitTests/Domain/TestData.cs`:

```csharp
namespace FitnessClub.UnitTests.Domain;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 5);
}
```

`api/tests/FitnessClub.UnitTests/Domain/ValueObjectTests.cs`:

```csharp
using System.Globalization;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.UnitTests.Domain;

public class ValueObjectTests
{
    [Theory]
    [InlineData("-1")]
    [InlineData("10.001")]
    public void Money_rejects_negative_or_sub_cent_amounts(string amount)
    {
        Assert.Throws<DomainException>(() => Money.Of(decimal.Parse(amount, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Money_with_same_amount_is_equal()
    {
        Assert.Equal(Money.Of(10.5m), Money.Of(10.50m));
    }

    [Theory]
    [InlineData("+38 (067) 123-45-67", "+380671234567")]
    [InlineData("0671234567", "0671234567")]
    public void PhoneNumber_is_normalized_to_digits(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumber.Create(input).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("+38067abc4567")]
    [InlineData("1234567890123456")]
    [InlineData("38+0671234567")]
    public void PhoneNumber_rejects_invalid_input(string input)
    {
        Assert.Throws<DomainException>(() => PhoneNumber.Create(input));
    }

    [Fact]
    public void EmailAddress_is_trimmed_and_lowercased()
    {
        Assert.Equal("olena@example.com", EmailAddress.Create("  Olena@Example.COM ").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("olena")]
    [InlineData("olena@localhost")]
    [InlineData("Olena <olena@example.com>")]
    public void EmailAddress_rejects_invalid_input(string input)
    {
        Assert.Throws<DomainException>(() => EmailAddress.Create(input));
    }

    [Fact]
    public void PersonName_trims_parts_and_builds_full_name()
    {
        var name = PersonName.Create(" Olena ", " Shevchenko ", " Petrivna ");

        Assert.Equal("Shevchenko Olena Petrivna", name.FullName);
    }

    [Fact]
    public void PersonName_treats_blank_middle_name_as_missing()
    {
        Assert.Null(PersonName.Create("Olena", "Shevchenko", "  ").MiddleName);
    }

    [Theory]
    [InlineData("", "Shevchenko")]
    [InlineData("Olena", " ")]
    public void PersonName_requires_first_and_last_name(string firstName, string lastName)
    {
        Assert.Throws<DomainException>(() => PersonName.Create(firstName, lastName, null));
    }

    [Fact]
    public void TimeSlot_must_end_after_start()
    {
        var start = TestData.Now;

        Assert.Throws<DomainException>(() => TimeSlot.Create(start, start));
    }

    [Fact]
    public void TimeSlots_touching_at_the_edge_do_not_overlap()
    {
        var first = TimeSlot.Create(TestData.Now, TestData.Now.AddHours(1));
        var second = TimeSlot.Create(TestData.Now.AddHours(1), TestData.Now.AddHours(2));

        Assert.False(first.Overlaps(second));
        Assert.True(first.Overlaps(TimeSlot.Create(TestData.Now.AddMinutes(30), TestData.Now.AddHours(2))));
    }
}
```

`api/tests/FitnessClub.UnitTests/Domain/MembershipPlanTests.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.UnitTests.Domain;

public class MembershipPlanTests
{
    private static readonly Money Price = Money.Of(800m);

    [Fact]
    public void Create_with_valid_values_sets_fields_and_is_active()
    {
        var plan = MembershipPlan.Create("  Monthly  ", Price, 30, null);

        Assert.NotEqual(Guid.Empty, plan.Id);
        Assert.Equal("Monthly", plan.Name);
        Assert.Equal(Price, plan.Price);
        Assert.Equal(30, plan.ValidityDays);
        Assert.Null(plan.VisitLimit);
        Assert.True(plan.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_blank_name_throws(string name)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create(name, Price, 30, null));
    }

    [Fact]
    public void Create_with_name_longer_than_max_throws()
    {
        var name = new string('a', MembershipPlan.NameMaxLength + 1);

        Assert.Throws<DomainException>(() => MembershipPlan.Create(name, Price, 30, null));
    }

    [Fact]
    public void Create_with_zero_price_throws()
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", Money.Of(0m), 30, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MembershipPlan.MaxValidityDays + 1)]
    public void Create_with_validity_out_of_range_throws(int validityDays)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", Price, validityDays, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MembershipPlan.MaxVisitLimit + 1)]
    public void Create_with_visit_limit_out_of_range_throws(int visitLimit)
    {
        Assert.Throws<DomainException>(() => MembershipPlan.Create("Plan", Price, 30, visitLimit));
    }

    [Fact]
    public void Update_with_invalid_values_leaves_plan_unchanged()
    {
        var plan = MembershipPlan.Create("Monthly", Price, 30, null);

        Assert.Throws<DomainException>(() => plan.Update("Yearly", Price, 0, null));

        Assert.Equal("Monthly", plan.Name);
        Assert.Equal(30, plan.ValidityDays);
    }

    [Fact]
    public void Deactivate_then_activate_toggles_IsActive()
    {
        var plan = MembershipPlan.Create("Monthly", Price, 30, null);

        plan.Deactivate();
        Assert.False(plan.IsActive);

        plan.Activate();
        Assert.True(plan.IsActive);
    }
}
```

In `api/tests/FitnessClub.IntegrationTests/MembershipPlans/MembershipPlansEndpointsTests.cs`, replace:

````csharp
Assert.Equal("Price can have at most 2 decimal places.", problem?.Detail);
````

with:

````csharp
Assert.Equal("Amount can have at most 2 decimal places.", problem?.Detail);
````

- [ ] **Step 2: Run and confirm it fails**

Run from `api/`:

```sh
dotnet test --project tests/FitnessClub.UnitTests --filter-class "FitnessClub.UnitTests.Domain.ValueObjectTests"
```

Expected: FAIL: build error `CS0234: The type or namespace name 'SharedKernel' does not exist in the namespace 'FitnessClub.Domain'`.

- [ ] **Step 3: Implement**

Implement the building blocks and value objects, and make `MembershipPlan` an aggregate root priced in `Money`:

`api/src/FitnessClub.Domain/Common/AggregateRoot.cs`:

```csharp
namespace FitnessClub.Domain.Common;

public abstract class AggregateRoot : Entity;
```

`api/src/FitnessClub.Domain/Common/IRepository.cs`:

```csharp
namespace FitnessClub.Domain.Common;

public interface IRepository<TAggregate> where TAggregate : AggregateRoot
{
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(TAggregate aggregate);
}
```

`api/src/FitnessClub.Domain/Common/DateTimeOffsetExtensions.cs`:

```csharp
namespace FitnessClub.Domain.Common;

internal static class DateTimeOffsetExtensions
{
    public static DateOnly ToDateOnly(this DateTimeOffset value) => DateOnly.FromDateTime(value.DateTime);

    public static TimeOnly ToTimeOnly(this DateTimeOffset value) => TimeOnly.FromDateTime(value.DateTime);
}
```

`api/src/FitnessClub.Domain/SharedKernel/Money.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.SharedKernel;

public sealed record Money
{
    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static Money Of(decimal amount)
    {
        if (amount < 0)
            throw new DomainException("Amount cannot be negative.");

        if (decimal.Round(amount, 2) != amount)
            throw new DomainException("Amount can have at most 2 decimal places.");

        return new Money(amount);
    }
}
```

`api/src/FitnessClub.Domain/SharedKernel/PhoneNumber.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.SharedKernel;

public sealed record PhoneNumber
{
    public const int MinDigits = 10;
    public const int MaxDigits = 15;
    public const int MaxLength = MaxDigits + 1;

    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Phone number is required.");

        var trimmed = value.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var body = hasPlus ? trimmed[1..] : trimmed;

        if (body.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '(' or ')')))
            throw new DomainException("Phone number can contain only digits, spaces, dashes, parentheses and a leading plus.");

        var digits = new string(body.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < MinDigits or > MaxDigits)
            throw new DomainException($"Phone number must have {MinDigits} to {MaxDigits} digits.");

        return new PhoneNumber(hasPlus ? $"+{digits}" : digits);
    }

    public override string ToString() => Value;
}
```

`api/src/FitnessClub.Domain/SharedKernel/EmailAddress.cs`:

```csharp
using System.Net.Mail;
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.SharedKernel;

public sealed record EmailAddress
{
    public const int MaxLength = 254;

    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static EmailAddress Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email is required.");

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new DomainException($"Email must be at most {MaxLength} characters.");

        if (!MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed || !address.Host.Contains('.'))
            throw new DomainException("Email is not valid.");

        return new EmailAddress(trimmed.ToLowerInvariant());
    }

    public override string ToString() => Value;
}
```

`api/src/FitnessClub.Domain/SharedKernel/PersonName.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.SharedKernel;

public sealed record PersonName
{
    public const int PartMaxLength = 100;

    public string FirstName { get; private init; } = null!;
    public string LastName { get; private init; } = null!;
    public string? MiddleName { get; private init; }

    private PersonName()
    {
    }

    public static PersonName Create(string firstName, string lastName, string? middleName) =>
        new()
        {
            FirstName = Required(firstName, "First name"),
            LastName = Required(lastName, "Last name"),
            MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : Limited(middleName.Trim(), "Middle name"),
        };

    public string FullName => MiddleName is null ? $"{LastName} {FirstName}" : $"{LastName} {FirstName} {MiddleName}";

    private static string Required(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{field} is required.");

        return Limited(value.Trim(), field);
    }

    private static string Limited(string value, string field) =>
        value.Length > PartMaxLength
            ? throw new DomainException($"{field} must be at most {PartMaxLength} characters.")
            : value;
}
```

`api/src/FitnessClub.Domain/SharedKernel/TimeSlot.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.SharedKernel;

public sealed record TimeSlot
{
    public DateTimeOffset Start { get; private init; }
    public DateTimeOffset End { get; private init; }

    private TimeSlot()
    {
    }

    public static TimeSlot Create(DateTimeOffset start, DateTimeOffset end) =>
        end <= start
            ? throw new DomainException("A time slot must end after it starts.")
            : new TimeSlot { Start = start, End = end };

    public TimeSpan Duration => End - Start;

    public bool Overlaps(TimeSlot other) => Start < other.End && other.Start < End;
}
```

`api/src/FitnessClub.Domain/MembershipPlans/MembershipPlan.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.MembershipPlans;

public sealed class MembershipPlan : AggregateRoot
{
    public const int NameMaxLength = 100;
    public const int MaxValidityDays = 3650;
    public const int MaxVisitLimit = 1000;

    public string Name { get; private set; } = null!;
    public Money Price { get; private set; } = null!;
    public int ValidityDays { get; private set; }
    public int? VisitLimit { get; private set; }
    public bool IsActive { get; private set; }

    private MembershipPlan()
    {
    }

    public static MembershipPlan Create(string name, Money price, int validityDays, int? visitLimit)
    {
        var plan = new MembershipPlan { IsActive = true };
        plan.Update(name, price, validityDays, visitLimit);
        return plan;
    }

    public void Update(string name, Money price, int validityDays, int? visitLimit)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Plan name is required.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
            throw new DomainException($"Plan name must be at most {NameMaxLength} characters.");

        if (price.Amount <= 0)
            throw new DomainException("Price must be greater than zero.");

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

`api/src/FitnessClub.Domain/MembershipPlans/IMembershipPlanRepository.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.MembershipPlans;

public interface IMembershipPlanRepository : IRepository<MembershipPlan>
{
    Task<IReadOnlyList<MembershipPlan>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement**

Map `Money` in EF and fix the existing call sites. The service still uses `IApplicationDbContext` here; Task 3 replaces it.

`api/src/FitnessClub.Infrastructure/Persistence/Configurations/ValueConversions.cs`:

```csharp
using FitnessClub.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal static class ValueConversions
{
    public static PropertyBuilder<Money> HasMoneyConversion(this PropertyBuilder<Money> property) =>
        property.HasConversion(money => money.Amount, amount => Money.Of(amount)).HasPrecision(18, 2);

    public static PropertyBuilder<PhoneNumber> HasPhoneConversion(this PropertyBuilder<PhoneNumber> property) =>
        property.HasConversion(phone => phone.Value, value => PhoneNumber.Create(value)).HasMaxLength(PhoneNumber.MaxLength);

    public static PropertyBuilder<EmailAddress?> HasEmailConversion(this PropertyBuilder<EmailAddress?> property) =>
        property.HasConversion(email => email!.Value, value => EmailAddress.Create(value)).HasMaxLength(EmailAddress.MaxLength);

    public static void OwnsPersonName<TOwner>(this EntityTypeBuilder<TOwner> builder, System.Linq.Expressions.Expression<Func<TOwner, PersonName?>> navigation)
        where TOwner : class =>
        builder.OwnsOne(navigation, name =>
        {
            name.Property(n => n.FirstName).HasColumnName("FirstName").HasMaxLength(PersonName.PartMaxLength).IsRequired();
            name.Property(n => n.LastName).HasColumnName("LastName").HasMaxLength(PersonName.PartMaxLength).IsRequired();
            name.Property(n => n.MiddleName).HasColumnName("MiddleName").HasMaxLength(PersonName.PartMaxLength);
        });
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
        builder.Property(p => p.Price).HasMoneyConversion();
    }
}
```

In `api/src/FitnessClub.Application/MembershipPlans/MembershipPlanService.cs`, replace:

````csharp
using FitnessClub.Domain.MembershipPlans;
````

with:

````csharp
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;
````

In `api/src/FitnessClub.Application/MembershipPlans/MembershipPlanService.cs`, replace:

````csharp
MembershipPlan.Create(request.Name, request.Price, request.ValidityDays, request.VisitLimit);
````

with:

````csharp
MembershipPlan.Create(request.Name, Money.Of(request.Price), request.ValidityDays, request.VisitLimit);
````

In `api/src/FitnessClub.Application/MembershipPlans/MembershipPlanService.cs`, replace:

````csharp
plan.Update(request.Name, request.Price, request.ValidityDays, request.VisitLimit);
````

with:

````csharp
plan.Update(request.Name, Money.Of(request.Price), request.ValidityDays, request.VisitLimit);
````

In `api/src/FitnessClub.Application/MembershipPlans/MembershipPlanResponse.cs`, replace:

````csharp
plan.Price, plan.ValidityDays
````

with:

````csharp
plan.Price.Amount, plan.ValidityDays
````

- [ ] **Step 5: Run and confirm it passes**

Run from `api/`:

```sh
dotnet test
```

Expected: PASS: 74 tests.

- [ ] **Step 6: Commit**

From the repo root:

```sh
git add api/src api/tests
git commit -m "feat(api): domain building blocks, shared kernel value objects and MembershipPlan aggregate"
```


### Task 3: Repositories, unit of work and interfaces: membership plans through clean boundaries

**Files:**
- Modify: `api/Directory.Packages.props`, `api/src/FitnessClub.Application/FitnessClub.Application.csproj`, `api/src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj`, `api/tests/FitnessClub.UnitTests/FitnessClub.UnitTests.csproj`
- Delete: `api/src/FitnessClub.Application/Abstractions/IApplicationDbContext.cs`
- Create: `api/src/FitnessClub.Application/Abstractions/IUnitOfWork.cs`, `MembershipPlans/IMembershipPlanService.cs`; Modify: `MembershipPlanService.cs`, `DependencyInjection.cs`
- Create: `api/src/FitnessClub.Infrastructure/Persistence/UnitOfWork.cs`, `Repositories/Repository.cs`, `Repositories/MembershipPlanRepository.cs`; Modify: `FitnessClubDbContext.cs`, `DependencyInjection.cs`, `BackgroundJobs/RecurringJobs.cs`
- Modify: `api/src/FitnessClub.Api/Program.cs`, `Controllers/MembershipPlansController.cs`
- Test: `api/tests/FitnessClub.UnitTests/Fakes/FakeUnitOfWork.cs`, `Fakes/InMemoryMembershipPlanRepository.cs`, `Application/MembershipPlanServiceTests.cs`
- Test: `api/tests/FitnessClub.IntegrationTests/Persistence/PersistenceTestBase.cs`, `Persistence/MembershipPlanRepositoryTests.cs`
- Test: `api/tests/FitnessClub.ArchitectureTests/LayerDependencyTests.cs`, `DomainModelTests.cs`, `BoundaryTests.cs`

**Interfaces:**
- Consumes: Task 2 `IRepository<T>`, `IMembershipPlanRepository`, `Money`, `MembershipPlan`.
- Produces (Application): `interface IUnitOfWork { Task SaveChangesAsync(CancellationToken cancellationToken); }`; `interface IMembershipPlanService` with `ListAsync(bool, ct)`, `GetAsync(Guid, ct)`, `CreateAsync(MembershipPlanRequest, ct)`, `UpdateAsync(Guid, MembershipPlanRequest, ct)`, `ActivateAsync(Guid, ct)`, `DeactivateAsync(Guid, ct)`; `internal sealed class MembershipPlanService(IMembershipPlanRepository plans, IUnitOfWork unitOfWork)`. `InternalsVisibleTo FitnessClub.UnitTests`.
- Produces (Infrastructure, all internal): `FitnessClubDbContext` with `const string VersionProperty = "Version"` (a shadow `Guid` concurrency token on every aggregate root, re-stamped on save whenever the root or any owned child changed); `UnitOfWork` (turns `DbUpdateConcurrencyException` into `ConflictException`); `abstract class Repository<TAggregate>(FitnessClubDbContext db)` with `protected DbSet<TAggregate> Set`; `MembershipPlanRepository`. Public surface is only `DependencyInjection.AddInfrastructure(IServiceCollection, IConfiguration)` and `UseInfrastructureAsync(this WebApplication)` (maps the Hangfire dashboard in Development, migrates a relational database, registers recurring jobs).
- Produces (tests): `PersistenceTestBase(FitnessClubApiFactory factory)` with `Factory`, `Ct`, `Now`, `Today`, `SaveAsync<TRepository>(Action<TRepository>)`, `ReadAsync<TRepository, TResult>(Func<TRepository, Task<TResult>>)`, `ChangeAsync<TRepository>(Func<TRepository, Task>)`, `InUnitOfWorkAsync(Func<IServiceProvider, Task>)`, `UniquePhone()`. Each helper opens its own DI scope, so every read sees what is really stored.

- [ ] **Step 1: Write the failing tests**

Unit tests no longer reference Infrastructure. The service is tested against hand-written fakes, including "nothing is saved when a rule fails":

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
    <ProjectReference Include="..\..\src\FitnessClub.Application\FitnessClub.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

`api/tests/FitnessClub.UnitTests/Fakes/FakeUnitOfWork.cs`:

```csharp
using FitnessClub.Application.Abstractions;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
```

`api/tests/FitnessClub.UnitTests/Fakes/InMemoryMembershipPlanRepository.cs`:

```csharp
using FitnessClub.Domain.MembershipPlans;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class InMemoryMembershipPlanRepository : IMembershipPlanRepository
{
    private readonly List<MembershipPlan> _plans = [];

    public Task<MembershipPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_plans.FirstOrDefault(p => p.Id == id));

    public void Add(MembershipPlan aggregate) => _plans.Add(aggregate);

    public Task<IReadOnlyList<MembershipPlan>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MembershipPlan>>(
            _plans.Where(p => includeInactive || p.IsActive).OrderBy(p => p.Name).ToList());

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(_plans.Any(p => p.Id != excludeId && string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)));
}
```

`api/tests/FitnessClub.UnitTests/Application/MembershipPlanServiceTests.cs`:

```csharp
using FitnessClub.Application.Common;
using FitnessClub.Application.MembershipPlans;
using FitnessClub.Domain.Common;
using FitnessClub.UnitTests.Fakes;

namespace FitnessClub.UnitTests.Application;

public class MembershipPlanServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MembershipPlanService _service;

    public MembershipPlanServiceTests()
    {
        _service = new MembershipPlanService(new InMemoryMembershipPlanRepository(), _unitOfWork);
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
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_with_duplicate_name_ignoring_case_and_spaces_throws_conflict_and_does_not_save()
    {
        await _service.CreateAsync(Request("Monthly"), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(Request("  MONTHLY "), Ct));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_with_invalid_domain_values_throws_domain_exception()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(Request(price: 10.001m), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
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
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_to_another_plans_name_throws_conflict()
    {
        await _service.CreateAsync(Request("Monthly"), Ct);
        var yearly = await _service.CreateAsync(Request("Yearly", 8000m, 365), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(yearly.Id, Request("monthly"), Ct));

        Assert.Equal("Yearly", (await _service.GetAsync(yearly.Id, Ct)).Name);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_for_missing_plan_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(Guid.NewGuid(), Request(), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DeactivateAsync_for_missing_plan_throws_not_found_and_does_not_save()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeactivateAsync(Guid.NewGuid(), Ct));
        Assert.Equal(0, _unitOfWork.SaveCount);
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

- [ ] **Step 2: Write the failing tests**

Repository round-trip tests go through the real DI container and EF InMemory:

`api/tests/FitnessClub.IntegrationTests/Persistence/PersistenceTestBase.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Persistence;

public abstract class PersistenceTestBase(FitnessClubApiFactory factory) : IClassFixture<FitnessClubApiFactory>
{
    protected FitnessClubApiFactory Factory { get; } = factory;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    protected static readonly DateOnly Today = new(2026, 10, 5);

    protected async Task SaveAsync<TRepository>(Action<TRepository> change) where TRepository : notnull
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        change(scope.ServiceProvider.GetRequiredService<TRepository>());
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
    }

    protected async Task<TResult> ReadAsync<TRepository, TResult>(Func<TRepository, Task<TResult>> read) where TRepository : notnull
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<TRepository>());
    }

    protected Task ChangeAsync<TRepository>(Func<TRepository, Task> change) where TRepository : notnull =>
        InUnitOfWorkAsync(services => change(services.GetRequiredService<TRepository>()));

    protected async Task InUnitOfWorkAsync(Func<IServiceProvider, Task> work)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
    }

    protected static string UniquePhone() => $"+380{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
}
```

`api/tests/FitnessClub.IntegrationTests/Persistence/MembershipPlanRepositoryTests.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Persistence;

public class MembershipPlanRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    [Fact]
    public async Task Plan_round_trips_with_money_price()
    {
        var plan = MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(799.99m), 30, 12);
        await SaveAsync<IMembershipPlanRepository>(plans => plans.Add(plan));

        var loaded = await ReadAsync<IMembershipPlanRepository, MembershipPlan?>(plans => plans.GetByIdAsync(plan.Id, Ct));

        Assert.NotNull(loaded);
        Assert.Equal(Money.Of(799.99m), loaded.Price);
        Assert.Equal(12, loaded.VisitLimit);
    }

    [Fact]
    public async Task NameExistsAsync_ignores_case_and_excluded_plan()
    {
        var name = $"Plan {Guid.NewGuid():N}";
        var plan = MembershipPlan.Create(name, Money.Of(100m), 30, null);
        await SaveAsync<IMembershipPlanRepository>(plans => plans.Add(plan));

        Assert.True(await ReadAsync<IMembershipPlanRepository, bool>(plans => plans.NameExistsAsync($" {name.ToUpperInvariant()} ", null, Ct)));
        Assert.False(await ReadAsync<IMembershipPlanRepository, bool>(plans => plans.NameExistsAsync(name, plan.Id, Ct)));
    }

    [Fact]
    public async Task Saving_a_stale_copy_raises_conflict()
    {
        var plan = MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(100m), 30, null);
        await SaveAsync<IMembershipPlanRepository>(plans => plans.Add(plan));

        await using var stale = Factory.Services.CreateAsyncScope();
        var staleCopy = await stale.ServiceProvider.GetRequiredService<IMembershipPlanRepository>().GetByIdAsync(plan.Id, Ct);
        await ChangeAsync<IMembershipPlanRepository>(async plans => (await plans.GetByIdAsync(plan.Id, Ct))!.Deactivate());

        staleCopy!.Update(staleCopy.Name, Money.Of(150m), 30, null);

        await Assert.ThrowsAsync<ConflictException>(() => stale.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
        var current = await ReadAsync<IMembershipPlanRepository, MembershipPlan?>(plans => plans.GetByIdAsync(plan.Id, Ct));
        Assert.Equal(Money.Of(100m), current!.Price);
    }
}
```

- [ ] **Step 3: Write the failing tests**

Architecture rules for the layers, the domain model and the boundaries:

`api/tests/FitnessClub.ArchitectureTests/LayerDependencyTests.cs`:

```csharp
using NetArchTest.Rules;

namespace FitnessClub.ArchitectureTests;

public class LayerDependencyTests
{
    private static readonly string[] QueryableAssemblies = ["System.Linq.Queryable", "System.Linq.Expressions"];

    [Fact]
    public void Domain_depends_only_on_the_base_class_library()
    {
        var offenders = Layers.Domain.ReferencedAssemblyNames()
            .Where(name => !IsBaseClassLibrary(name) || QueryableAssemblies.Contains(name));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Application_depends_only_on_domain_and_dependency_injection_abstractions()
    {
        string[] allowed = ["FitnessClub.Domain", "Microsoft.Extensions.DependencyInjection.Abstractions"];

        var offenders = Layers.Application.ReferencedAssemblyNames()
            .Where(name => (!IsBaseClassLibrary(name) && !allowed.Contains(name)) || QueryableAssemblies.Contains(name));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Api_does_not_reference_persistence_or_job_frameworks()
    {
        var offenders = Layers.Api.ReferencedAssemblyNames()
            .Where(name => name.StartsWith("Microsoft.EntityFrameworkCore") || name.StartsWith("Hangfire"));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Only_the_composition_root_uses_infrastructure()
    {
        var result = Types.InAssembly(Layers.Api)
            .That().DoNotHaveName("Program")
            .ShouldNot().HaveDependencyOn("FitnessClub.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, result.Describe());
    }

    [Fact]
    public void Api_uses_domain_only_to_translate_domain_errors()
    {
        var result = Types.InAssembly(Layers.Api)
            .That().DoNotHaveName("ExceptionToProblemDetailsHandler")
            .ShouldNot().HaveDependencyOn("FitnessClub.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful, result.Describe());
    }

    [Fact]
    public void Application_types_do_not_depend_on_outer_layers_or_frameworks()
    {
        var result = Types.InAssembly(Layers.Application)
            .ShouldNot().HaveDependencyOnAny("FitnessClub.Infrastructure", "FitnessClub.Api", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, result.Describe());
    }

    private static bool IsBaseClassLibrary(string name) =>
        name is "netstandard" or "mscorlib" or "System" || name.StartsWith("System.");
}
```

`api/tests/FitnessClub.ArchitectureTests/DomainModelTests.cs`:

```csharp
using System.Reflection;
using FitnessClub.Domain.Common;

namespace FitnessClub.ArchitectureTests;

public class DomainModelTests
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.Instance;

    private static IEnumerable<Type> DomainModelTypes() =>
        Layers.Domain.DeclaredTypes().Where(type =>
            !type.IsAbstract && !type.IsEnum && !type.IsInterface
            && (typeof(Entity).IsAssignableFrom(type) || type.Namespace == "FitnessClub.Domain.SharedKernel" || IsRecord(type)));

    [Fact]
    public void The_domain_model_is_found()
    {
        Assert.NotEmpty(DomainModelTypes());
    }

    [Fact]
    public void Entities_and_value_objects_have_no_public_setters_or_fields()
    {
        var offenders = DomainModelTypes()
            .SelectMany(type => type.GetProperties(Members).Where(property => property.SetMethod is { IsPublic: true }).Cast<MemberInfo>()
                .Concat(type.GetFields(Members)))
            .Select(member => $"{member.DeclaringType!.Name}.{member.Name}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void Entities_and_value_objects_have_no_public_constructors()
    {
        var offenders = DomainModelTypes()
            .Where(type => type.GetConstructors(Members).Length > 0)
            .Select(type => type.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Entities_and_value_objects_are_sealed()
    {
        var offenders = DomainModelTypes().Where(type => !type.IsSealed).Select(type => type.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_aggregate_root_has_a_repository_interface_in_domain()
    {
        var offenders = AggregateRoots().Where(root => RepositoryInterfaceFor(root) is null).Select(root => root.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_repository_interface_is_implemented_by_a_non_public_infrastructure_class()
    {
        var implementations = Layers.Infrastructure.ConcreteClasses().ToList();

        var offenders = AggregateRoots()
            .Select(RepositoryInterfaceFor)
            .OfType<Type>()
            .Where(contract => !implementations.Any(type => contract.IsAssignableFrom(type) && !type.IsPublic))
            .Select(contract => contract.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Repositories_exist_only_for_aggregate_roots()
    {
        var offenders = Layers.Domain.DeclaredTypes()
            .Where(type => type.IsInterface)
            .SelectMany(type => type.GetInterfaces().Append(type))
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRepository<>))
            .Select(type => type.GetGenericArguments()[0])
            .Where(argument => !argument.IsGenericParameter && !typeof(AggregateRoot).IsAssignableFrom(argument))
            .Select(argument => argument.Name);

        Assert.Empty(offenders);
    }

    private static IEnumerable<Type> AggregateRoots() =>
        Layers.Domain.ConcreteClasses().Where(type => typeof(AggregateRoot).IsAssignableFrom(type));

    private static Type? RepositoryInterfaceFor(Type root) =>
        Layers.Domain.DeclaredTypes().FirstOrDefault(type =>
            type.IsInterface && typeof(IRepository<>).MakeGenericType(root).IsAssignableFrom(type) && type.Name == $"I{root.Name}Repository");

    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$") is not null;
}
```

`api/tests/FitnessClub.ArchitectureTests/BoundaryTests.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.ArchitectureTests;

public class BoundaryTests
{
    [Fact]
    public void DbContext_exposes_only_aggregate_roots()
    {
        var offenders = typeof(FitnessClubDbContext).GetProperties()
            .Where(property => property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(property => property.PropertyType.GetGenericArguments()[0])
            .Where(entity => !typeof(AggregateRoot).IsAssignableFrom(entity))
            .Select(entity => entity.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Application_services_are_internal_and_exposed_through_interfaces()
    {
        var services = Layers.Application.ConcreteClasses().Where(type => type.Name.EndsWith("Service")).ToList();

        var offenders = services
            .Where(type => type.IsPublic || type.GetInterface($"I{type.Name}") is not { IsPublic: true })
            .Select(type => type.Name);

        Assert.NotEmpty(services);
        Assert.Empty(offenders);
    }

    [Fact]
    public void Controllers_depend_only_on_application_service_interfaces()
    {
        var controllers = Layers.Api.ConcreteClasses().Where(type => typeof(ControllerBase).IsAssignableFrom(type)).ToList();

        var injected = controllers
            .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters()))
            .Concat(controllers
                .SelectMany(type => type.GetMethods())
                .SelectMany(method => method.GetParameters())
                .Where(parameter => parameter.IsDefined(typeof(FromServicesAttribute), inherit: false)));

        var offenders = injected
            .Where(parameter => !parameter.ParameterType.IsInterface
                || parameter.ParameterType.Assembly != Layers.Application
                || !parameter.ParameterType.Name.EndsWith("Service"))
            .Select(parameter => $"{parameter.Member.DeclaringType!.Name}({parameter.ParameterType.Name})");

        Assert.NotEmpty(controllers);
        Assert.Empty(offenders);
    }

    [Fact]
    public void Infrastructure_exposes_only_its_registration_entry_point()
    {
        var offenders = Layers.Infrastructure.DeclaredTypes()
            .Where(type => type.IsPublic && type != typeof(FitnessClub.Infrastructure.DependencyInjection))
            .Select(type => type.Name);

        Assert.Empty(offenders);
    }
}
```

- [ ] **Step 4: Run and confirm it fails**

Run from `api/`:

```sh
dotnet build
```

Expected: FAIL: build errors such as `CS0246: The type or namespace name 'IUnitOfWork' could not be found` and `'IMembershipPlanService' could not be found`.

- [ ] **Step 5: Implement**

Application: drop EF Core, depend on repository and unit-of-work abstractions. The service checks name uniqueness before it touches the aggregate:

In `api/Directory.Packages.props`, replace:

````xml
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" />
````

with:

````xml
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.12" />
````

`api/src/FitnessClub.Application/FitnessClub.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\FitnessClub.Domain\FitnessClub.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="FitnessClub.UnitTests" />
  </ItemGroup>
</Project>
```

Delete `api/src/FitnessClub.Application/Abstractions/IApplicationDbContext.cs`.

`api/src/FitnessClub.Application/Abstractions/IUnitOfWork.cs`:

```csharp
namespace FitnessClub.Application.Abstractions;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

`api/src/FitnessClub.Application/MembershipPlans/IMembershipPlanService.cs`:

```csharp
namespace FitnessClub.Application.MembershipPlans;

public interface IMembershipPlanService
{
    Task<IReadOnlyList<MembershipPlanResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<MembershipPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<MembershipPlanResponse> CreateAsync(MembershipPlanRequest request, CancellationToken cancellationToken);

    Task<MembershipPlanResponse> UpdateAsync(Guid id, MembershipPlanRequest request, CancellationToken cancellationToken);

    Task ActivateAsync(Guid id, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
```

`api/src/FitnessClub.Application/MembershipPlans/MembershipPlanService.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Application.MembershipPlans;

internal sealed class MembershipPlanService(IMembershipPlanRepository plans, IUnitOfWork unitOfWork) : IMembershipPlanService
{
    public async Task<IReadOnlyList<MembershipPlanResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var list = await plans.ListAsync(includeInactive, cancellationToken);
        return list.Select(MembershipPlanResponse.FromEntity).ToList();
    }

    public async Task<MembershipPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        MembershipPlanResponse.FromEntity(await FindAsync(id, cancellationToken));

    public async Task<MembershipPlanResponse> CreateAsync(MembershipPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = MembershipPlan.Create(request.Name, Money.Of(request.Price), request.ValidityDays, request.VisitLimit);
        await EnsureNameIsUniqueAsync(request.Name, excludeId: null, cancellationToken);

        plans.Add(plan);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MembershipPlanResponse.FromEntity(plan);
    }

    public async Task<MembershipPlanResponse> UpdateAsync(Guid id, MembershipPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        await EnsureNameIsUniqueAsync(request.Name, plan.Id, cancellationToken);
        plan.Update(request.Name, Money.Of(request.Price), request.ValidityDays, request.VisitLimit);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MembershipPlanResponse.FromEntity(plan);
    }

    public async Task ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        plan.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var plan = await FindAsync(id, cancellationToken);
        plan.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<MembershipPlan> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await plans.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Membership plan '{id}' was not found.");

    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await plans.NameExistsAsync(name, excludeId, cancellationToken))
            throw new ConflictException($"A membership plan named '{name.Trim()}' already exists.");
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
        services.AddScoped<IMembershipPlanService, MembershipPlanService>();
        return services;
    }
}
```

- [ ] **Step 6: Implement**

Infrastructure: internal DbContext with optimistic concurrency per aggregate, unit of work, generic and plan repository, and a single public entry point. Later tasks add one `DbSet` and one registration per aggregate:

`api/src/FitnessClub.Infrastructure/FitnessClub.Infrastructure.csproj`:

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
  <ItemGroup>
    <InternalsVisibleTo Include="FitnessClub.ArchitectureTests" />
  </ItemGroup>
</Project>
```

`api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FitnessClub.Infrastructure.Persistence;

internal sealed class FitnessClubDbContext(DbContextOptions<FitnessClubDbContext> options) : DbContext(options)
{
    public const string VersionProperty = "Version";

    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FitnessClubDbContext).Assembly);

        var aggregateRoots = modelBuilder.Model.GetEntityTypes()
            .Where(type => !type.IsOwned() && typeof(AggregateRoot).IsAssignableFrom(type.ClrType))
            .Select(type => type.ClrType)
            .ToList();

        foreach (var root in aggregateRoots)
            modelBuilder.Entity(root).Property<Guid>(VersionProperty).IsConcurrencyToken();
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampChangedAggregates();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampChangedAggregates()
    {
        ChangeTracker.DetectChanges();

        var changedRoots = ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(AggregateRootOf)
            .OfType<EntityEntry>()
            .Where(root => root.State != EntityState.Deleted)
            .DistinctBy(root => root.Entity)
            .ToList();

        foreach (var root in changedRoots)
            root.Property(VersionProperty).CurrentValue = Guid.NewGuid();
    }

    private EntityEntry? AggregateRootOf(EntityEntry entry)
    {
        var current = entry;
        while (current.Metadata.IsOwned())
        {
            var ownership = current.Metadata.FindOwnership()!;
            var ownerKey = ownership.Properties.Select(property => current.Property(property.Name).CurrentValue).ToList();
            current = ChangeTracker.Entries().FirstOrDefault(candidate =>
                candidate.Metadata == ownership.PrincipalEntityType
                && ownership.PrincipalKey.Properties.Select(property => candidate.Property(property.Name).CurrentValue).SequenceEqual(ownerKey));

            if (current is null)
                return null;
        }

        return current;
    }
}
```

`api/src/FitnessClub.Infrastructure/Persistence/UnitOfWork.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence;

internal sealed class UnitOfWork(FitnessClubDbContext db) : IUnitOfWork
{
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
    }
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Repositories/Repository.cs`:

```csharp
using FitnessClub.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal abstract class Repository<TAggregate>(FitnessClubDbContext db) : IRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    protected DbSet<TAggregate> Set { get; } = db.Set<TAggregate>();

    public Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Set.FirstOrDefaultAsync(aggregate => aggregate.Id == id, cancellationToken);

    public void Add(TAggregate aggregate) => Set.Add(aggregate);
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Repositories/MembershipPlanRepository.cs`:

```csharp
using FitnessClub.Domain.MembershipPlans;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class MembershipPlanRepository(FitnessClubDbContext db)
    : Repository<MembershipPlan>(db), IMembershipPlanRepository
{
    public async Task<IReadOnlyList<MembershipPlan>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await Set.Where(p => includeInactive || p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToLower();
        return Set.AnyAsync(p => p.Id != excludeId && p.Name.ToLower() == normalizedName, cancellationToken);
    }
}
```

`api/src/FitnessClub.Infrastructure/BackgroundJobs/RecurringJobs.cs`:

```csharp
using Hangfire;

namespace FitnessClub.Infrastructure.BackgroundJobs;

internal static class RecurringJobs
{
    public static void Register(IRecurringJobManager recurringJobs)
    {
    }
}
```

`api/src/FitnessClub.Infrastructure/DependencyInjection.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Infrastructure.BackgroundJobs;
using FitnessClub.Infrastructure.Persistence;
using FitnessClub.Infrastructure.Persistence.Repositories;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitnessClub.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "FitnessClub";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FitnessClubDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                options.UseInMemoryDatabase(configuration["Database:InMemoryName"] ?? "FitnessClub");
            else
                options.UseSqlServer(connectionString, sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMembershipPlanRepository, MembershipPlanRepository>();

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

        return services;
    }

    public static async Task UseInfrastructureAsync(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.MapHangfireDashboard("/hangfire").AllowAnonymous();

        await InitializeDatabaseAsync(app.Services);
        RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>());
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FitnessClubDbContext>();
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
    }
}
```

- [ ] **Step 7: Implement**

Api: the controller depends on the interface, and `Program` no longer touches Hangfire or the database:

`api/src/FitnessClub.Api/Program.cs`:

```csharp
using FitnessClub.Api.Auth;
using FitnessClub.Api.ErrorHandling;
using FitnessClub.Application;
using FitnessClub.Infrastructure;
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
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

await app.UseInfrastructureAsync();

await app.RunAsync();

public partial class Program;
```

In `api/src/FitnessClub.Api/Controllers/MembershipPlansController.cs`, replace:

````csharp
(MembershipPlanService service)
````

with:

````csharp
(IMembershipPlanService service)
````

- [ ] **Step 8: Run and confirm it passes**

Run from `api/`:

```sh
dotnet test
```

Expected: PASS: 95 tests (architecture rules included).

- [ ] **Step 9: Commit**

From the repo root:

```sh
git add api/Directory.Packages.props api/src api/tests
git commit -m "refactor(api): repositories, unit of work and service interfaces for membership plans"
```


### Task 4: Room aggregate

**Files:**
- Create: `api/src/FitnessClub.Domain/Rooms/Room.cs`, `IRoomRepository.cs`
- Create: `api/src/FitnessClub.Infrastructure/Persistence/Configurations/RoomConfiguration.cs`, `Repositories/RoomRepository.cs`; Modify: `FitnessClubDbContext.cs`, `DependencyInjection.cs`
- Test: `api/tests/FitnessClub.UnitTests/Domain/RoomTests.cs`, `api/tests/FitnessClub.IntegrationTests/Persistence/RoomRepositoryTests.cs`

**Interfaces:**
- Consumes: Task 3 `Repository<T>`, `PersistenceTestBase`.
- Produces: `Room : AggregateRoot` with `NameMaxLength = 100`, `MaxCapacity = 500`, `Name`, `Capacity`, `IsActive`, `static Room Create(string name, int capacity)`, `Update(string name, int capacity)`, `Activate()`, `Deactivate()`; `IRoomRepository : IRepository<Room>` with `ListAsync(bool includeInactive, ct)` and `NameExistsAsync(string name, Guid? excludeId, ct)`.

- [ ] **Step 1: Write the failing tests**

`api/tests/FitnessClub.UnitTests/Domain/RoomTests.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Rooms;

namespace FitnessClub.UnitTests.Domain;

public class RoomTests
{
    [Fact]
    public void Create_trims_name_and_starts_active()
    {
        var room = Room.Create("  Yoga hall ", 25);

        Assert.Equal("Yoga hall", room.Name);
        Assert.Equal(25, room.Capacity);
        Assert.True(room.IsActive);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("Hall", 0)]
    [InlineData("Hall", Room.MaxCapacity + 1)]
    public void Create_with_invalid_values_throws(string name, int capacity)
    {
        Assert.Throws<DomainException>(() => Room.Create(name, capacity));
    }

    [Fact]
    public void Deactivate_then_activate_toggles_IsActive()
    {
        var room = Room.Create("Hall", 10);

        room.Deactivate();
        Assert.False(room.IsActive);

        room.Activate();
        Assert.True(room.IsActive);
    }
}
```

`api/tests/FitnessClub.IntegrationTests/Persistence/RoomRepositoryTests.cs`:

```csharp
using FitnessClub.Domain.Rooms;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Persistence;

public class RoomRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    [Fact]
    public async Task Room_round_trips_and_inactive_rooms_are_listed_only_on_request()
    {
        var active = Room.Create($"Room {Guid.NewGuid():N}", 20);
        var inactive = Room.Create($"Room {Guid.NewGuid():N}", 10);
        inactive.Deactivate();
        await SaveAsync<IRoomRepository>(rooms =>
        {
            rooms.Add(active);
            rooms.Add(inactive);
        });

        var activeOnly = await ReadAsync<IRoomRepository, IReadOnlyList<Room>>(rooms => rooms.ListAsync(false, Ct));
        var all = await ReadAsync<IRoomRepository, IReadOnlyList<Room>>(rooms => rooms.ListAsync(true, Ct));

        Assert.Contains(activeOnly, r => r.Id == active.Id && r.Capacity == 20);
        Assert.DoesNotContain(activeOnly, r => r.Id == inactive.Id);
        Assert.Contains(all, r => r.Id == inactive.Id);
        Assert.True(await ReadAsync<IRoomRepository, bool>(rooms => rooms.NameExistsAsync(active.Name.ToLowerInvariant(), null, Ct)));
    }
}
```

- [ ] **Step 2: Run and confirm it fails**

Run from `api/`:

```sh
dotnet test --project tests/FitnessClub.UnitTests --filter-class "FitnessClub.UnitTests.Domain.RoomTests"
```

Expected: FAIL: build error `CS0234: The type or namespace name 'Rooms' does not exist in the namespace 'FitnessClub.Domain'`.

- [ ] **Step 3: Implement**

`api/src/FitnessClub.Domain/Rooms/Room.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Rooms;

public sealed class Room : AggregateRoot
{
    public const int NameMaxLength = 100;
    public const int MaxCapacity = 500;

    public string Name { get; private set; } = null!;
    public int Capacity { get; private set; }
    public bool IsActive { get; private set; }

    private Room()
    {
    }

    public static Room Create(string name, int capacity)
    {
        var room = new Room { IsActive = true };
        room.Update(name, capacity);
        return room;
    }

    public void Update(string name, int capacity)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Room name is required.");

        var trimmedName = name.Trim();
        if (trimmedName.Length > NameMaxLength)
            throw new DomainException($"Room name must be at most {NameMaxLength} characters.");

        if (capacity is < 1 or > MaxCapacity)
            throw new DomainException($"Room capacity must be between 1 and {MaxCapacity}.");

        Name = trimmedName;
        Capacity = capacity;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
```

`api/src/FitnessClub.Domain/Rooms/IRoomRepository.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Rooms;

public interface IRoomRepository : IRepository<Room>
{
    Task<IReadOnlyList<Room>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken);
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Configurations/RoomConfiguration.cs`:

```csharp
using FitnessClub.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(Room.NameMaxLength).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();
    }
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Repositories/RoomRepository.cs`:

```csharp
using FitnessClub.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class RoomRepository(FitnessClubDbContext db) : Repository<Room>(db), IRoomRepository
{
    public async Task<IReadOnlyList<Room>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await Set.Where(r => includeInactive || r.IsActive).OrderBy(r => r.Name).ToListAsync(cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToLower();
        return Set.AnyAsync(r => r.Id != excludeId && r.Name.ToLower() == normalizedName, cancellationToken);
    }
}
```

- [ ] **Step 4: Implement**

Expose the aggregate and register its repository:

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
using FitnessClub.Domain.MembershipPlans;
````

with:

````csharp
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Rooms;
````

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
````

with:

````csharp
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Room> Rooms => Set<Room>();
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
using FitnessClub.Domain.MembershipPlans;
````

with:

````csharp
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Rooms;
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
        services.AddScoped<IMembershipPlanRepository, MembershipPlanRepository>();
````

with:

````csharp
        services.AddScoped<IMembershipPlanRepository, MembershipPlanRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
````

- [ ] **Step 5: Run and confirm it passes**

Run from `api/`:

```sh
dotnet test
```

Expected: PASS: 101 tests.

- [ ] **Step 6: Commit**

From the repo root:

```sh
git add api/src api/tests
git commit -m "feat(api): room aggregate with repository"
```


### Task 5: Client aggregate with memberships, plus Visit and Payment

**Files:**
- Create: `api/src/FitnessClub.Domain/Clients/Client.cs`, `Membership.cs`, `IClientRepository.cs`
- Create: `api/src/FitnessClub.Domain/Visits/Visit.cs`, `IVisitRepository.cs`
- Create: `api/src/FitnessClub.Domain/Payments/Payment.cs`, `PaymentMethod.cs`, `IPaymentRepository.cs`
- Create: `api/src/FitnessClub.Infrastructure/Persistence/Configurations/ClientConfiguration.cs`, `VisitConfiguration.cs`, `PaymentConfiguration.cs`, `Repositories/ClientRepository.cs`, `VisitRepository.cs`, `PaymentRepository.cs`; Modify: `FitnessClubDbContext.cs`, `DependencyInjection.cs`
- Test: `api/tests/FitnessClub.UnitTests/Domain/TestData.cs` (grows), `ClientTests.cs`, `api/tests/FitnessClub.IntegrationTests/Persistence/ClientRepositoryTests.cs`

**Interfaces:**
- Consumes: Task 2 value objects and `MembershipPlan`; Task 3 repository base and `PersistenceTestBase`.
- Produces `Client : AggregateRoot`: `MaxAge = 120`; `Name`, `DateOfBirth`, `Phone`, `Email?`, `RegisteredAt`, `IReadOnlyCollection<Membership> Memberships`; `static Register(PersonName, DateOnly dateOfBirth, PhoneNumber, EmailAddress?, DateTimeOffset now)`; `UpdateProfile(same, now)`; `int AgeOn(DateOnly)`; `Membership? ActiveMembershipOn(DateOnly)`; `bool HasActiveMembershipOn(DateOnly)`; `Payment PurchaseMembership(MembershipPlan, DateOnly startsOn, PaymentMethod, DateTimeOffset now)`; `CancelMembership(Guid membershipId, DateTimeOffset now)`; `Visit CheckIn(DateTimeOffset now)`; `bool Owns(Membership)`; `bool NeedsExpiryNotice(Membership, DateOnly today)`; `IReadOnlyList<Membership> MembershipsNeedingExpiryNotice(DateOnly today, DateOnly endsBy)`.
- Produces `Membership : Entity` (read-only outside the aggregate): `PlanId`, `PlanName`, `Price`, `StartsOn`, `EndsOn`, `VisitLimit`, `VisitsUsed`, `LastVisitOn`, `PurchasedAt`, `CancelledAt`, `IsCancelled`, `RemainingVisits`, `HasVisitsRemaining`, `IsActiveOn(DateOnly)`.
- Produces `Visit : AggregateRoot` (`ClientId`, `MembershipId`, `CheckedInAt`; created only by `Client.CheckIn`) and `Payment : AggregateRoot` (`ClientId`, `MembershipId`, `Money Amount`, `PaymentMethod Method` = `Cash | Card`, `PaidAt`; created only by `Client.PurchaseMembership`).
- Produces repositories: `IClientRepository` (`ListAsync(ct)`, `PhoneExistsAsync(PhoneNumber, Guid? excludeId, ct)`, `ListWithMembershipsEndingBetweenAsync(DateOnly from, DateOnly to, ct)`), `IVisitRepository` (`ListForClientAsync(Guid clientId, ct)`), `IPaymentRepository` (`ListPaidBetweenAsync(DateTimeOffset from, DateTimeOffset to, ct)`, half-open).
- Produces test helpers in `TestData`: `Plan(validityDays, visitLimit, price)`, `Client(email?)`, `ClientWithMembership(validityDays, visitLimit)`, `Buy(client, plan, startsOn?)` → `Membership`.

- [ ] **Step 1: Write the failing tests**

Write the failing tests. They pin the rules most likely to bite at the front desk: one check-in per day, no overlapping purchases, a payment for every sale, and optimistic concurrency when two receptionists change the same client.

`api/tests/FitnessClub.UnitTests/Domain/TestData.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.UnitTests.Domain;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 5);

    public static MembershipPlan Plan(int validityDays = 30, int? visitLimit = null, decimal price = 800m) =>
        MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(price), validityDays, visitLimit);

    public static Client Client(string? email = null) =>
        FitnessClub.Domain.Clients.Client.Register(
            PersonName.Create("Olena", "Shevchenko", null),
            new DateOnly(1995, 3, 14),
            PhoneNumber.Create("+380671234567"),
            email is null ? null : EmailAddress.Create(email),
            Now);

    public static Client ClientWithMembership(int validityDays = 30, int? visitLimit = null)
    {
        var client = Client();
        Buy(client, Plan(validityDays, visitLimit));
        return client;
    }

    public static Membership Buy(Client client, MembershipPlan plan, DateOnly? startsOn = null)
    {
        var payment = client.PurchaseMembership(plan, startsOn ?? Today, PaymentMethod.Cash, Now);
        return client.Memberships.Single(m => m.Id == payment.MembershipId);
    }
}
```

`api/tests/FitnessClub.UnitTests/Domain/ClientTests.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.UnitTests.Domain;

public class ClientTests
{
    private static readonly PersonName Name = PersonName.Create("Olena", "Shevchenko", null);
    private static readonly PhoneNumber Phone = PhoneNumber.Create("+380671234567");

    [Fact]
    public void Register_sets_profile_and_registration_time()
    {
        var client = Client.Register(Name, new DateOnly(1995, 3, 14), Phone, null, TestData.Now);

        Assert.Equal(Name, client.Name);
        Assert.Equal(Phone, client.Phone);
        Assert.Equal(TestData.Now, client.RegisteredAt);
        Assert.Empty(client.Memberships);
    }

    [Fact]
    public void Register_with_future_date_of_birth_throws()
    {
        Assert.Throws<DomainException>(() => Client.Register(Name, TestData.Today.AddDays(1), Phone, null, TestData.Now));
    }

    [Fact]
    public void Register_older_than_max_age_throws()
    {
        var dateOfBirth = TestData.Today.AddYears(-Client.MaxAge - 1);

        Assert.Throws<DomainException>(() => Client.Register(Name, dateOfBirth, Phone, null, TestData.Now));
    }

    [Theory]
    [InlineData(4, 31)]
    [InlineData(5, 31)]
    [InlineData(6, 30)]
    public void AgeOn_counts_full_years(int birthDay, int expectedAge)
    {
        var client = Client.Register(Name, new DateOnly(1995, 10, birthDay), Phone, null, TestData.Now);

        Assert.Equal(expectedAge, client.AgeOn(TestData.Today));
    }

    [Fact]
    public void PurchaseMembership_snapshots_plan_and_computes_end_date()
    {
        var client = TestData.Client();
        var plan = TestData.Plan(validityDays: 30, visitLimit: 12, price: 950m);

        var membership = TestData.Buy(client, plan);

        Assert.Equal(plan.Id, membership.PlanId);
        Assert.Equal(plan.Name, membership.PlanName);
        Assert.Equal(Money.Of(950m), membership.Price);
        Assert.Equal(TestData.Today, membership.StartsOn);
        Assert.Equal(TestData.Today.AddDays(29), membership.EndsOn);
        Assert.Equal(12, membership.RemainingVisits);
        Assert.Contains(membership, client.Memberships);
    }

    [Fact]
    public void PurchaseMembership_of_inactive_plan_throws()
    {
        var plan = TestData.Plan();
        plan.Deactivate();

        Assert.Throws<DomainException>(() => TestData.Buy(TestData.Client(), plan));
    }

    [Fact]
    public void PurchaseMembership_starting_in_the_past_throws()
    {
        Assert.Throws<DomainException>(() => TestData.Buy(TestData.Client(), TestData.Plan(), TestData.Today.AddDays(-1)));
    }

    [Fact]
    public void PurchaseMembership_overlapping_a_live_membership_throws()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);

        Assert.Throws<DomainException>(() => TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(29)));
    }

    [Fact]
    public void PurchaseMembership_right_after_the_current_one_ends_is_allowed()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);

        var next = TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(30));

        Assert.Equal(2, client.Memberships.Count);
        Assert.Equal(TestData.Today.AddDays(30), next.StartsOn);
    }

    [Fact]
    public void PurchaseMembership_after_single_visit_is_used_up_is_allowed_the_same_day()
    {
        var client = TestData.ClientWithMembership(validityDays: 1, visitLimit: 1);
        client.CheckIn(TestData.Now);

        TestData.Buy(client, TestData.Plan(validityDays: 1, visitLimit: 1));

        Assert.True(client.HasActiveMembershipOn(TestData.Today));
    }

    [Fact]
    public void PurchaseMembership_returns_payment_for_the_new_membership()
    {
        var client = TestData.Client();

        var payment = client.PurchaseMembership(TestData.Plan(price: 1200m), TestData.Today, PaymentMethod.Card, TestData.Now);

        var membership = Assert.Single(client.Memberships);
        Assert.Equal(client.Id, payment.ClientId);
        Assert.Equal(membership.Id, payment.MembershipId);
        Assert.Equal(Money.Of(1200m), payment.Amount);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Equal(TestData.Now, payment.PaidAt);
    }

    [Fact]
    public void PurchaseMembership_with_unknown_payment_method_throws_and_adds_nothing()
    {
        var client = TestData.Client();

        Assert.Throws<DomainException>(() => client.PurchaseMembership(TestData.Plan(), TestData.Today, (PaymentMethod)42, TestData.Now));
        Assert.Empty(client.Memberships);
    }

    [Fact]
    public void CheckIn_records_visit_and_uses_one_visit()
    {
        var client = TestData.ClientWithMembership(visitLimit: 10);
        var membership = client.Memberships.Single();

        var visit = client.CheckIn(TestData.Now);

        Assert.Equal(client.Id, visit.ClientId);
        Assert.Equal(membership.Id, visit.MembershipId);
        Assert.Equal(TestData.Now, visit.CheckedInAt);
        Assert.Equal(9, membership.RemainingVisits);
    }

    [Fact]
    public void CheckIn_without_membership_throws()
    {
        Assert.Throws<DomainException>(() => TestData.Client().CheckIn(TestData.Now));
    }

    [Fact]
    public void CheckIn_after_membership_expired_throws()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);

        Assert.Throws<DomainException>(() => client.CheckIn(TestData.Now.AddDays(30)));
    }

    [Fact]
    public void CheckIn_when_visits_are_used_up_throws()
    {
        var client = TestData.ClientWithMembership(visitLimit: 1);
        client.CheckIn(TestData.Now);

        Assert.Throws<DomainException>(() => client.CheckIn(TestData.Now.AddDays(1)));
    }

    [Fact]
    public void CheckIn_twice_on_the_same_day_throws_and_uses_one_visit()
    {
        var client = TestData.ClientWithMembership(visitLimit: 10);
        client.CheckIn(TestData.Now);

        Assert.Throws<DomainException>(() => client.CheckIn(TestData.Now.AddHours(3)));
        Assert.Equal(9, client.Memberships.Single().RemainingVisits);
    }

    [Fact]
    public void CheckIn_on_the_next_day_uses_another_visit()
    {
        var client = TestData.ClientWithMembership(visitLimit: 10);
        client.CheckIn(TestData.Now);

        client.CheckIn(TestData.Now.AddDays(1));

        Assert.Equal(8, client.Memberships.Single().RemainingVisits);
    }

    [Fact]
    public void NeedsExpiryNotice_is_false_once_the_client_has_renewed()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);
        var current = client.Memberships.Single();
        Assert.True(client.NeedsExpiryNotice(current, TestData.Today));

        TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(30));

        Assert.False(client.NeedsExpiryNotice(current, TestData.Today));
    }

    [Fact]
    public void NeedsExpiryNotice_is_false_for_ended_or_used_up_memberships()
    {
        var ended = TestData.ClientWithMembership(validityDays: 30);
        var usedUp = TestData.ClientWithMembership(visitLimit: 1);
        usedUp.CheckIn(TestData.Now);

        Assert.False(ended.NeedsExpiryNotice(ended.Memberships.Single(), TestData.Today.AddDays(30)));
        Assert.False(usedUp.NeedsExpiryNotice(usedUp.Memberships.Single(), TestData.Today));
    }

    [Fact]
    public void MembershipsNeedingExpiryNotice_returns_only_those_ending_by_the_date()
    {
        var client = TestData.ClientWithMembership(validityDays: 3);

        Assert.Single(client.MembershipsNeedingExpiryNotice(TestData.Today, TestData.Today.AddDays(3)));
        Assert.Empty(client.MembershipsNeedingExpiryNotice(TestData.Today, TestData.Today.AddDays(1)));
    }

    [Fact]
    public void CancelMembership_makes_it_inactive()
    {
        var client = TestData.ClientWithMembership();
        var membership = client.Memberships.Single();

        client.CancelMembership(membership.Id, TestData.Now);

        Assert.True(membership.IsCancelled);
        Assert.False(client.HasActiveMembershipOn(TestData.Today));
    }

    [Fact]
    public void CancelMembership_twice_throws()
    {
        var client = TestData.ClientWithMembership();
        var membershipId = client.Memberships.Single().Id;
        client.CancelMembership(membershipId, TestData.Now);

        Assert.Throws<DomainException>(() => client.CancelMembership(membershipId, TestData.Now));
    }

    [Fact]
    public void CancelMembership_of_another_client_throws()
    {
        Assert.Throws<DomainException>(() => TestData.ClientWithMembership().CancelMembership(Guid.NewGuid(), TestData.Now));
    }
}
```

`api/tests/FitnessClub.IntegrationTests/Persistence/ClientRepositoryTests.cs`:

```csharp
using FitnessClub.Application.Abstractions;
using FitnessClub.Application.Common;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Visits;
using FitnessClub.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FitnessClub.IntegrationTests.Persistence;

public class ClientRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    private static MembershipPlan Plan(int validityDays = 30, int? visitLimit = null) =>
        MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(800m), validityDays, visitLimit);

    private static Client NewClient(string? email = "olena@example.com") =>
        Client.Register(
            PersonName.Create("Olena", "Shevchenko", "Petrivna"),
            new DateOnly(1995, 3, 14),
            PhoneNumber.Create(UniquePhone()),
            email is null ? null : EmailAddress.Create(email),
            Now);

    [Fact]
    public async Task Client_round_trips_with_value_objects_and_memberships()
    {
        var client = NewClient();
        var payment = client.PurchaseMembership(Plan(visitLimit: 8), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        var loaded = await ReadAsync<IClientRepository, Client?>(clients => clients.GetByIdAsync(client.Id, Ct));

        Assert.NotNull(loaded);
        Assert.Equal(client.Name, loaded.Name);
        Assert.Equal(client.Phone, loaded.Phone);
        Assert.Equal(client.Email, loaded.Email);
        var loadedMembership = Assert.Single(loaded.Memberships);
        Assert.Equal(payment.MembershipId, loadedMembership.Id);
        Assert.Equal(Today.AddDays(29), loadedMembership.EndsOn);
        Assert.Equal(8, loadedMembership.RemainingVisits);
    }

    [Fact]
    public async Task Membership_bought_on_loaded_client_is_inserted()
    {
        var client = NewClient(email: null);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        await ChangeAsync<IClientRepository>(async clients =>
        {
            var loaded = await clients.GetByIdAsync(client.Id, Ct);
            loaded!.PurchaseMembership(Plan(), Today, PaymentMethod.Card, Now);
        });

        var reloaded = await ReadAsync<IClientRepository, Client?>(clients => clients.GetByIdAsync(client.Id, Ct));
        Assert.Single(reloaded!.Memberships);
        Assert.Null(reloaded.Email);
    }

    [Fact]
    public async Task Check_in_saves_visit_and_used_visit_in_one_unit_of_work()
    {
        var client = NewClient();
        client.PurchaseMembership(Plan(visitLimit: 5), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        await InUnitOfWorkAsync(async services =>
        {
            var loaded = await services.GetRequiredService<IClientRepository>().GetByIdAsync(client.Id, Ct);
            services.GetRequiredService<IVisitRepository>().Add(loaded!.CheckIn(Now));
        });

        var reloaded = await ReadAsync<IClientRepository, Client?>(clients => clients.GetByIdAsync(client.Id, Ct));
        var history = await ReadAsync<IVisitRepository, IReadOnlyList<Visit>>(visits => visits.ListForClientAsync(client.Id, Ct));
        Assert.Equal(4, reloaded!.Memberships.Single().RemainingVisits);
        Assert.Equal(Now, Assert.Single(history).CheckedInAt);
    }

    [Fact]
    public async Task PhoneExistsAsync_matches_normalized_phone()
    {
        var client = NewClient();
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        Assert.True(await ReadAsync<IClientRepository, bool>(clients => clients.PhoneExistsAsync(client.Phone, null, Ct)));
        Assert.False(await ReadAsync<IClientRepository, bool>(clients => clients.PhoneExistsAsync(client.Phone, client.Id, Ct)));
    }

    [Fact]
    public async Task ListWithMembershipsEndingBetweenAsync_skips_cancelled_and_out_of_range()
    {
        var expiring = NewClient();
        expiring.PurchaseMembership(Plan(validityDays: 3), Today, PaymentMethod.Cash, Now);
        var cancelled = NewClient();
        var cancelledPayment = cancelled.PurchaseMembership(Plan(validityDays: 3), Today, PaymentMethod.Cash, Now);
        cancelled.CancelMembership(cancelledPayment.MembershipId, Now);
        var later = NewClient();
        later.PurchaseMembership(Plan(validityDays: 60), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients =>
        {
            clients.Add(expiring);
            clients.Add(cancelled);
            clients.Add(later);
        });

        var found = await ReadAsync<IClientRepository, IReadOnlyList<Client>>(
            clients => clients.ListWithMembershipsEndingBetweenAsync(Today, Today.AddDays(3), Ct));

        Assert.Contains(found, c => c.Id == expiring.Id);
        Assert.DoesNotContain(found, c => c.Id == cancelled.Id);
        Assert.DoesNotContain(found, c => c.Id == later.Id);
    }

    [Fact]
    public async Task Payment_round_trips_and_is_listed_by_paid_date()
    {
        var client = NewClient();
        var payment = client.PurchaseMembership(Plan(), Today, PaymentMethod.Card, Now);
        await InUnitOfWorkAsync(services =>
        {
            services.GetRequiredService<IClientRepository>().Add(client);
            services.GetRequiredService<IPaymentRepository>().Add(payment);
            return Task.CompletedTask;
        });

        var paid = await ReadAsync<IPaymentRepository, IReadOnlyList<Payment>>(
            payments => payments.ListPaidBetweenAsync(Now.AddMinutes(-1), Now.AddMinutes(1), Ct));

        var loaded = Assert.Single(paid, p => p.Id == payment.Id);
        Assert.Equal(Money.Of(800m), loaded.Amount);
        Assert.Equal(PaymentMethod.Card, loaded.Method);
    }

    [Fact]
    public async Task Concurrent_changes_to_the_same_client_raise_conflict()
    {
        var client = NewClient();
        client.PurchaseMembership(Plan(visitLimit: 1), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        await using var first = Factory.Services.CreateAsyncScope();
        await using var second = Factory.Services.CreateAsyncScope();
        var firstCopy = await first.ServiceProvider.GetRequiredService<IClientRepository>().GetByIdAsync(client.Id, Ct);
        var secondCopy = await second.ServiceProvider.GetRequiredService<IClientRepository>().GetByIdAsync(client.Id, Ct);
        first.ServiceProvider.GetRequiredService<IVisitRepository>().Add(firstCopy!.CheckIn(Now));
        second.ServiceProvider.GetRequiredService<IVisitRepository>().Add(secondCopy!.CheckIn(Now));

        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        await Assert.ThrowsAsync<ConflictException>(() => second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
        var reloaded = await ReadAsync<IClientRepository, Client?>(clients => clients.GetByIdAsync(client.Id, Ct));
        Assert.Equal(0, reloaded!.Memberships.Single().RemainingVisits);
    }

    [Fact]
    public async Task Changing_only_a_child_entity_still_bumps_the_aggregate_version()
    {
        var client = NewClient();
        client.PurchaseMembership(Plan(), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        await using var stale = Factory.Services.CreateAsyncScope();
        var staleCopy = await stale.ServiceProvider.GetRequiredService<IClientRepository>().GetByIdAsync(client.Id, Ct);
        await ChangeAsync<IClientRepository>(async clients =>
            (await clients.GetByIdAsync(client.Id, Ct))!.CancelMembership(client.Memberships.Single().Id, Now));

        staleCopy!.UpdateProfile(staleCopy.Name, staleCopy.DateOfBirth, PhoneNumber.Create(UniquePhone()), null, Now);

        await Assert.ThrowsAsync<ConflictException>(() => stale.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
    }
}
```

- [ ] **Step 2: Run and confirm it fails**

Run from `api/`:

```sh
dotnet test --project tests/FitnessClub.UnitTests --filter-class "FitnessClub.UnitTests.Domain.ClientTests"
```

Expected: FAIL: build error `CS0234: The type or namespace name 'Clients' does not exist in the namespace 'FitnessClub.Domain'`.

- [ ] **Step 3: Implement**

`api/src/FitnessClub.Domain/Clients/Membership.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Clients;

public sealed class Membership : Entity
{
    public Guid PlanId { get; private set; }
    public string PlanName { get; private set; } = null!;
    public Money Price { get; private set; } = null!;
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public int? VisitLimit { get; private set; }
    public int VisitsUsed { get; private set; }
    public DateOnly? LastVisitOn { get; private set; }
    public DateTimeOffset PurchasedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    private Membership()
    {
    }

    internal static Membership Create(MembershipPlan plan, DateOnly startsOn, DateTimeOffset purchasedAt) =>
        new()
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            Price = plan.Price,
            StartsOn = startsOn,
            EndsOn = startsOn.AddDays(plan.ValidityDays - 1),
            VisitLimit = plan.VisitLimit,
            PurchasedAt = purchasedAt,
        };

    public bool IsCancelled => CancelledAt is not null;

    public int? RemainingVisits => VisitLimit - VisitsUsed;

    public bool HasVisitsRemaining => VisitLimit is null || VisitsUsed < VisitLimit;

    public bool IsActiveOn(DateOnly date) => !IsCancelled && HasVisitsRemaining && StartsOn <= date && date <= EndsOn;

    internal bool Blocks(DateOnly startsOn, DateOnly endsOn) =>
        !IsCancelled && HasVisitsRemaining && StartsOn <= endsOn && startsOn <= EndsOn;

    internal void RegisterVisit(DateOnly today)
    {
        if (!HasVisitsRemaining)
            throw new DomainException("The membership has no visits left.");

        if (LastVisitOn == today)
            throw new DomainException("The client has already checked in today.");

        VisitsUsed++;
        LastVisitOn = today;
    }

    internal void Cancel(DateTimeOffset now)
    {
        if (IsCancelled)
            throw new DomainException("The membership is already cancelled.");

        if (EndsOn < now.ToDateOnly())
            throw new DomainException("An expired membership cannot be cancelled.");

        CancelledAt = now;
    }
}
```

`api/src/FitnessClub.Domain/Clients/Client.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Visits;

namespace FitnessClub.Domain.Clients;

public sealed class Client : AggregateRoot
{
    public const int MaxAge = 120;

    private readonly List<Membership> _memberships = [];

    public PersonName Name { get; private set; } = null!;
    public DateOnly DateOfBirth { get; private set; }
    public PhoneNumber Phone { get; private set; } = null!;
    public EmailAddress? Email { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public IReadOnlyCollection<Membership> Memberships => _memberships.AsReadOnly();

    private Client()
    {
    }

    public static Client Register(PersonName name, DateOnly dateOfBirth, PhoneNumber phone, EmailAddress? email, DateTimeOffset now)
    {
        var client = new Client { RegisteredAt = now };
        client.UpdateProfile(name, dateOfBirth, phone, email, now);
        return client;
    }

    public void UpdateProfile(PersonName name, DateOnly dateOfBirth, PhoneNumber phone, EmailAddress? email, DateTimeOffset now)
    {
        var today = now.ToDateOnly();
        if (dateOfBirth > today)
            throw new DomainException("Date of birth cannot be in the future.");

        if (AgeOn(dateOfBirth, today) > MaxAge)
            throw new DomainException($"Age cannot be more than {MaxAge} years.");

        Name = name;
        DateOfBirth = dateOfBirth;
        Phone = phone;
        Email = email;
    }

    public int AgeOn(DateOnly date) => AgeOn(DateOfBirth, date);

    public Membership? ActiveMembershipOn(DateOnly date) =>
        _memberships.Where(m => m.IsActiveOn(date)).OrderBy(m => m.EndsOn).FirstOrDefault();

    public bool HasActiveMembershipOn(DateOnly date) => ActiveMembershipOn(date) is not null;

    public Payment PurchaseMembership(MembershipPlan plan, DateOnly startsOn, PaymentMethod method, DateTimeOffset now)
    {
        if (!plan.IsActive)
            throw new DomainException($"Membership plan '{plan.Name}' is not available for sale.");

        if (startsOn < now.ToDateOnly())
            throw new DomainException("A membership cannot start in the past.");

        var membership = Membership.Create(plan, startsOn, now);
        if (_memberships.Any(m => m.Blocks(membership.StartsOn, membership.EndsOn)))
            throw new DomainException("The client already has a membership for these dates.");

        var payment = Payment.ForMembership(Id, membership, method, now);
        _memberships.Add(membership);
        return payment;
    }

    public void CancelMembership(Guid membershipId, DateTimeOffset now) => FindMembership(membershipId).Cancel(now);

    public Visit CheckIn(DateTimeOffset now)
    {
        var today = now.ToDateOnly();
        var membership = ActiveMembershipOn(today)
            ?? throw new DomainException("The client has no active membership today.");

        membership.RegisterVisit(today);
        return Visit.Record(Id, membership.Id, now);
    }

    public bool Owns(Membership membership) => _memberships.Contains(membership);

    public bool NeedsExpiryNotice(Membership membership, DateOnly today) =>
        Owns(membership)
        && !membership.IsCancelled
        && membership.HasVisitsRemaining
        && membership.EndsOn >= today
        && !_memberships.Any(other => other != membership && !other.IsCancelled && other.StartsOn > membership.StartsOn);

    public IReadOnlyList<Membership> MembershipsNeedingExpiryNotice(DateOnly today, DateOnly endsBy) =>
        _memberships.Where(m => m.EndsOn <= endsBy && NeedsExpiryNotice(m, today)).ToList();

    private Membership FindMembership(Guid membershipId) =>
        _memberships.FirstOrDefault(m => m.Id == membershipId)
        ?? throw new DomainException($"Membership '{membershipId}' does not belong to this client.");

    private static int AgeOn(DateOnly dateOfBirth, DateOnly date)
    {
        var age = date.Year - dateOfBirth.Year;
        return date < dateOfBirth.AddYears(age) ? age - 1 : age;
    }
}
```

`api/src/FitnessClub.Domain/Clients/IClientRepository.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Clients;

public interface IClientRepository : IRepository<Client>
{
    Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken);

    Task<bool> PhoneExistsAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Client>> ListWithMembershipsEndingBetweenAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
```

`api/src/FitnessClub.Domain/Visits/Visit.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Visits;

public sealed class Visit : AggregateRoot
{
    public Guid ClientId { get; private set; }
    public Guid MembershipId { get; private set; }
    public DateTimeOffset CheckedInAt { get; private set; }

    private Visit()
    {
    }

    internal static Visit Record(Guid clientId, Guid membershipId, DateTimeOffset checkedInAt) =>
        new() { ClientId = clientId, MembershipId = membershipId, CheckedInAt = checkedInAt };
}
```

`api/src/FitnessClub.Domain/Visits/IVisitRepository.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Visits;

public interface IVisitRepository : IRepository<Visit>
{
    Task<IReadOnlyList<Visit>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken);
}
```

`api/src/FitnessClub.Domain/Payments/PaymentMethod.cs`:

```csharp
namespace FitnessClub.Domain.Payments;

public enum PaymentMethod
{
    Cash,
    Card,
}
```

`api/src/FitnessClub.Domain/Payments/Payment.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Payments;

public sealed class Payment : AggregateRoot
{
    public Guid ClientId { get; private set; }
    public Guid MembershipId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public PaymentMethod Method { get; private set; }
    public DateTimeOffset PaidAt { get; private set; }

    private Payment()
    {
    }

    internal static Payment ForMembership(Guid clientId, Clients.Membership membership, PaymentMethod method, DateTimeOffset paidAt)
    {
        if (!Enum.IsDefined(method))
            throw new DomainException("Unknown payment method.");

        return new Payment
        {
            ClientId = clientId,
            MembershipId = membership.Id,
            Amount = membership.Price,
            Method = method,
            PaidAt = paidAt,
        };
    }
}
```

`api/src/FitnessClub.Domain/Payments/IPaymentRepository.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Payments;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<IReadOnlyList<Payment>> ListPaidBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement**

Memberships are an owned collection, so loading a client always loads them. Cross-aggregate links are plain id columns with restrict foreign keys:

`api/src/FitnessClub.Infrastructure/Persistence/Configurations/ClientConfiguration.cs`:

```csharp
using FitnessClub.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.OwnsPersonName(c => c.Name);
        builder.Navigation(c => c.Name).IsRequired();
        builder.Property(c => c.Phone).HasPhoneConversion().IsRequired();
        builder.HasIndex(c => c.Phone).IsUnique();
        builder.Property(c => c.Email).HasEmailConversion();

        builder.OwnsMany(c => c.Memberships, membership =>
        {
            membership.ToTable("Memberships");
            membership.WithOwner().HasForeignKey("ClientId");
            membership.HasKey(m => m.Id);
            membership.Property(m => m.Id).ValueGeneratedNever();
            membership.Property(m => m.PlanName).HasMaxLength(Domain.MembershipPlans.MembershipPlan.NameMaxLength).IsRequired();
            membership.Property(m => m.Price).HasMoneyConversion();
            membership.HasIndex(m => m.EndsOn);
        });
        builder.Navigation(c => c.Memberships).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Configurations/VisitConfiguration.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Visits;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.ToTable("Visits");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.HasOne<Client>().WithMany().HasForeignKey(v => v.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => new { v.ClientId, v.CheckedInAt });
    }
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Configurations/PaymentConfiguration.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Amount).HasMoneyConversion();
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Client>().WithMany().HasForeignKey(p => p.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.PaidAt);
        builder.HasIndex(p => p.MembershipId).IsUnique();
    }
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Repositories/ClientRepository.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class ClientRepository(FitnessClubDbContext db) : Repository<Client>(db), IClientRepository
{
    public async Task<IReadOnlyList<Client>> ListAsync(CancellationToken cancellationToken) =>
        await Set.OrderBy(c => c.Name.LastName).ThenBy(c => c.Name.FirstName).ToListAsync(cancellationToken);

    public Task<bool> PhoneExistsAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(c => c.Id != excludeId && c.Phone == phone, cancellationToken);

    public async Task<IReadOnlyList<Client>> ListWithMembershipsEndingBetweenAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await Set
            .Where(c => c.Memberships.Any(m => m.CancelledAt == null && m.EndsOn >= from && m.EndsOn <= to))
            .ToListAsync(cancellationToken);
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Repositories/VisitRepository.cs`:

```csharp
using FitnessClub.Domain.Visits;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class VisitRepository(FitnessClubDbContext db) : Repository<Visit>(db), IVisitRepository
{
    public async Task<IReadOnlyList<Visit>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        await Set.Where(v => v.ClientId == clientId).OrderByDescending(v => v.CheckedInAt).ToListAsync(cancellationToken);
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Repositories/PaymentRepository.cs`:

```csharp
using FitnessClub.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class PaymentRepository(FitnessClubDbContext db) : Repository<Payment>(db), IPaymentRepository
{
    public async Task<IReadOnlyList<Payment>> ListPaidBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await Set.Where(p => p.PaidAt >= from && p.PaidAt < to).OrderBy(p => p.PaidAt).ToListAsync(cancellationToken);
}
```

- [ ] **Step 5: Implement**

Expose the aggregates and register the repositories:

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
using FitnessClub.Domain.Common;
````

with:

````csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
````

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
using FitnessClub.Domain.MembershipPlans;
````

with:

````csharp
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
````

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
using FitnessClub.Domain.Rooms;
````

with:

````csharp
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.Visits;
````

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
````

with:

````csharp
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Payment> Payments => Set<Payment>();
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
using FitnessClub.Domain.MembershipPlans;
````

with:

````csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
using FitnessClub.Domain.MembershipPlans;
````

with:

````csharp
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
using FitnessClub.Domain.Rooms;
````

with:

````csharp
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.Visits;
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
        services.AddScoped<IMembershipPlanRepository, MembershipPlanRepository>();
````

with:

````csharp
        services.AddScoped<IMembershipPlanRepository, MembershipPlanRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IVisitRepository, VisitRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
````

- [ ] **Step 6: Run and confirm it passes**

Run from `api/`:

```sh
dotnet test
```

Expected: PASS: 135 tests.

- [ ] **Step 7: Commit**

From the repo root:

```sh
git add api/src api/tests
git commit -m "feat(api): client aggregate with memberships, visits and payments"
```


### Task 6: Trainer aggregate with working hours and client list

**Files:**
- Create: `api/src/FitnessClub.Domain/Trainers/Trainer.cs`, `WorkingHours.cs`, `ClientAssignment.cs`, `ITrainerRepository.cs`
- Create: `api/src/FitnessClub.Infrastructure/Persistence/Configurations/TrainerConfiguration.cs`, `Repositories/TrainerRepository.cs`; Modify: `FitnessClubDbContext.cs`, `DependencyInjection.cs`
- Test: `api/tests/FitnessClub.UnitTests/Domain/TestData.cs` (grows), `TrainerTests.cs`, `api/tests/FitnessClub.IntegrationTests/Persistence/TrainerRepositoryTests.cs`

**Interfaces:**
- Consumes: Task 2 value objects; Task 5 `Client` (for the client-list foreign key and tests).
- Produces `Trainer : AggregateRoot`: `SpecializationMaxLength = 100`, `IdentityUserIdMaxLength = 128`; `Name`, `Phone`, `Email?`, `Specialization`, `IdentityUserId?`, `IsActive`, `IReadOnlyCollection<WorkingHours> WorkingHours`, `IReadOnlyCollection<ClientAssignment> Clients`; `static Hire(PersonName, PhoneNumber, EmailAddress?, string specialization)`; `UpdateProfile(...)`; `LinkIdentity(string)`; `SetWorkingHours(IEnumerable<WorkingHours>)`; `bool IsWorkingDuring(TimeSlot)`; `AssignClient(Guid clientId, DateTimeOffset now)`; `UnassignClient(Guid clientId)`; `Activate()`, `Deactivate()`.
- Produces value objects `WorkingHours.Create(DayOfWeek, TimeOnly start, TimeOnly end)` with `Overlaps`, `Covers(DayOfWeek, TimeOnly, TimeOnly)`, and `ClientAssignment` (`ClientId`, `AssignedAt`).
- Produces `ITrainerRepository`: `ListAsync(bool includeInactive, ct)`, `PhoneExistsAsync(PhoneNumber, Guid? excludeId, ct)`, `GetByIdentityUserIdAsync(string, ct)`.
- Produces test helpers in `TestData`: `Trainer()` (works 08:00–20:00 every day), `Slot(startHour, durationMinutes, daysFromToday)`.

- [ ] **Step 1: Write the failing tests**

`api/tests/FitnessClub.UnitTests/Domain/TestData.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.UnitTests.Domain;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 5);

    public static MembershipPlan Plan(int validityDays = 30, int? visitLimit = null, decimal price = 800m) =>
        MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(price), validityDays, visitLimit);

    public static Client Client(string? email = null) =>
        FitnessClub.Domain.Clients.Client.Register(
            PersonName.Create("Olena", "Shevchenko", null),
            new DateOnly(1995, 3, 14),
            PhoneNumber.Create("+380671234567"),
            email is null ? null : EmailAddress.Create(email),
            Now);

    public static Client ClientWithMembership(int validityDays = 30, int? visitLimit = null)
    {
        var client = Client();
        Buy(client, Plan(validityDays, visitLimit));
        return client;
    }

    public static Membership Buy(Client client, MembershipPlan plan, DateOnly? startsOn = null)
    {
        var payment = client.PurchaseMembership(plan, startsOn ?? Today, PaymentMethod.Cash, Now);
        return client.Memberships.Single(m => m.Id == payment.MembershipId);
    }

    public static Trainer Trainer()
    {
        var trainer = FitnessClub.Domain.Trainers.Trainer.Hire(
            PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create("+380501112233"), null, "Yoga");
        trainer.SetWorkingHours(Enum.GetValues<DayOfWeek>().Select(day => WorkingHours.Create(day, new TimeOnly(8, 0), new TimeOnly(20, 0))));
        return trainer;
    }

    public static TimeSlot Slot(int startHour = 10, int durationMinutes = 60, int daysFromToday = 1)
    {
        var start = new DateTimeOffset(Today.AddDays(daysFromToday), new TimeOnly(startHour, 0), TimeSpan.Zero);
        return TimeSlot.Create(start, start.AddMinutes(durationMinutes));
    }
}
```

`api/tests/FitnessClub.UnitTests/Domain/TrainerTests.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.UnitTests.Domain;

public class TrainerTests
{
    private static Trainer NewTrainer() =>
        Trainer.Hire(PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create("+380501112233"), null, "  Yoga ");

    [Fact]
    public void Hire_trims_specialization_and_starts_active()
    {
        var trainer = NewTrainer();

        Assert.Equal("Yoga", trainer.Specialization);
        Assert.True(trainer.IsActive);
        Assert.Empty(trainer.WorkingHours);
    }

    [Fact]
    public void Hire_without_specialization_throws()
    {
        Assert.Throws<DomainException>(() =>
            Trainer.Hire(PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create("+380501112233"), null, " "));
    }

    [Fact]
    public void WorkingHours_must_end_after_start()
    {
        Assert.Throws<DomainException>(() => WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void SetWorkingHours_with_overlap_on_same_day_throws_and_keeps_old_hours()
    {
        var trainer = NewTrainer();
        trainer.SetWorkingHours([WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0))]);

        Assert.Throws<DomainException>(() => trainer.SetWorkingHours(
        [
            WorkingHours.Create(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            WorkingHours.Create(DayOfWeek.Tuesday, new TimeOnly(11, 0), new TimeOnly(14, 0)),
        ]));

        Assert.Equal(DayOfWeek.Monday, trainer.WorkingHours.Single().Day);
    }

    [Fact]
    public void SetWorkingHours_allows_split_shifts_on_one_day()
    {
        var trainer = NewTrainer();

        trainer.SetWorkingHours(
        [
            WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0)),
            WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
        ]);

        Assert.Equal([new TimeOnly(8, 0), new TimeOnly(14, 0)], trainer.WorkingHours.Select(h => h.Start));
    }

    [Fact]
    public void IsWorkingDuring_requires_slot_inside_one_shift()
    {
        var trainer = NewTrainer();
        var day = TestData.Slot(daysFromToday: 1).Start.DayOfWeek;
        trainer.SetWorkingHours([WorkingHours.Create(day, new TimeOnly(9, 0), new TimeOnly(12, 0))]);

        Assert.True(trainer.IsWorkingDuring(TestData.Slot(startHour: 9, durationMinutes: 180)));
        Assert.False(trainer.IsWorkingDuring(TestData.Slot(startHour: 11, durationMinutes: 90)));
        Assert.False(trainer.IsWorkingDuring(TestData.Slot(startHour: 9, daysFromToday: 2)));
    }

    [Fact]
    public void Inactive_trainer_is_not_working()
    {
        var trainer = TestData.Trainer();
        trainer.Deactivate();

        Assert.False(trainer.IsWorkingDuring(TestData.Slot()));
    }

    [Fact]
    public void AssignClient_adds_client_once()
    {
        var trainer = NewTrainer();
        var clientId = Guid.NewGuid();

        trainer.AssignClient(clientId, TestData.Now);

        Assert.Equal(clientId, trainer.Clients.Single().ClientId);
        Assert.Throws<DomainException>(() => trainer.AssignClient(clientId, TestData.Now));
    }

    [Fact]
    public void AssignClient_to_inactive_trainer_throws()
    {
        var trainer = NewTrainer();
        trainer.Deactivate();

        Assert.Throws<DomainException>(() => trainer.AssignClient(Guid.NewGuid(), TestData.Now));
    }

    [Fact]
    public void UnassignClient_removes_client_and_rejects_unknown_client()
    {
        var trainer = NewTrainer();
        var clientId = Guid.NewGuid();
        trainer.AssignClient(clientId, TestData.Now);

        trainer.UnassignClient(clientId);

        Assert.Empty(trainer.Clients);
        Assert.Throws<DomainException>(() => trainer.UnassignClient(clientId));
    }

    [Fact]
    public void LinkIdentity_requires_value()
    {
        var trainer = NewTrainer();

        Assert.Throws<DomainException>(() => trainer.LinkIdentity(" "));

        trainer.LinkIdentity(" auth0|abc ");
        Assert.Equal("auth0|abc", trainer.IdentityUserId);
    }
}
```

`api/tests/FitnessClub.IntegrationTests/Persistence/TrainerRepositoryTests.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Persistence;

public class TrainerRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    private static Trainer NewTrainer() =>
        Trainer.Hire(PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create(UniquePhone()), null, "Yoga");

    [Fact]
    public async Task Trainer_round_trips_with_working_hours_and_clients()
    {
        var client = Client.Register(PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), PhoneNumber.Create(UniquePhone()), null, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));
        var trainer = NewTrainer();
        trainer.SetWorkingHours(
        [
            WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            WorkingHours.Create(DayOfWeek.Wednesday, new TimeOnly(14, 0), new TimeOnly(20, 0)),
        ]);
        trainer.AssignClient(client.Id, Now);
        trainer.LinkIdentity($"auth0|{Guid.NewGuid():N}");
        await SaveAsync<ITrainerRepository>(trainers => trainers.Add(trainer));

        var loaded = await ReadAsync<ITrainerRepository, Trainer?>(trainers => trainers.GetByIdentityUserIdAsync(trainer.IdentityUserId!, Ct));

        Assert.NotNull(loaded);
        Assert.Equal(trainer.WorkingHours, loaded.WorkingHours);
        Assert.Equal(client.Id, Assert.Single(loaded.Clients).ClientId);
    }

    [Fact]
    public async Task Replacing_working_hours_removes_old_rows()
    {
        var trainer = NewTrainer();
        trainer.SetWorkingHours([WorkingHours.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0))]);
        await SaveAsync<ITrainerRepository>(trainers => trainers.Add(trainer));

        await ChangeAsync<ITrainerRepository>(async trainers =>
        {
            var loaded = await trainers.GetByIdAsync(trainer.Id, Ct);
            loaded!.SetWorkingHours([WorkingHours.Create(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(16, 0))]);
        });

        var reloaded = await ReadAsync<ITrainerRepository, Trainer?>(trainers => trainers.GetByIdAsync(trainer.Id, Ct));
        Assert.Equal(DayOfWeek.Friday, Assert.Single(reloaded!.WorkingHours).Day);
    }
}
```

- [ ] **Step 2: Run and confirm it fails**

Run from `api/`:

```sh
dotnet test --project tests/FitnessClub.UnitTests --filter-class "FitnessClub.UnitTests.Domain.TrainerTests"
```

Expected: FAIL: build error `CS0234: The type or namespace name 'Trainers' does not exist in the namespace 'FitnessClub.Domain'`.

- [ ] **Step 3: Implement**

`api/src/FitnessClub.Domain/Trainers/WorkingHours.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Trainers;

public sealed record WorkingHours
{
    public DayOfWeek Day { get; private init; }
    public TimeOnly Start { get; private init; }
    public TimeOnly End { get; private init; }

    private WorkingHours()
    {
    }

    public static WorkingHours Create(DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        if (!Enum.IsDefined(day))
            throw new DomainException("Unknown day of week.");

        if (end <= start)
            throw new DomainException("Working hours must end after they start.");

        return new WorkingHours { Day = day, Start = start, End = end };
    }

    public bool Overlaps(WorkingHours other) => Day == other.Day && Start < other.End && other.Start < End;

    public bool Covers(DayOfWeek day, TimeOnly start, TimeOnly end) => Day == day && Start <= start && end <= End;
}
```

`api/src/FitnessClub.Domain/Trainers/ClientAssignment.cs`:

```csharp
namespace FitnessClub.Domain.Trainers;

public sealed record ClientAssignment
{
    public Guid ClientId { get; private init; }
    public DateTimeOffset AssignedAt { get; private init; }

    private ClientAssignment()
    {
    }

    internal static ClientAssignment Create(Guid clientId, DateTimeOffset assignedAt) =>
        new() { ClientId = clientId, AssignedAt = assignedAt };
}
```

`api/src/FitnessClub.Domain/Trainers/Trainer.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Trainers;

public sealed class Trainer : AggregateRoot
{
    public const int SpecializationMaxLength = 100;
    public const int IdentityUserIdMaxLength = 128;

    private readonly List<WorkingHours> _workingHours = [];
    private readonly List<ClientAssignment> _clients = [];

    public PersonName Name { get; private set; } = null!;
    public PhoneNumber Phone { get; private set; } = null!;
    public EmailAddress? Email { get; private set; }
    public string Specialization { get; private set; } = null!;
    public string? IdentityUserId { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<WorkingHours> WorkingHours => _workingHours.AsReadOnly();
    public IReadOnlyCollection<ClientAssignment> Clients => _clients.AsReadOnly();

    private Trainer()
    {
    }

    public static Trainer Hire(PersonName name, PhoneNumber phone, EmailAddress? email, string specialization)
    {
        var trainer = new Trainer { IsActive = true };
        trainer.UpdateProfile(name, phone, email, specialization);
        return trainer;
    }

    public void UpdateProfile(PersonName name, PhoneNumber phone, EmailAddress? email, string specialization)
    {
        if (string.IsNullOrWhiteSpace(specialization))
            throw new DomainException("Specialization is required.");

        var trimmedSpecialization = specialization.Trim();
        if (trimmedSpecialization.Length > SpecializationMaxLength)
            throw new DomainException($"Specialization must be at most {SpecializationMaxLength} characters.");

        Name = name;
        Phone = phone;
        Email = email;
        Specialization = trimmedSpecialization;
    }

    public void LinkIdentity(string identityUserId)
    {
        if (string.IsNullOrWhiteSpace(identityUserId))
            throw new DomainException("Identity user id is required.");

        var trimmed = identityUserId.Trim();
        if (trimmed.Length > IdentityUserIdMaxLength)
            throw new DomainException($"Identity user id must be at most {IdentityUserIdMaxLength} characters.");

        IdentityUserId = trimmed;
    }

    public void SetWorkingHours(IEnumerable<WorkingHours> workingHours)
    {
        var hours = workingHours.ToList();
        for (var i = 0; i < hours.Count; i++)
            for (var j = i + 1; j < hours.Count; j++)
                if (hours[i].Overlaps(hours[j]))
                    throw new DomainException($"Working hours overlap on {hours[i].Day}.");

        _workingHours.Clear();
        _workingHours.AddRange(hours.OrderBy(h => h.Day).ThenBy(h => h.Start));
    }

    public bool IsWorkingDuring(TimeSlot slot) =>
        IsActive
        && slot.Start.ToDateOnly() == slot.End.ToDateOnly()
        && _workingHours.Any(h => h.Covers(slot.Start.DayOfWeek, slot.Start.ToTimeOnly(), slot.End.ToTimeOnly()));

    public void AssignClient(Guid clientId, DateTimeOffset now)
    {
        if (!IsActive)
            throw new DomainException("An inactive trainer cannot take clients.");

        if (_clients.Any(c => c.ClientId == clientId))
            throw new DomainException("The client is already assigned to this trainer.");

        _clients.Add(ClientAssignment.Create(clientId, now));
    }

    public void UnassignClient(Guid clientId)
    {
        var assignment = _clients.FirstOrDefault(c => c.ClientId == clientId)
            ?? throw new DomainException("The client is not assigned to this trainer.");

        _clients.Remove(assignment);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
```

`api/src/FitnessClub.Domain/Trainers/ITrainerRepository.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Trainers;

public interface ITrainerRepository : IRepository<Trainer>
{
    Task<IReadOnlyList<Trainer>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<bool> PhoneExistsAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken);

    Task<Trainer?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken);
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Configurations/TrainerConfiguration.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Trainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class TrainerConfiguration : IEntityTypeConfiguration<Trainer>
{
    public void Configure(EntityTypeBuilder<Trainer> builder)
    {
        builder.ToTable("Trainers");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.OwnsPersonName(t => t.Name);
        builder.Navigation(t => t.Name).IsRequired();
        builder.Property(t => t.Phone).HasPhoneConversion().IsRequired();
        builder.HasIndex(t => t.Phone).IsUnique();
        builder.Property(t => t.Email).HasEmailConversion();
        builder.Property(t => t.Specialization).HasMaxLength(Trainer.SpecializationMaxLength).IsRequired();
        builder.Property(t => t.IdentityUserId).HasMaxLength(Trainer.IdentityUserIdMaxLength);
        builder.HasIndex(t => t.IdentityUserId).IsUnique().HasFilter("[IdentityUserId] IS NOT NULL");

        builder.OwnsMany(t => t.WorkingHours, hours =>
        {
            hours.ToTable("TrainerWorkingHours");
            hours.WithOwner().HasForeignKey("TrainerId");
            hours.Property<int>("Id");
            hours.HasKey("Id");
            hours.Property(h => h.Day).HasConversion<string>().HasMaxLength(10);
        });
        builder.Navigation(t => t.WorkingHours).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(t => t.Clients, assignment =>
        {
            assignment.ToTable("TrainerClients");
            assignment.WithOwner().HasForeignKey("TrainerId");
            assignment.HasKey("TrainerId", nameof(ClientAssignment.ClientId));
            assignment.HasOne<Client>().WithMany().HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(t => t.Clients).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Repositories/TrainerRepository.cs`:

```csharp
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class TrainerRepository(FitnessClubDbContext db) : Repository<Trainer>(db), ITrainerRepository
{
    public async Task<IReadOnlyList<Trainer>> ListAsync(bool includeInactive, CancellationToken cancellationToken) =>
        await Set
            .Where(t => includeInactive || t.IsActive)
            .OrderBy(t => t.Name.LastName)
            .ThenBy(t => t.Name.FirstName)
            .ToListAsync(cancellationToken);

    public Task<bool> PhoneExistsAsync(PhoneNumber phone, Guid? excludeId, CancellationToken cancellationToken) =>
        Set.AnyAsync(t => t.Id != excludeId && t.Phone == phone, cancellationToken);

    public Task<Trainer?> GetByIdentityUserIdAsync(string identityUserId, CancellationToken cancellationToken) =>
        Set.FirstOrDefaultAsync(t => t.IdentityUserId == identityUserId, cancellationToken);
}
```

- [ ] **Step 4: Implement**

Expose the aggregate and register its repository:

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
using FitnessClub.Domain.Rooms;
````

with:

````csharp
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.Trainers;
````

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
    public DbSet<Payment> Payments => Set<Payment>();
````

with:

````csharp
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Trainer> Trainers => Set<Trainer>();
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
using FitnessClub.Domain.Rooms;
````

with:

````csharp
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.Trainers;
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
        services.AddScoped<IPaymentRepository, PaymentRepository>();
````

with:

````csharp
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ITrainerRepository, TrainerRepository>();
````

- [ ] **Step 5: Run and confirm it passes**

Run from `api/`:

```sh
dotnet test
```

Expected: PASS: 148 tests.

- [ ] **Step 6: Commit**

From the repo root:

```sh
git add api/src api/tests
git commit -m "feat(api): trainer aggregate with working hours and client list"
```


### Task 7: TrainingSession aggregate with bookings and the session scheduler

**Files:**
- Create: `api/src/FitnessClub.Domain/Training/TrainingSession.cs`, `Booking.cs`, `SessionType.cs`, `SessionStatus.cs`, `ITrainingSessionRepository.cs`, `ISessionScheduler.cs`, `SessionScheduler.cs`
- Create: `api/src/FitnessClub.Infrastructure/Persistence/Configurations/TrainingSessionConfiguration.cs`, `Repositories/TrainingSessionRepository.cs`; Modify: `FitnessClubDbContext.cs`, `DependencyInjection.cs`
- Test: `api/tests/FitnessClub.UnitTests/Domain/TestData.cs` (final), `Fakes/InMemoryTrainingSessionRepository.cs`, `Domain/TrainingSessionTests.cs`, `api/tests/FitnessClub.IntegrationTests/Persistence/TrainingSessionRepositoryTests.cs`

**Interfaces:**
- Consumes: `Trainer.IsWorkingDuring`, `Room`, `Client.HasActiveMembershipOn`, `TimeSlot`.
- Produces `TrainingSession : AggregateRoot`: `TitleMaxLength = 100`, `MinDuration` (15 min), `MaxDuration` (4 h); `Title`, `Type` (`Group | Individual`), `TrainerId`, `RoomId`, `TimeSlot Slot`, `Capacity`, `Status` (`Scheduled | Cancelled`), `IReadOnlyCollection<Booking> Bookings`, `ActiveBookingCount`; `CancelBooking(Guid clientId, DateTimeOffset now)`; `Cancel(DateTimeOffset now)` (also cancels its bookings). Creation and booking are `internal` and go only through the scheduler.
- Produces `Booking : Entity`: `ClientId`, `BookedAt`, `CancelledAt`, `IsActive`.
- Produces `interface ISessionScheduler { Task<TrainingSession> ScheduleAsync(string title, SessionType type, Trainer trainer, Room room, TimeSlot slot, int capacity, DateTimeOffset now, CancellationToken ct); Task<Booking> BookAsync(TrainingSession session, Client client, DateTimeOffset now, CancellationToken ct); }` and `sealed class SessionScheduler(ITrainingSessionRepository sessions)`. The scheduler does not add or save; the caller adds the new session to the repository and saves through `IUnitOfWork`.
- Produces `ITrainingSessionRepository`: `TrainerHasSessionDuringAsync(Guid trainerId, TimeSlot, ct)`, `RoomIsBookedDuringAsync(Guid roomId, TimeSlot, ct)`, `ClientHasBookingDuringAsync(Guid clientId, TimeSlot, ct)` (scheduled sessions only), `ListStartingBetweenAsync(from, to, ct)`, `ListForTrainerStartingBetweenAsync(Guid trainerId, from, to, ct)`.

- [ ] **Step 1: Write the failing tests**

`api/tests/FitnessClub.UnitTests/Domain/TestData.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.UnitTests.Domain;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 5);

    public static MembershipPlan Plan(int validityDays = 30, int? visitLimit = null, decimal price = 800m) =>
        MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(price), validityDays, visitLimit);

    public static Client Client(string? email = null) =>
        FitnessClub.Domain.Clients.Client.Register(
            PersonName.Create("Olena", "Shevchenko", null),
            new DateOnly(1995, 3, 14),
            PhoneNumber.Create("+380671234567"),
            email is null ? null : EmailAddress.Create(email),
            Now);

    public static Client ClientWithMembership(int validityDays = 30, int? visitLimit = null)
    {
        var client = Client();
        Buy(client, Plan(validityDays, visitLimit));
        return client;
    }

    public static Membership Buy(Client client, MembershipPlan plan, DateOnly? startsOn = null)
    {
        var payment = client.PurchaseMembership(plan, startsOn ?? Today, PaymentMethod.Cash, Now);
        return client.Memberships.Single(m => m.Id == payment.MembershipId);
    }

    public static Room Room(int capacity = 20) => FitnessClub.Domain.Rooms.Room.Create($"Room {Guid.NewGuid():N}", capacity);

    public static Trainer Trainer()
    {
        var trainer = FitnessClub.Domain.Trainers.Trainer.Hire(
            PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create("+380501112233"), null, "Yoga");
        trainer.SetWorkingHours(Enum.GetValues<DayOfWeek>().Select(day => WorkingHours.Create(day, new TimeOnly(8, 0), new TimeOnly(20, 0))));
        return trainer;
    }

    public static TimeSlot Slot(int startHour = 10, int durationMinutes = 60, int daysFromToday = 1)
    {
        var start = new DateTimeOffset(Today.AddDays(daysFromToday), new TimeOnly(startHour, 0), TimeSpan.Zero);
        return TimeSlot.Create(start, start.AddMinutes(durationMinutes));
    }
}
```

`api/tests/FitnessClub.UnitTests/Fakes/InMemoryTrainingSessionRepository.cs`:

```csharp
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Training;

namespace FitnessClub.UnitTests.Fakes;

internal sealed class InMemoryTrainingSessionRepository : ITrainingSessionRepository
{
    private readonly List<TrainingSession> _sessions = [];

    public Task<TrainingSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));

    public void Add(TrainingSession aggregate) => _sessions.Add(aggregate);

    public Task<bool> TrainerHasSessionDuringAsync(Guid trainerId, TimeSlot slot, CancellationToken cancellationToken) =>
        Task.FromResult(Scheduled().Any(s => s.TrainerId == trainerId && s.Slot.Overlaps(slot)));

    public Task<bool> RoomIsBookedDuringAsync(Guid roomId, TimeSlot slot, CancellationToken cancellationToken) =>
        Task.FromResult(Scheduled().Any(s => s.RoomId == roomId && s.Slot.Overlaps(slot)));

    public Task<bool> ClientHasBookingDuringAsync(Guid clientId, TimeSlot slot, CancellationToken cancellationToken) =>
        Task.FromResult(Scheduled().Any(s => s.Slot.Overlaps(slot) && s.Bookings.Any(b => b.IsActive && b.ClientId == clientId)));

    public Task<IReadOnlyList<TrainingSession>> ListStartingBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TrainingSession>>(
            _sessions.Where(s => s.Slot.Start >= from && s.Slot.Start < to).OrderBy(s => s.Slot.Start).ToList());

    public Task<IReadOnlyList<TrainingSession>> ListForTrainerStartingBetweenAsync(
        Guid trainerId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TrainingSession>>(
            _sessions.Where(s => s.TrainerId == trainerId && s.Slot.Start >= from && s.Slot.Start < to).OrderBy(s => s.Slot.Start).ToList());

    private IEnumerable<TrainingSession> Scheduled() => _sessions.Where(s => s.Status == SessionStatus.Scheduled);
}
```

`api/tests/FitnessClub.UnitTests/Domain/TrainingSessionTests.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Training;
using FitnessClub.UnitTests.Fakes;

namespace FitnessClub.UnitTests.Domain;

public class TrainingSessionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly InMemoryTrainingSessionRepository _repository = new();
    private readonly SessionScheduler _scheduler;

    public TrainingSessionTests()
    {
        _scheduler = new SessionScheduler(_repository);
    }

    private Task<Booking> BookAsync(TrainingSession session, FitnessClub.Domain.Clients.Client client, DateTimeOffset? now = null) =>
        _scheduler.BookAsync(session, client, now ?? TestData.Now, Ct);

    private Task<TrainingSession> GroupSessionAsync(int capacity = 2) =>
        _scheduler.ScheduleAsync("Morning yoga", SessionType.Group, TestData.Trainer(), TestData.Room(), TestData.Slot(), capacity, TestData.Now, Ct);

    [Fact]
    public async Task Schedule_creates_scheduled_session()
    {
        var trainer = TestData.Trainer();
        var room = TestData.Room(capacity: 20);
        var slot = TestData.Slot();

        var session = await _scheduler.ScheduleAsync(" Morning yoga ", SessionType.Group, trainer, room, slot, 15, TestData.Now, Ct);

        Assert.Equal("Morning yoga", session.Title);
        Assert.Equal(trainer.Id, session.TrainerId);
        Assert.Equal(room.Id, session.RoomId);
        Assert.Equal(slot, session.Slot);
        Assert.Equal(15, session.Capacity);
        Assert.Equal(SessionStatus.Scheduled, session.Status);
    }

    [Fact]
    public async Task Schedule_in_the_past_throws()
    {
        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Yoga", SessionType.Group, TestData.Trainer(), TestData.Room(), TestData.Slot(daysFromToday: -1), 5, TestData.Now, Ct));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(241)]
    public async Task Schedule_with_duration_out_of_range_throws(int minutes)
    {
        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Yoga", SessionType.Group, TestData.Trainer(), TestData.Room(), TestData.Slot(durationMinutes: minutes), 5, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_outside_trainer_hours_throws()
    {
        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Yoga", SessionType.Group, TestData.Trainer(), TestData.Room(), TestData.Slot(startHour: 19, durationMinutes: 90), 5, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_in_inactive_room_throws()
    {
        var room = TestData.Room();
        room.Deactivate();

        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Yoga", SessionType.Group, TestData.Trainer(), room, TestData.Slot(), 5, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_with_capacity_above_room_capacity_throws()
    {
        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Yoga", SessionType.Group, TestData.Trainer(), TestData.Room(capacity: 10), TestData.Slot(), 11, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_individual_session_with_more_than_one_place_throws()
    {
        await Assert.ThrowsAsync<DomainException>(() => _scheduler.ScheduleAsync(
            "Personal", SessionType.Individual, TestData.Trainer(), TestData.Room(), TestData.Slot(), 2, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_when_trainer_is_busy_throws()
    {
        var repository = new InMemoryTrainingSessionRepository();
        var scheduler = new SessionScheduler(repository);
        var trainer = TestData.Trainer();
        repository.Add(await scheduler.ScheduleAsync("Yoga", SessionType.Group, trainer, TestData.Room(), TestData.Slot(startHour: 10), 5, TestData.Now, Ct));

        await Assert.ThrowsAsync<DomainException>(() => scheduler.ScheduleAsync(
            "Pilates", SessionType.Group, trainer, TestData.Room(), TestData.Slot(startHour: 10, durationMinutes: 30), 5, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_when_room_is_taken_throws()
    {
        var repository = new InMemoryTrainingSessionRepository();
        var scheduler = new SessionScheduler(repository);
        var room = TestData.Room();
        repository.Add(await scheduler.ScheduleAsync("Yoga", SessionType.Group, TestData.Trainer(), room, TestData.Slot(startHour: 10), 5, TestData.Now, Ct));

        await Assert.ThrowsAsync<DomainException>(() => scheduler.ScheduleAsync(
            "Pilates", SessionType.Group, TestData.Trainer(), room, TestData.Slot(startHour: 10, durationMinutes: 30), 5, TestData.Now, Ct));
    }

    [Fact]
    public async Task Schedule_after_a_cancelled_session_in_the_same_room_is_allowed()
    {
        var repository = new InMemoryTrainingSessionRepository();
        var scheduler = new SessionScheduler(repository);
        var room = TestData.Room();
        var cancelled = await scheduler.ScheduleAsync("Yoga", SessionType.Group, TestData.Trainer(), room, TestData.Slot(), 5, TestData.Now, Ct);
        cancelled.Cancel(TestData.Now);
        repository.Add(cancelled);

        var session = await scheduler.ScheduleAsync("Pilates", SessionType.Group, TestData.Trainer(), room, TestData.Slot(), 5, TestData.Now, Ct);

        Assert.Equal(SessionStatus.Scheduled, session.Status);
    }

    [Fact]
    public async Task Book_adds_active_booking()
    {
        var session = await GroupSessionAsync();
        var client = TestData.ClientWithMembership();

        var booking = await BookAsync(session, client);

        Assert.Equal(client.Id, booking.ClientId);
        Assert.Equal(1, session.ActiveBookingCount);
    }

    [Fact]
    public async Task Book_without_membership_on_session_date_throws()
    {
        var session = await GroupSessionAsync();

        await Assert.ThrowsAsync<DomainException>(() => BookAsync(session, TestData.Client()));
    }

    [Fact]
    public async Task Book_same_client_twice_throws()
    {
        var session = await GroupSessionAsync();
        var client = TestData.ClientWithMembership();
        await BookAsync(session, client);

        await Assert.ThrowsAsync<DomainException>(() => BookAsync(session, client));
    }

    [Fact]
    public async Task Book_when_full_throws()
    {
        var session = await GroupSessionAsync(capacity: 1);
        await BookAsync(session, TestData.ClientWithMembership());

        await Assert.ThrowsAsync<DomainException>(() => BookAsync(session, TestData.ClientWithMembership()));
    }

    [Fact]
    public async Task Book_after_session_started_throws()
    {
        var session = await GroupSessionAsync();

        await Assert.ThrowsAsync<DomainException>(() => BookAsync(session, TestData.ClientWithMembership(), session.Slot.Start));
    }

    [Fact]
    public async Task CancelBooking_frees_the_place()
    {
        var session = await GroupSessionAsync(capacity: 1);
        var client = TestData.ClientWithMembership();
        await BookAsync(session, client);

        session.CancelBooking(client.Id, TestData.Now);

        Assert.Equal(0, session.ActiveBookingCount);
        await BookAsync(session, TestData.ClientWithMembership());
    }

    [Fact]
    public async Task CancelBooking_for_client_without_booking_throws()
    {
        var session = await GroupSessionAsync();

        Assert.Throws<DomainException>(() => session.CancelBooking(Guid.NewGuid(), TestData.Now));
    }

    [Fact]
    public async Task Cancelled_session_rejects_bookings_and_second_cancel()
    {
        var session = await GroupSessionAsync();
        await BookAsync(session, TestData.ClientWithMembership());

        session.Cancel(TestData.Now);

        Assert.Equal(SessionStatus.Cancelled, session.Status);
        Assert.Equal(0, session.ActiveBookingCount);
        await Assert.ThrowsAsync<DomainException>(() => BookAsync(session, TestData.ClientWithMembership()));
        Assert.Throws<DomainException>(() => session.Cancel(TestData.Now));
    }

    [Fact]
    public async Task Book_client_into_overlapping_session_throws()
    {
        var client = TestData.ClientWithMembership();
        var first = await GroupSessionAsync();
        await BookAsync(first, client);
        _repository.Add(first);
        var overlapping = await _scheduler.ScheduleAsync(
            "Pilates", SessionType.Group, TestData.Trainer(), TestData.Room(), TestData.Slot(startHour: 10, durationMinutes: 30), 5, TestData.Now, Ct);

        await Assert.ThrowsAsync<DomainException>(() => BookAsync(overlapping, client));
    }
}
```

`api/tests/FitnessClub.IntegrationTests/Persistence/TrainingSessionRepositoryTests.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Persistence;

public class TrainingSessionRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    private static readonly DateTimeOffset SessionStart = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

    private async Task<(Trainer Trainer, Room Room)> SeedTrainerAndRoomAsync()
    {
        var trainer = Trainer.Hire(PersonName.Create("Taras", "Bondar", null), PhoneNumber.Create(UniquePhone()), null, "Yoga");
        trainer.SetWorkingHours(Enum.GetValues<DayOfWeek>().Select(day => WorkingHours.Create(day, new TimeOnly(8, 0), new TimeOnly(20, 0))));
        var room = Room.Create($"Room {Guid.NewGuid():N}", 20);
        await SaveAsync<ITrainerRepository>(trainers => trainers.Add(trainer));
        await SaveAsync<IRoomRepository>(rooms => rooms.Add(room));
        return (trainer, room);
    }

    private static TimeSlot Slot(int startOffsetMinutes = 0, int durationMinutes = 60) =>
        TimeSlot.Create(SessionStart.AddMinutes(startOffsetMinutes), SessionStart.AddMinutes(startOffsetMinutes + durationMinutes));

    [Fact]
    public async Task Session_round_trips_with_slot_and_bookings()
    {
        var (trainer, room) = await SeedTrainerAndRoomAsync();
        var client = Client.Register(PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), PhoneNumber.Create(UniquePhone()), null, Now);
        client.PurchaseMembership(MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(800m), 30, null), Today, PaymentMethod.Cash, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));

        TrainingSession session = null!;
        await ChangeAsync<ITrainingSessionRepository>(async sessions =>
        {
            var scheduler = new SessionScheduler(sessions);
            session = await scheduler.ScheduleAsync("Yoga", SessionType.Group, trainer, room, Slot(), 10, Now, Ct);
            await scheduler.BookAsync(session, client, Now, Ct);
            sessions.Add(session);
        });

        var loaded = await ReadAsync<ITrainingSessionRepository, TrainingSession?>(sessions => sessions.GetByIdAsync(session.Id, Ct));

        Assert.NotNull(loaded);
        Assert.Equal(Slot(), loaded.Slot);
        Assert.Equal(client.Id, Assert.Single(loaded.Bookings).ClientId);
        Assert.True(await ReadAsync<ITrainingSessionRepository, bool>(s => s.ClientHasBookingDuringAsync(client.Id, Slot(30), Ct)));
        Assert.False(await ReadAsync<ITrainingSessionRepository, bool>(s => s.ClientHasBookingDuringAsync(client.Id, Slot(60), Ct)));
    }

    [Fact]
    public async Task Overlap_queries_detect_busy_trainer_and_room_but_ignore_cancelled_sessions()
    {
        var (trainer, room) = await SeedTrainerAndRoomAsync();
        var (otherTrainer, otherRoom) = await SeedTrainerAndRoomAsync();
        await ChangeAsync<ITrainingSessionRepository>(async sessions =>
        {
            var scheduler = new SessionScheduler(sessions);
            sessions.Add(await scheduler.ScheduleAsync("Yoga", SessionType.Group, trainer, room, Slot(), 10, Now, Ct));
            var cancelled = await scheduler.ScheduleAsync("Pilates", SessionType.Group, otherTrainer, otherRoom, Slot(), 10, Now, Ct);
            cancelled.Cancel(Now);
            sessions.Add(cancelled);
        });

        Assert.True(await ReadAsync<ITrainingSessionRepository, bool>(s => s.TrainerHasSessionDuringAsync(trainer.Id, Slot(30), Ct)));
        Assert.True(await ReadAsync<ITrainingSessionRepository, bool>(s => s.RoomIsBookedDuringAsync(room.Id, Slot(30), Ct)));
        Assert.False(await ReadAsync<ITrainingSessionRepository, bool>(s => s.TrainerHasSessionDuringAsync(trainer.Id, Slot(60), Ct)));
        Assert.False(await ReadAsync<ITrainingSessionRepository, bool>(s => s.TrainerHasSessionDuringAsync(otherTrainer.Id, Slot(), Ct)));
        Assert.False(await ReadAsync<ITrainingSessionRepository, bool>(s => s.RoomIsBookedDuringAsync(otherRoom.Id, Slot(), Ct)));

        var forTrainer = await ReadAsync<ITrainingSessionRepository, IReadOnlyList<TrainingSession>>(
            s => s.ListForTrainerStartingBetweenAsync(trainer.Id, SessionStart.AddHours(-1), SessionStart.AddHours(1), Ct));
        Assert.Single(forTrainer);
    }
}
```

- [ ] **Step 2: Run and confirm it fails**

Run from `api/`:

```sh
dotnet test --project tests/FitnessClub.UnitTests --filter-class "FitnessClub.UnitTests.Domain.TrainingSessionTests"
```

Expected: FAIL: build error `CS0234: The type or namespace name 'Training' does not exist in the namespace 'FitnessClub.Domain'`.

- [ ] **Step 3: Implement**

`api/src/FitnessClub.Domain/Training/SessionType.cs`:

```csharp
namespace FitnessClub.Domain.Training;

public enum SessionType
{
    Group,
    Individual,
}
```

`api/src/FitnessClub.Domain/Training/SessionStatus.cs`:

```csharp
namespace FitnessClub.Domain.Training;

public enum SessionStatus
{
    Scheduled,
    Cancelled,
}
```

`api/src/FitnessClub.Domain/Training/Booking.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Training;

public sealed class Booking : Entity
{
    public Guid ClientId { get; private set; }
    public DateTimeOffset BookedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }

    private Booking()
    {
    }

    internal static Booking Create(Guid clientId, DateTimeOffset bookedAt) =>
        new() { ClientId = clientId, BookedAt = bookedAt };

    public bool IsActive => CancelledAt is null;

    internal void Cancel(DateTimeOffset now) => CancelledAt = now;
}
```

`api/src/FitnessClub.Domain/Training/TrainingSession.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.Domain.Training;

public sealed class TrainingSession : AggregateRoot
{
    public const int TitleMaxLength = 100;
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(4);

    private readonly List<Booking> _bookings = [];

    public string Title { get; private set; } = null!;
    public SessionType Type { get; private set; }
    public Guid TrainerId { get; private set; }
    public Guid RoomId { get; private set; }
    public TimeSlot Slot { get; private set; } = null!;
    public int Capacity { get; private set; }
    public SessionStatus Status { get; private set; }
    public IReadOnlyCollection<Booking> Bookings => _bookings.AsReadOnly();

    private TrainingSession()
    {
    }

    internal static TrainingSession Create(
        string title, SessionType type, Trainer trainer, Room room, TimeSlot slot, int capacity, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Session title is required.");

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > TitleMaxLength)
            throw new DomainException($"Session title must be at most {TitleMaxLength} characters.");

        if (!Enum.IsDefined(type))
            throw new DomainException("Unknown session type.");

        if (slot.Start <= now)
            throw new DomainException("A session must be scheduled in the future.");

        if (slot.Duration < MinDuration || slot.Duration > MaxDuration)
            throw new DomainException($"A session must last between {MinDuration.TotalMinutes} minutes and {MaxDuration.TotalHours} hours.");

        if (!trainer.IsActive)
            throw new DomainException("An inactive trainer cannot run sessions.");

        if (!room.IsActive)
            throw new DomainException($"Room '{room.Name}' is not in use.");

        if (!trainer.IsWorkingDuring(slot))
            throw new DomainException("The trainer does not work at that time.");

        if (type == SessionType.Individual && capacity != 1)
            throw new DomainException("An individual session has exactly one place.");

        if (capacity < 1 || capacity > room.Capacity)
            throw new DomainException($"Capacity must be between 1 and the room capacity of {room.Capacity}.");

        return new TrainingSession
        {
            Title = trimmedTitle,
            Type = type,
            TrainerId = trainer.Id,
            RoomId = room.Id,
            Slot = slot,
            Capacity = capacity,
            Status = SessionStatus.Scheduled,
        };
    }

    public int ActiveBookingCount => _bookings.Count(b => b.IsActive);

    internal Booking Book(Client client, DateTimeOffset now)
    {
        EnsureOpen(now);

        if (!client.HasActiveMembershipOn(Slot.Start.ToDateOnly()))
            throw new DomainException("The client has no active membership on the session date.");

        if (_bookings.Any(b => b.IsActive && b.ClientId == client.Id))
            throw new DomainException("The client is already booked for this session.");

        if (ActiveBookingCount >= Capacity)
            throw new DomainException("The session is full.");

        var booking = Booking.Create(client.Id, now);
        _bookings.Add(booking);
        return booking;
    }

    public void CancelBooking(Guid clientId, DateTimeOffset now)
    {
        EnsureOpen(now);

        var booking = _bookings.FirstOrDefault(b => b.IsActive && b.ClientId == clientId)
            ?? throw new DomainException("The client has no booking for this session.");

        booking.Cancel(now);
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsureOpen(now);
        foreach (var booking in _bookings.Where(b => b.IsActive))
            booking.Cancel(now);

        Status = SessionStatus.Cancelled;
    }

    private void EnsureOpen(DateTimeOffset now)
    {
        if (Status == SessionStatus.Cancelled)
            throw new DomainException("The session is cancelled.");

        if (now >= Slot.Start)
            throw new DomainException("The session has already started.");
    }
}
```

`api/src/FitnessClub.Domain/Training/ITrainingSessionRepository.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.SharedKernel;

namespace FitnessClub.Domain.Training;

public interface ITrainingSessionRepository : IRepository<TrainingSession>
{
    Task<bool> TrainerHasSessionDuringAsync(Guid trainerId, TimeSlot slot, CancellationToken cancellationToken);

    Task<bool> RoomIsBookedDuringAsync(Guid roomId, TimeSlot slot, CancellationToken cancellationToken);

    Task<bool> ClientHasBookingDuringAsync(Guid clientId, TimeSlot slot, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainingSession>> ListStartingBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrainingSession>> ListForTrainerStartingBetweenAsync(
        Guid trainerId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
```

`api/src/FitnessClub.Domain/Training/ISessionScheduler.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.Domain.Training;

public interface ISessionScheduler
{
    Task<TrainingSession> ScheduleAsync(
        string title,
        SessionType type,
        Trainer trainer,
        Room room,
        TimeSlot slot,
        int capacity,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Booking> BookAsync(TrainingSession session, Client client, DateTimeOffset now, CancellationToken cancellationToken);
}
```

`api/src/FitnessClub.Domain/Training/SessionScheduler.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Trainers;

namespace FitnessClub.Domain.Training;

public sealed class SessionScheduler(ITrainingSessionRepository sessions) : ISessionScheduler
{
    public async Task<TrainingSession> ScheduleAsync(
        string title,
        SessionType type,
        Trainer trainer,
        Room room,
        TimeSlot slot,
        int capacity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = TrainingSession.Create(title, type, trainer, room, slot, capacity, now);

        if (await sessions.TrainerHasSessionDuringAsync(trainer.Id, slot, cancellationToken))
            throw new DomainException("The trainer already has a session at that time.");

        if (await sessions.RoomIsBookedDuringAsync(room.Id, slot, cancellationToken))
            throw new DomainException($"Room '{room.Name}' is already booked at that time.");

        return session;
    }

    public async Task<Booking> BookAsync(TrainingSession session, Client client, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await sessions.ClientHasBookingDuringAsync(client.Id, session.Slot, cancellationToken))
            throw new DomainException("The client already has a booking at that time.");

        return session.Book(client, now);
    }
}
```

- [ ] **Step 4: Implement**

`api/src/FitnessClub.Infrastructure/Persistence/Configurations/TrainingSessionConfiguration.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Rooms;
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class TrainingSessionConfiguration : IEntityTypeConfiguration<TrainingSession>
{
    public void Configure(EntityTypeBuilder<TrainingSession> builder)
    {
        builder.ToTable("TrainingSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Title).HasMaxLength(TrainingSession.TitleMaxLength).IsRequired();
        builder.Property(s => s.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Trainer>().WithMany().HasForeignKey(s => s.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany().HasForeignKey(s => s.RoomId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(s => s.Slot, slot =>
        {
            slot.Property(t => t.Start).HasColumnName("StartsAt");
            slot.Property(t => t.End).HasColumnName("EndsAt");
            slot.HasIndex(t => t.Start);
        });
        builder.Navigation(s => s.Slot).IsRequired();

        builder.OwnsMany(s => s.Bookings, booking =>
        {
            booking.ToTable("Bookings");
            booking.WithOwner().HasForeignKey("TrainingSessionId");
            booking.HasKey(b => b.Id);
            booking.Property(b => b.Id).ValueGeneratedNever();
            booking.HasOne<Client>().WithMany().HasForeignKey(b => b.ClientId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(s => s.Bookings).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Repositories/TrainingSessionRepository.cs`:

```csharp
using FitnessClub.Domain.SharedKernel;
using FitnessClub.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class TrainingSessionRepository(FitnessClubDbContext db)
    : Repository<TrainingSession>(db), ITrainingSessionRepository
{
    public Task<bool> TrainerHasSessionDuringAsync(Guid trainerId, TimeSlot slot, CancellationToken cancellationToken) =>
        Scheduled().AnyAsync(
            s => s.TrainerId == trainerId && s.Slot.Start < slot.End && slot.Start < s.Slot.End,
            cancellationToken);

    public Task<bool> RoomIsBookedDuringAsync(Guid roomId, TimeSlot slot, CancellationToken cancellationToken) =>
        Scheduled().AnyAsync(
            s => s.RoomId == roomId && s.Slot.Start < slot.End && slot.Start < s.Slot.End,
            cancellationToken);

    public Task<bool> ClientHasBookingDuringAsync(Guid clientId, TimeSlot slot, CancellationToken cancellationToken) =>
        Scheduled().AnyAsync(
            s => s.Slot.Start < slot.End && slot.Start < s.Slot.End
                && s.Bookings.Any(b => b.ClientId == clientId && b.CancelledAt == null),
            cancellationToken);

    public async Task<IReadOnlyList<TrainingSession>> ListStartingBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await Set
            .Where(s => s.Slot.Start >= from && s.Slot.Start < to)
            .OrderBy(s => s.Slot.Start)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrainingSession>> ListForTrainerStartingBetweenAsync(
        Guid trainerId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        await Set
            .Where(s => s.TrainerId == trainerId && s.Slot.Start >= from && s.Slot.Start < to)
            .OrderBy(s => s.Slot.Start)
            .ToListAsync(cancellationToken);

    private IQueryable<TrainingSession> Scheduled() => Set.Where(s => s.Status == SessionStatus.Scheduled);
}
```

- [ ] **Step 5: Implement**

Expose the aggregate and register its repository:

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
using FitnessClub.Domain.Trainers;
````

with:

````csharp
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
````

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
    public DbSet<Room> Rooms => Set<Room>();
````

with:

````csharp
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
using FitnessClub.Domain.Trainers;
````

with:

````csharp
using FitnessClub.Domain.Trainers;
using FitnessClub.Domain.Training;
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
        services.AddScoped<IRoomRepository, RoomRepository>();
````

with:

````csharp
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<ITrainingSessionRepository, TrainingSessionRepository>();
````

- [ ] **Step 6: Run and confirm it passes**

Run from `api/`:

```sh
dotnet test
```

Expected: PASS: 170 tests.

- [ ] **Step 7: Commit**

From the repo root:

```sh
git add api/src api/tests
git commit -m "feat(api): training session aggregate with bookings and scheduler"
```


### Task 8: Notification aggregate

**Files:**
- Create: `api/src/FitnessClub.Domain/Notifications/Notification.cs`, `NotificationType.cs`, `NotificationChannel.cs`, `NotificationStatus.cs`, `INotificationRepository.cs`
- Create: `api/src/FitnessClub.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`, `Repositories/NotificationRepository.cs`; Modify: `FitnessClubDbContext.cs`, `DependencyInjection.cs`
- Test: `api/tests/FitnessClub.UnitTests/Domain/NotificationTests.cs`, `api/tests/FitnessClub.IntegrationTests/Persistence/NotificationRepositoryTests.cs`

**Interfaces:**
- Consumes: `Client.NeedsExpiryNotice`, `Client.Owns`, `Membership`.
- Produces `Notification : AggregateRoot`: `RecipientMaxLength = 254`, `MessageMaxLength = 1000`, `FailureReasonMaxLength = 500`; `ClientId`, `MembershipId?`, `Type` (`MembershipExpiring`), `Channel` (`Email | Sms`), `Recipient`, `Message`, `Status` (`Pending | Sent | Failed`), `CreatedAt`, `SentAt?`, `FailureReason?`; `static MembershipExpiring(Client, Membership, DateTimeOffset now)`; `MarkSent(DateTimeOffset now)`; `MarkFailed(string reason)`; `Retry()`.
- Produces `INotificationRepository`: `ExistsForMembershipAsync(Guid membershipId, NotificationType, ct)`, `ListPendingAsync(ct)`.

- [ ] **Step 1: Write the failing tests**

`api/tests/FitnessClub.UnitTests/Domain/NotificationTests.cs`:

```csharp
using FitnessClub.Domain.Common;
using FitnessClub.Domain.Notifications;

namespace FitnessClub.UnitTests.Domain;

public class NotificationTests
{
    [Fact]
    public void MembershipExpiring_uses_email_when_client_has_one()
    {
        var client = TestData.Client(email: "olena@example.com");
        var membership = TestData.Buy(client, TestData.Plan(validityDays: 30));

        var notification = Notification.MembershipExpiring(client, membership, TestData.Now);

        Assert.Equal(NotificationChannel.Email, notification.Channel);
        Assert.Equal("olena@example.com", notification.Recipient);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Equal(membership.Id, notification.MembershipId);
        Assert.Contains("2026-11-03", notification.Message);
    }

    [Fact]
    public void MembershipExpiring_falls_back_to_sms()
    {
        var client = TestData.ClientWithMembership();

        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Now);

        Assert.Equal(NotificationChannel.Sms, notification.Channel);
        Assert.Equal(client.Phone.Value, notification.Recipient);
    }

    [Fact]
    public void MembershipExpiring_for_cancelled_membership_throws()
    {
        var client = TestData.ClientWithMembership();
        var membership = client.Memberships.Single();
        client.CancelMembership(membership.Id, TestData.Now);

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(client, membership, TestData.Now));
    }

    [Fact]
    public void MembershipExpiring_after_renewal_throws()
    {
        var client = TestData.ClientWithMembership(validityDays: 30);
        var current = client.Memberships.Single();
        TestData.Buy(client, TestData.Plan(), TestData.Today.AddDays(30));

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(client, current, TestData.Now));
    }

    [Fact]
    public void MembershipExpiring_for_another_clients_membership_throws()
    {
        var owner = TestData.ClientWithMembership();

        Assert.Throws<DomainException>(() => Notification.MembershipExpiring(TestData.Client(), owner.Memberships.Single(), TestData.Now));
    }

    [Fact]
    public void MarkSent_records_time_and_cannot_be_repeated()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Now);

        notification.MarkSent(TestData.Now.AddMinutes(1));

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Equal(TestData.Now.AddMinutes(1), notification.SentAt);
        Assert.Throws<DomainException>(() => notification.MarkFailed("late failure"));
    }

    [Fact]
    public void MarkFailed_truncates_long_reason()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Now);

        notification.MarkFailed(new string('x', Notification.FailureReasonMaxLength + 50));

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal(Notification.FailureReasonMaxLength, notification.FailureReason!.Length);
    }

    [Fact]
    public void Retry_returns_failed_notification_to_pending()
    {
        var client = TestData.ClientWithMembership();
        var notification = Notification.MembershipExpiring(client, client.Memberships.Single(), TestData.Now);
        Assert.Throws<DomainException>(() => notification.Retry());
        notification.MarkFailed("SMS gateway timeout");

        notification.Retry();

        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Null(notification.FailureReason);
    }
}
```

`api/tests/FitnessClub.IntegrationTests/Persistence/NotificationRepositoryTests.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Notifications;
using FitnessClub.Domain.Payments;
using FitnessClub.Domain.SharedKernel;
using FitnessClub.IntegrationTests.Infrastructure;

namespace FitnessClub.IntegrationTests.Persistence;

public class NotificationRepositoryTests(FitnessClubApiFactory factory) : PersistenceTestBase(factory)
{
    [Fact]
    public async Task Notification_round_trips_and_is_found_by_membership()
    {
        var client = Client.Register(PersonName.Create("Olena", "Shevchenko", null), new DateOnly(1995, 3, 14), PhoneNumber.Create(UniquePhone()), null, Now);
        var payment = client.PurchaseMembership(MembershipPlan.Create($"Plan {Guid.NewGuid():N}", Money.Of(800m), 30, null), Today, PaymentMethod.Cash, Now);
        var membership = client.Memberships.Single(m => m.Id == payment.MembershipId);
        var notification = Notification.MembershipExpiring(client, membership, Now);
        await SaveAsync<IClientRepository>(clients => clients.Add(client));
        await SaveAsync<INotificationRepository>(notifications => notifications.Add(notification));

        Assert.True(await ReadAsync<INotificationRepository, bool>(
            n => n.ExistsForMembershipAsync(membership.Id, NotificationType.MembershipExpiring, Ct)));
        Assert.Contains(await ReadAsync<INotificationRepository, IReadOnlyList<Notification>>(n => n.ListPendingAsync(Ct)), n => n.Id == notification.Id);

        await ChangeAsync<INotificationRepository>(async notifications =>
            (await notifications.GetByIdAsync(notification.Id, Ct))!.MarkSent(Now.AddMinutes(1)));

        var loaded = await ReadAsync<INotificationRepository, Notification?>(n => n.GetByIdAsync(notification.Id, Ct));
        Assert.Equal(NotificationStatus.Sent, loaded!.Status);
        Assert.Equal(NotificationChannel.Sms, loaded.Channel);
        Assert.DoesNotContain(await ReadAsync<INotificationRepository, IReadOnlyList<Notification>>(n => n.ListPendingAsync(Ct)), n => n.Id == notification.Id);
    }
}
```

- [ ] **Step 2: Run and confirm it fails**

Run from `api/`:

```sh
dotnet test --project tests/FitnessClub.UnitTests --filter-class "FitnessClub.UnitTests.Domain.NotificationTests"
```

Expected: FAIL: build error `CS0234: The type or namespace name 'Notifications' does not exist in the namespace 'FitnessClub.Domain'`.

- [ ] **Step 3: Implement**

`api/src/FitnessClub.Domain/Notifications/NotificationType.cs`:

```csharp
namespace FitnessClub.Domain.Notifications;

public enum NotificationType
{
    MembershipExpiring,
}
```

`api/src/FitnessClub.Domain/Notifications/NotificationChannel.cs`:

```csharp
namespace FitnessClub.Domain.Notifications;

public enum NotificationChannel
{
    Email,
    Sms,
}
```

`api/src/FitnessClub.Domain/Notifications/NotificationStatus.cs`:

```csharp
namespace FitnessClub.Domain.Notifications;

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
}
```

`api/src/FitnessClub.Domain/Notifications/Notification.cs`:

```csharp
using System.Globalization;
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Notifications;

public sealed class Notification : AggregateRoot
{
    public const int RecipientMaxLength = 254;
    public const int MessageMaxLength = 1000;
    public const int FailureReasonMaxLength = 500;

    public Guid ClientId { get; private set; }
    public Guid? MembershipId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public NotificationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? FailureReason { get; private set; }

    private Notification()
    {
    }

    public static Notification MembershipExpiring(Client client, Membership membership, DateTimeOffset now)
    {
        if (!client.Owns(membership))
            throw new DomainException("The membership does not belong to the client.");

        if (!client.NeedsExpiryNotice(membership, now.ToDateOnly()))
            throw new DomainException("The membership does not need an expiry notice.");

        var endsOn = membership.EndsOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new Notification
        {
            ClientId = client.Id,
            MembershipId = membership.Id,
            Type = NotificationType.MembershipExpiring,
            Channel = client.Email is null ? NotificationChannel.Sms : NotificationChannel.Email,
            Recipient = client.Email?.Value ?? client.Phone.Value,
            Message = $"Dear {client.Name.FirstName}, your membership '{membership.PlanName}' expires on {endsOn}.",
            Status = NotificationStatus.Pending,
            CreatedAt = now,
        };
    }

    public void MarkSent(DateTimeOffset now)
    {
        EnsurePending();
        Status = NotificationStatus.Sent;
        SentAt = now;
    }

    public void MarkFailed(string reason)
    {
        EnsurePending();

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A failure reason is required.");

        var trimmed = reason.Trim();
        Status = NotificationStatus.Failed;
        FailureReason = trimmed.Length > FailureReasonMaxLength ? trimmed[..FailureReasonMaxLength] : trimmed;
    }

    public void Retry()
    {
        if (Status != NotificationStatus.Failed)
            throw new DomainException("Only a failed notification can be retried.");

        Status = NotificationStatus.Pending;
        FailureReason = null;
    }

    private void EnsurePending()
    {
        if (Status != NotificationStatus.Pending)
            throw new DomainException("The notification has already been processed.");
    }
}
```

`api/src/FitnessClub.Domain/Notifications/INotificationRepository.cs`:

```csharp
using FitnessClub.Domain.Common;

namespace FitnessClub.Domain.Notifications;

public interface INotificationRepository : IRepository<Notification>
{
    Task<bool> ExistsForMembershipAsync(Guid membershipId, NotificationType type, CancellationToken cancellationToken);

    Task<IReadOnlyList<Notification>> ListPendingAsync(CancellationToken cancellationToken);
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`:

```csharp
using FitnessClub.Domain.Clients;
using FitnessClub.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitnessClub.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(10);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(n => n.Recipient).HasMaxLength(Notification.RecipientMaxLength).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(Notification.MessageMaxLength).IsRequired();
        builder.Property(n => n.FailureReason).HasMaxLength(Notification.FailureReasonMaxLength);
        builder.HasOne<Client>().WithMany().HasForeignKey(n => n.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => new { n.MembershipId, n.Type }).IsUnique().HasFilter("[MembershipId] IS NOT NULL");
        builder.HasIndex(n => new { n.Status, n.CreatedAt });
    }
}
```

`api/src/FitnessClub.Infrastructure/Persistence/Repositories/NotificationRepository.cs`:

```csharp
using FitnessClub.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace FitnessClub.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository(FitnessClubDbContext db) : Repository<Notification>(db), INotificationRepository
{
    public Task<bool> ExistsForMembershipAsync(Guid membershipId, NotificationType type, CancellationToken cancellationToken) =>
        Set.AnyAsync(n => n.MembershipId == membershipId && n.Type == type, cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListPendingAsync(CancellationToken cancellationToken) =>
        await Set.Where(n => n.Status == NotificationStatus.Pending).OrderBy(n => n.CreatedAt).ToListAsync(cancellationToken);
}
```

- [ ] **Step 4: Implement**

Expose the aggregate and register its repository:

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
using FitnessClub.Domain.MembershipPlans;
````

with:

````csharp
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Notifications;
````

In `api/src/FitnessClub.Infrastructure/Persistence/FitnessClubDbContext.cs`, replace:

````csharp
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
````

with:

````csharp
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<Notification> Notifications => Set<Notification>();
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
using FitnessClub.Domain.MembershipPlans;
````

with:

````csharp
using FitnessClub.Domain.MembershipPlans;
using FitnessClub.Domain.Notifications;
````

In `api/src/FitnessClub.Infrastructure/DependencyInjection.cs`, replace:

````csharp
        services.AddScoped<ITrainingSessionRepository, TrainingSessionRepository>();
````

with:

````csharp
        services.AddScoped<ITrainingSessionRepository, TrainingSessionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
````

- [ ] **Step 5: Run and confirm it passes**

Run from `api/`:

```sh
dotnet test
```

Expected: PASS: 179 tests.

- [ ] **Step 6: Commit**

From the repo root:

```sh
git add api/src api/tests
git commit -m "feat(api): notification aggregate"
```


### Task 9: Verify the boundaries with four parallel review agents

**Files:**
- No planned file changes. Fixes for confirmed findings go in the files the findings name.

**Interfaces:**
- Consumes: the whole branch after Task 8.
- Produces: a clean review from all four agents, or fixes for each confirmed finding with a test for each one.

- [ ] **Step 1: Run and confirm it passes**

Run from `api/`:

```sh
dotnet build && dotnet test
```

Expected: PASS: build with 0 warnings, then 179 tests.

- [ ] **Step 2: Dispatch the four boundary reviewers**

Dispatch four review agents **in parallel, in one message** (use superpowers:dispatching-parallel-agents), one per layer. Use these prompts verbatim:

1. **Domain:** Read-only Clean Architecture and DDD boundary review of the Domain layer. Do not edit files. Repository: the current worktree; the spec is `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`. Scope: `api/src/FitnessClub.Domain` (every file) and `api/tests/FitnessClub.UnitTests/Domain`. Check: Domain purity (BCL only); each aggregate root guards its invariants; children and value objects cannot be changed from outside the root; other aggregates referenced by id only; missing or wrong invariants for the requirements in `docs/Requirements/Fitness Club System.md` (give a concrete failing scenario); every requirement and report can be served by the model; repository interfaces only for roots and free of persistence types. Report each finding as file:line, the violated rule, a concrete failing scenario and the smallest fix, ranked by severity, and label each "real defect" or "design opinion". Under 350 words.
2. **Application:** Read-only Clean Architecture and DDD boundary review of the Application layer. Do not edit files. Repository: the current worktree; the spec is `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`. Scope: `api/src/FitnessClub.Application` (every file and the csproj), `api/tests/FitnessClub.UnitTests/Application` and `Fakes`, and how `api/src/FitnessClub.Api/Controllers` use Application. Check: No EF, `IQueryable`, `DbSet` or HTTP types; exactly one `IUnitOfWork.SaveChangesAsync` per use case and none on failure paths; no domain rule implemented in a service; no Domain type in a request or response record; fakes behave like the real repositories. Report each finding as file:line, the violated rule, a concrete failing scenario and the smallest fix, ranked by severity, and label each "real defect" or "design opinion". Under 350 words.
3. **Infrastructure:** Read-only Clean Architecture and DDD boundary review of the Infrastructure layer. Do not edit files. Repository: the current worktree; the spec is `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`. Scope: `api/src/FitnessClub.Infrastructure` (every file) and `api/tests/FitnessClub.IntegrationTests/Persistence`. Check: Repositories are internal, implement Domain interfaces, return whole aggregates and never save; `UnitOfWork` is the only caller of `SaveChanges`; only roots have `DbSet`s; owned-type mappings, converters and indexes are valid for SQL Server; the `Version` stamping covers every change to an owned child; only `DependencyInjection` is public. Report each finding as file:line, the violated rule, a concrete failing scenario and the smallest fix, ranked by severity, and label each "real defect" or "design opinion". Under 350 words.
4. **Api and tests:** Read-only Clean Architecture and DDD boundary review of the Api and tests layer. Do not edit files. Repository: the current worktree; the spec is `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`. Scope: `api/src/FitnessClub.Api`, every `*.csproj`, `api/FitnessClub.slnx`, `api/Directory.Packages.props`, `api/tests/FitnessClub.ArchitectureTests`. Check: Api uses Infrastructure only in `Program` and Domain only in `ExceptionToProblemDetailsHandler`; controllers inject only Application `I*Service`; the project reference graph follows Domain ← Application ← Infrastructure ← Api (UnitTests reference only Domain and Application); architecture rules have no loopholes; no C# comments anywhere. Report each finding as file:line, the violated rule, a concrete failing scenario and the smallest fix, ranked by severity, and label each "real defect" or "design opinion". Under 350 words.

- [ ] **Step 3: Triage the findings and fix them test-first**

Triage the four reports together. For every **real defect** that is inside this plan's scope (see the spec's "Out of scope"):
1. Write a failing test that shows it: an architecture rule in `api/tests/FitnessClub.ArchitectureTests` for a boundary violation, a unit test for a domain rule, or a persistence test for a mapping problem.
2. Run it and see it fail.
3. Fix the code.
4. Run `dotnet test` and see everything pass.
5. Commit as `fix(api): <finding>`.

Put design opinions and out-of-scope defects in the spec's "Out of scope" list with one line each, rather than acting on them. If a finding contradicts the spec, stop and ask your human partner.

- [ ] **Step 4: Do not re-raise these (already fixed)**

These findings were already raised and fixed while this plan was written, so they should not come back: payments not tied to purchases, repeat check-ins using up visits, expiry notices for renewed memberships, no retry for failed notices, double-booking a client into overlapping sessions, bookings surviving a cancelled session, lost updates without concurrency tokens, update mutating before the uniqueness check, public `FitnessClubDbContext`, Hangfire in `Program`, controllers able to inject repositories, `IQueryable` not forbidden, and regex-based comment detection.

- [ ] **Step 5: Run and confirm it passes**

Run from `api/`:

```sh
docker compose -f ../deploy/docker-compose.yml up --build -d && sleep 15 && curl -fsS http://localhost:8080/health && docker compose -f ../deploy/docker-compose.yml down
```

Expected: PASS: prints `Healthy` (needs `deploy/.env` with Auth0 values; see `deploy/CLAUDE.md`).


### Task 10: Update the documentation

**Files:**
- Modify: `api/CLAUDE.md`, `CLAUDE.md`, `docs/Code/Specs/2026-10-05-api-foundation-design.md`, `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`

**Interfaces:**
- Consumes: everything above.
- Produces: docs that match the code, so sub-projects 2–5 start from the new conventions.

- [ ] **Step 1: Edit the docs**

Apply these replacements exactly:

In `api/CLAUDE.md`, replace:

````markdown
The design is in `docs/Code/Specs/2026-10-05-api-foundation-design.md`.
````

with:

````markdown
The designs are in `docs/Code/Specs/2026-10-05-api-foundation-design.md` and `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`.
````

In `api/CLAUDE.md`, replace:

````markdown
- .NET 10, ASP.NET Core controllers, Clean Architecture
````

with:

````markdown
- .NET 10, ASP.NET Core controllers, Clean Architecture with DDD aggregates, repositories and a unit of work
````

In `api/CLAUDE.md`, replace:

````markdown
- xUnit v3 tests on Microsoft.Testing.Platform
````

with:

````markdown
- xUnit v3 tests on Microsoft.Testing.Platform
- Architecture rules: NetArchTest + Roslyn in `tests/FitnessClub.ArchitectureTests`
````

In `api/CLAUDE.md`, replace:

````markdown
Package versions live only in `Directory.Packages.props`. Never put a `Version` on a `PackageReference`.
````

with:

````markdown
Package versions live only in `Directory.Packages.props`. Never put a `Version` on a `PackageReference`. `Newtonsoft.Json` is pinned to 13.0.4 because Hangfire.Core would otherwise pull a vulnerable 11.x (NU1903).
````

In `api/CLAUDE.md`, replace:

````markdown
dotnet test --project tests/FitnessClub.UnitTests             # one project
````

with:

````markdown
dotnet test --project tests/FitnessClub.UnitTests             # one project
dotnet test --project tests/FitnessClub.ArchitectureTests     # layer, boundary and DDD rules
````

In `api/CLAUDE.md`, replace:

````markdown
src/FitnessClub.Domain          entities + rules (DomainException); no package references
src/FitnessClub.Application     services, request/response records, IApplicationDbContext, Roles
src/FitnessClub.Infrastructure  EF Core DbContext + configurations, Hangfire, RecurringJobs
src/FitnessClub.Api             controllers, Auth0 setup, exception → problem details, Program.cs
tests/FitnessClub.UnitTests     Domain + Application (services against the InMemory provider)
tests/FitnessClub.IntegrationTests  HTTP tests via FitnessClubApiFactory
````

with:

````markdown
src/FitnessClub.Domain          aggregates, value objects (SharedKernel), domain services, repository interfaces; BCL only
src/FitnessClub.Application     I*Service + internal services, request/response records, IUnitOfWork, Roles
src/FitnessClub.Infrastructure  internal DbContext, configurations, repositories, UnitOfWork, Hangfire
src/FitnessClub.Api             controllers, Auth0 setup, exception → problem details, Program.cs
tests/FitnessClub.UnitTests     Domain rules + Application services against fakes (no Infrastructure)
tests/FitnessClub.IntegrationTests  HTTP tests and repository round-trips via FitnessClubApiFactory
tests/FitnessClub.ArchitectureTests layer, boundary, DDD and no-comment rules
````

In `api/CLAUDE.md`, replace:

````markdown
Which project may reference which: Domain ← Application ← Infrastructure ← Api. Application may use EF Core's base package for `DbSet<T>`, but never a database-specific provider.
````

with:

````markdown
Which project may reference which: Domain ← Application ← Infrastructure ← Api. Domain uses only the BCL. Application uses only Domain and DI abstractions, never EF Core or `IQueryable`. Api uses Infrastructure only in `Program` and Domain only to map `DomainException`. `FitnessClub.ArchitectureTests` enforces all of this.
````

In `api/CLAUDE.md`, replace:

````markdown
  1. Entity in Domain.
  2. `DbSet` on `IApplicationDbContext` and `FitnessClubDbContext`, plus an `IEntityTypeConfiguration`.
  3. Service in Application, registered in `AddApplication()`.
  4. Controller in Api.
````

with:

````markdown
  1. Aggregate root (sealed, private setters, factory method) and `I{Root}Repository` in Domain. Child entities and value objects stay inside the aggregate.
  2. `DbSet` on `FitnessClubDbContext`, an `IEntityTypeConfiguration` (owned types for children), and an internal `{Root}Repository : Repository<{Root}>` registered in `AddInfrastructure()`.
  3. `I{Name}Service` plus an internal `{Name}Service` in Application, registered in `AddApplication()`. Load aggregates through repositories, call domain methods, then call `IUnitOfWork.SaveChangesAsync` once.
  4. A controller in Api that injects only the `I{Name}Service`.
- **No comments** in C# code. `SourceCodeTests` fails on any `//`, `/* */` or `///`.
- **Concurrency:** every aggregate root has a shadow `Version` token, re-stamped when the root or an owned child changes. A stale save becomes `ConflictException` → 409.
````

In `api/CLAUDE.md`, replace:

````markdown
- **Time:** use the injected `TimeProvider`, never `DateTime.UtcNow`.
````

with:

````markdown
- **Time:** use the injected `TimeProvider`, never `DateTime.UtcNow`. Domain methods take `DateTimeOffset now` and read dates in its offset, so pass club-local time.
````

In `api/CLAUDE.md`, replace:

````markdown
- **InMemory limits:** it doesn't enforce unique indexes or relationships.
````

with:

````markdown
- **InMemory limits:** it doesn't enforce unique indexes or relationships, and it has no transactions.
````

In `api/CLAUDE.md`, replace:

````markdown
- **Recurring jobs** are registered only in `Infrastructure/BackgroundJobs/RecurringJobs.Register`.
````

with:

````markdown
- **Recurring jobs** are registered only in `Infrastructure/BackgroundJobs/RecurringJobs.Register`, which `UseInfrastructureAsync` calls at startup.
````

In `api/CLAUDE.md`, replace:

````markdown
  - Each test class gets its own InMemory database, but tests in the same class share it, so use unique names.
````

with:

````markdown
  - Each test class gets its own InMemory database, but tests in the same class share it, so use unique names.
  - Repository tests derive from `PersistenceTestBase`. Each of its helpers runs in its own DI scope.
````

In `api/CLAUDE.md`, replace:

````markdown
At startup, `InitializeDatabaseAsync()` runs `Database.MigrateAsync()`.
````

with:

````markdown
At startup, `UseInfrastructureAsync()` runs `Database.MigrateAsync()`.
````

In `CLAUDE.md`, replace:

````markdown
- **Backend (`api/`):** foundation built, with membership plans as the first feature. Stack and commands are in `api/CLAUDE.md`. Next backend parts: clients/memberships/visits, trainers/bookings, expiry notifications, reports.
````

with:

````markdown
- **Backend (`api/`):** foundation built, and the whole domain model is in place: DDD aggregates, repositories and a unit of work behind enforced Clean Architecture boundaries. Membership plans are exposed end to end. Stack and commands are in `api/CLAUDE.md`. Next backend parts: use cases and endpoints for clients/memberships/visits, trainers/rooms/bookings, expiry notifications, reports.
````

In `docs/Code/Specs/2026-10-05-api-foundation-design.md`, replace:

````markdown
Not used, on purpose (YAGNI): MediatR/CQRS, AutoMapper, FluentValidation, the repository pattern.
````

with:

````markdown
Not used, on purpose (YAGNI): MediatR/CQRS, AutoMapper, FluentValidation. The repository pattern, the unit of work and the full domain model came later in [[2026-10-05-domain-model-and-architecture-design]], which replaces this spec's `IApplicationDbContext` and layering rules.
````

In `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`, replace:

````markdown
status: approved
````

with:

````markdown
status: implemented
````

In `docs/Code/Specs/2026-10-05-domain-model-and-architecture-design.md`, replace:

````markdown
| 1b | **Domain model and architecture** (this spec) | approved |
````

with:

````markdown
| 1b | **Domain model and architecture** (this spec) | implemented |
````

- [ ] **Step 2: Run and confirm it passes**

Run from `api/`:

```sh
grep -c -e IApplicationDbContext -e InitializeDatabaseAsync CLAUDE.md || true
```

Expected: PASS: prints `0` (no stale references left in `api/CLAUDE.md`).

- [ ] **Step 3: Commit**

From the repo root:

```sh
git add CLAUDE.md api/CLAUDE.md docs/Code/Specs
git commit -m "docs: domain model, repositories and architecture rules"
```
