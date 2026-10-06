---
tags: [spec, api, ddd]
status: implemented
date: 2026-10-05
---

# Domain Model and Clean Architecture: Design Spec

Requirements source: [[Fitness Club System]]. This spec replaces the persistence and layering rules of [[2026-10-05-api-foundation-design]]: the "no repository pattern" and "`IApplicationDbContext` in Application" decisions are withdrawn. Everything else in the foundation spec still holds.

| # | Sub-project | Status |
|---|---|---|
| 1 | Foundation | implemented |
| 1b | **Domain model and architecture** (this spec) | implemented |
| 2 | Clients, memberships, visits (use cases + endpoints) | not started |
| 3 | Trainers, schedules, rooms, bookings (use cases + endpoints) | not started |
| 4 | Membership expiry notifications (Hangfire job) | not started |
| 5 | Reports | not started |

## Goal

Model the whole fitness club domain with DDD aggregates, then enforce Clean Architecture boundaries with repositories, a unit of work and interfaces between every layer. Sub-projects 2–5 then only add use cases and endpoints on top of a finished, tested domain.

**Done when:**
- Every aggregate below exists in Domain with its rules, unit-tested.
- Every aggregate root has a repository interface in Domain and an internal EF implementation in Infrastructure, round-trip tested.
- Application has no EF Core reference. It persists only through repositories and `IUnitOfWork`.
- Membership plans keep working end to end with the same HTTP contract.
- `tests/FitnessClub.ArchitectureTests` enforces the layer and modelling rules below, and four parallel boundary reviews (Domain, Application, Infrastructure, Api) report no violations.
- Two users saving the same aggregate at the same time get a 409 instead of a lost update.
- No comments in any `.cs` file.

## Layers and allowed dependencies

```
Domain  ←  Application  ←  Infrastructure  ←  Api (composition root)
```

| Project | May reference | Owns |
|---|---|---|
| Domain | BCL only | Aggregates, entities, value objects, domain services, repository interfaces, `DomainException` |
| Application | Domain, `Microsoft.Extensions.DependencyInjection.Abstractions` | Use-case service interfaces + internal implementations, request/response records, `IUnitOfWork`, `NotFoundException`, `ConflictException`, `Roles` |
| Infrastructure | Application (and Domain through it), EF Core, Hangfire | Internal `FitnessClubDbContext`, entity configurations, repositories, `UnitOfWork`, recurring jobs. Public surface: `DependencyInjection.AddInfrastructure` and `UseInfrastructureAsync` only |
| Api | Application; Infrastructure only in `Program`; Domain only in `ExceptionToProblemDetailsHandler` (to map `DomainException`) | Controllers, auth, problem details |

Rules (enforced by `tests/FitnessClub.ArchitectureTests`, using reflection, NetArchTest for type-level dependencies and Roslyn for comments):
- Domain references only the BCL, and neither Domain nor Application references `System.Linq.Queryable` or `System.Linq.Expressions` (no `IQueryable` leaks).
- Application references only Domain and DI abstractions. No type in it depends on Infrastructure, Api, ASP.NET Core or EF Core.
- Api references neither EF Core nor Hangfire. Only `Program` uses Infrastructure, and only `ExceptionToProblemDetailsHandler` uses Domain.
- Controllers inject only Application `I*Service` interfaces, through the constructor or `[FromServices]`.
- Application services are `internal sealed` and exposed through a public `I{Name}Service`.
- Infrastructure has exactly one public type, `DependencyInjection`.
- `FitnessClubDbContext` has `DbSet`s only for aggregate roots.
- Repositories exist only for aggregate roots. Every aggregate root has `I{Root}Repository` in Domain and a non-public implementation in Infrastructure.
- Domain entities and value objects are sealed, with no public setters, public fields or public constructors.
- No comments (`//`, `/* */`, `///`) in any `.cs` file in `src/` or `tests/`.

## Building blocks

- `Entity`: `Guid Id`, generated on creation.
- `AggregateRoot : Entity`: the only type a repository may load or save.
- `IRepository<TAggregate> where TAggregate : AggregateRoot`: `GetByIdAsync`, `Add`. Repositories never save.
- `IUnitOfWork.SaveChangesAsync`: called exactly once per use case, after all aggregates are changed. One unit of work may save several aggregates, for example a check-in changes a `Client` and adds a `Visit`.
- Value objects are `sealed record`s with a private constructor and a static `Create`/`Of` factory that validates.
- Domain services live next to their aggregate, behind an interface (`ISessionScheduler`), and depend only on Domain repository interfaces.
- **Optimistic concurrency:** every aggregate root has a shadow `Version` (`Guid`) concurrency token. On save, the DbContext stamps a new version on every root that changed itself or has an owned child that was added, changed or removed. `UnitOfWork` turns `DbUpdateConcurrencyException` into `ConflictException` (409).
- **Time:** domain methods take `DateTimeOffset now`. Calendar dates and times of day are read in the offset of the value passed in, so callers (sub-projects 2–4) must pass club-local times. Use `TimeProvider.GetLocalNow()` with the container's `TZ` set to the club's zone, and convert any incoming slot to club-local time before calling the domain.

Domain events and strongly typed ids are out of scope.

## Shared kernel (value objects)

| Value object | Rules | Persistence |
|---|---|---|
| `Money` | Amount ≥ 0, at most 2 decimals. Single club currency | converter → `decimal(18,2)` |
| `PhoneNumber` | Digits, spaces, `-`, `(`, `)` and an optional leading `+`. 10–15 digits. Stored as `+digits` or `digits` | converter → `nvarchar(16)` |
| `EmailAddress` | Valid address without a display name. The host has a dot. ≤ 254 chars. Lowercased | converter → `nvarchar(254)` |
| `PersonName` | First and last name required, middle name optional, each ≤ 100 chars, trimmed. `FullName` = "Last First Middle" | owned → `FirstName`, `LastName`, `MiddleName` columns |
| `TimeSlot` | `End > Start`. `Overlaps` treats touching edges as not overlapping | owned → `StartsAt`, `EndsAt` columns |

## Aggregates

### MembershipPlan (`Domain/MembershipPlans`)

Unchanged rules from the foundation spec. `Price` becomes `Money` and must be > 0. Repository: `ListAsync(includeInactive)`, `NameExistsAsync(name, excludeId)`.

### Client (`Domain/Clients`) with child entity Membership

| Field | Rule |
|---|---|
| `Name` | `PersonName` |
| `DateOfBirth` | not in the future, age ≤ 120. `AgeOn(date)` computes age (the requirement's "age") |
| `Phone` | `PhoneNumber`, unique among clients |
| `Email` | optional `EmailAddress`, used for expiry notices |
| `RegisteredAt` | set on `Register` |
| `Memberships` | owned child entities |

`Membership` is a snapshot of the plan at sale time: `PlanId`, `PlanName`, `Price`, `StartsOn`, `EndsOn = StartsOn + ValidityDays − 1`, `VisitLimit`, `VisitsUsed`, `PurchasedAt`, `CancelledAt`.

Behaviour:
- `PurchaseMembership(plan, startsOn, method, now)` → `Payment`: the plan must be active and `startsOn` ≥ today. The new period must not overlap a membership that is not cancelled and still has visits. Buying the next one in advance is allowed, and so is buying a new single visit after the current one is used up. The sale and its payment are created together, so a membership never exists without exactly one payment.
- `ActiveMembershipOn(date)`: not cancelled, has visits left, date within `StartsOn..EndsOn`.
- `CheckIn(now)` → `Visit`: needs an active membership today and allows one check-in per membership per day (`Membership.LastVisitOn`), then uses one visit. A second scan on the same day is rejected and doesn't use up a visit.
- `CancelMembership(id, now)`: only the client's own membership. It can't be cancelled twice or after it has ended.
- `Owns(membership)`: guards other aggregates that take a membership.
- `NeedsExpiryNotice(membership, today)`: owned, not cancelled, visits left, not yet ended, and the client hasn't already bought a later membership. `MembershipsNeedingExpiryNotice(today, endsBy)` lists them for the job.

Repository: `ListAsync`, `PhoneExistsAsync(phone, excludeId)`, `ListWithMembershipsEndingBetweenAsync(from, to)` (for the expiry job).

### Visit (`Domain/Visits`)

`ClientId`, `MembershipId`, `CheckedInAt`. Created only by `Client.CheckIn`. Repository: `ListForClientAsync(clientId)`.

### Payment (`Domain/Payments`)

`ClientId`, `MembershipId` (unique), `Amount` (`Money`), `Method` (`Cash`, `Card`), `PaidAt`. Created only inside `Client.PurchaseMembership`, with the amount taken from the membership's price. The application adds the returned payment to `IPaymentRepository` in the same unit of work as the client. Revenue reports sum payments. Repository: `ListPaidBetweenAsync(from, to)` (half-open range).

### Trainer (`Domain/Trainers`) with value objects WorkingHours and ClientAssignment

| Field | Rule |
|---|---|
| `Name`, `Phone` (unique), `Email` | as for clients |
| `Specialization` | required, ≤ 100 chars |
| `IdentityUserId` | optional Auth0 `sub`, ≤ 128 chars, unique when set. Lets a trainer see their own schedule |
| `IsActive` | inactive trainers keep history but take no clients or sessions |
| `WorkingHours` | weekly schedule: `(Day, Start, End)`, `End > Start`, no overlap on the same day, split shifts allowed |
| `Clients` | `ClientAssignment(ClientId, AssignedAt)`, no duplicates. This is the requirement's "client list" |

`IsWorkingDuring(slot)`: active, the slot is within one calendar day, and one shift covers the whole slot.

Repository: `ListAsync(includeInactive)`, `PhoneExistsAsync`, `GetByIdentityUserIdAsync`.

### Room (`Domain/Rooms`)

`Name` (unique, ≤ 100), `Capacity` 1–500, `IsActive`. Repository: `ListAsync(includeInactive)`, `NameExistsAsync`.

### TrainingSession (`Domain/Training`) with child entity Booking

Covers both group classes and individual sessions.

| Field | Rule |
|---|---|
| `Title` | required, ≤ 100 |
| `Type` | `Group` or `Individual` |
| `TrainerId`, `RoomId` | by id |
| `Slot` | `TimeSlot`, in the future, 15 min – 4 h |
| `Capacity` | 1..room capacity. `Individual` is exactly 1 |
| `Status` | `Scheduled` or `Cancelled` |
| `Bookings` | `Booking(ClientId, BookedAt, CancelledAt)` |

- Sessions are created and booked only through `ISessionScheduler` (`SessionScheduler`), because both need checks across aggregates:
  - `ScheduleAsync`: the trainer is active and working during the slot, the room is active, and there is no overlapping scheduled session for the same trainer or room. Cancelled sessions don't block.
  - `BookAsync(session, client, now)`: the client has no active booking in another overlapping scheduled session. Then the session checks that it is scheduled and hasn't started, the client has an active membership on the session date, isn't already booked, and there is a free place.
- `CancelBooking(clientId, now)` and `Cancel(now)`: only before the start. Cancelling a session also cancels its bookings.

Repository: `TrainerHasSessionDuringAsync`, `RoomIsBookedDuringAsync`, `ClientHasBookingDuringAsync`, `ListStartingBetweenAsync`, `ListForTrainerStartingBetweenAsync`.

### Notification (`Domain/Notifications`)

`ClientId`, `MembershipId`, `Type` (`MembershipExpiring`), `Channel` (`Email` when the client has one, else `Sms`), `Recipient`, `Message`, `Status` (`Pending` → `Sent` | `Failed`, and `Failed` → `Pending` through `Retry()`), `CreatedAt`, `SentAt`, `FailureReason` (≤ 500, truncated). `MembershipExpiring(client, membership, now)` refuses memberships for which `Client.NeedsExpiryNotice` is false (renewed, cancelled, ended or used up). Only one notice per membership and type (unique index plus `ExistsForMembershipAsync`), so the daily job is idempotent. Repository: `ExistsForMembershipAsync`, `ListPendingAsync`.

## Requirement coverage

| Requirement | Model |
|---|---|
| Client full name, age, phone | `Client.Name`, `AgeOn`, `Phone` |
| Client membership and its expiry | `Client.Memberships`, `ActiveMembershipOn`, `Membership.EndsOn` |
| Every visit recorded | `Client.CheckIn` → `Visit` |
| Trainer specialization, schedule, client list | `Trainer.Specialization`, `WorkingHours`, `Clients` |
| Group and individual sessions with sign-up | `TrainingSession` + `Booking` |
| Configurable plans (single, monthly, yearly, packs) | `MembershipPlan` |
| Automatic expiry notification | `ListWithMembershipsEndingBetweenAsync` + `Notification` (job in sub-project 4) |
| Report: clients with visit activity | `Client` + `Visit` |
| Report: revenue per month / year | `Payment.Amount`, `PaidAt` |
| Report: trainer and room load by day | `TrainingSession` by `TrainerId` / `RoomId`, `Slot`, bookings vs capacity |

Report queries are read models built in sub-project 5 behind an Application interface implemented in Infrastructure. They are not aggregates.

## Persistence

- Child entities and multi-field value objects are EF **owned types** (`OwnsMany`, `OwnsOne`), so loading a root always loads its whole aggregate. Single-value value objects use value converters.
- Tables: `MembershipPlans`, `Clients`, `Memberships`, `Visits`, `Payments`, `Trainers`, `TrainerWorkingHours`, `TrainerClients`, `Rooms`, `TrainingSessions`, `Bookings`, `Notifications`.
- References between aggregates are id columns with a `Restrict` foreign key. No navigation properties cross aggregates.
- Unique indexes: plan name, room name, client phone, trainer phone, trainer identity id (filtered), payment membership id, notification (membership, type) (filtered). InMemory doesn't enforce them, so services still check.
- Enums are stored as strings.
- SQL Server uses split queries, so trainers with two owned collections don't multiply rows.
- A save rejected by the concurrency check writes nothing on either provider. SQL Server rolls the whole save back in its transaction. InMemory has no transactions, so `FitnessClubDbContext` guards it instead: saves run one at a time behind a process-wide lock, and before writing, every modified or deleted aggregate root has its stored `Version` compared with the one it was loaded with. A missing row or a different version throws `DbUpdateConcurrencyException` before anything is written. The synchronous `SaveChanges` stamps versions and applies the same guard.

## Testing

- **Unit (Domain):** every rule above, plus `SessionScheduler` against an in-memory fake repository.
- **Unit (Application):** `MembershipPlanService` against fake repository and unit of work. Asserts that nothing is saved on failure.
- **Integration (Persistence):** each repository round-trips its aggregate through the real DI container and EF InMemory. This includes children added to an already loaded root, a check-in saving two aggregates in one unit of work, and stale copies (of the root, or with only a child changed) raising `ConflictException`. A rejected save leaves no rows behind: no second visit, membership, payment or booking.
- **Architecture:** the rules listed under Layers. Each was checked by temporarily breaking it: a public setter, a public field, a public constructor, a `record struct`, a trailing comment, a doc comment, `IQueryable` in Application, a controller injecting a repository, and a controller using Infrastructure.
- Existing HTTP tests stay green. The only contract change is the problem-details message for sub-cent prices: `Amount can have at most 2 decimal places.`

## Out of scope

- Use cases and endpoints for clients, trainers, rooms, sessions, payments and notifications (sub-projects 2–4).
- Report queries (sub-project 5).
- Domain events and an outbox, and strongly typed ids.
- Races between aggregates that a version token can't see: two admins scheduling the same trainer or room at once, and two requests booking one client into two different overlapping sessions. Admin scheduling is rare. Revisit with a serializable transaction or `sp_getapplock` when the endpoints exist.
- Mapping SQL Server unique-index violations (`DbUpdateException` 2601/2627) to 409. InMemory can't raise them, so this comes with the SQL Server switch.
- Club time zone configuration (`TZ` in compose, and conversion of incoming times).
- EF migrations (created when SQL Server is switched on).
- Phone numbers are stored as typed: `+380…` and `380…` are different values. Normalizing to E.164 needs a country default.
- Expiry notices can target a membership that hasn't started yet or an unused single-visit pass. The job (sub-project 4) narrows the window.
- Cancelling a membership doesn't refund its payment or cancel future bookings. Refunds come with the payment use cases.
- The price ceiling of 1,000,000 exists only as a request rule, not in `MembershipPlan`.
- Version stamping scans tracked entries for each changed owned child (O(n²)). That's fine at this aggregate size.
- The Hangfire dashboard stays local-only (Hangfire's `LocalRequestsOnly`) even though its endpoint is `AllowAnonymous`.
- The csproj reference graph is enforced by assembly references and NetArchTest, not by parsing `ProjectReference` items.
