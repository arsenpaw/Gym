---
tags: [spec, api, trainers]
status: implemented
date: 2026-10-07
---

# Trainers Endpoints: Design Spec

Requirements source: [[Fitness Club System]] ("each trainer has a profile: specialization, work schedule, client list"). Builds on the `Trainer` aggregate from [[2026-10-05-domain-model-and-architecture-design]]. Part of sub-project 3 (trainers, schedules, rooms, bookings). A trainer's own session schedule belongs to the sessions endpoints, not here.

## Layers

- **Application** `Application/Trainers/`: `ITrainerService` + internal `TrainerService`, request records `TrainerRequest`, `WorkingHoursRequest`, `LinkIdentityRequest`, response records `TrainerSummaryResponse`, `TrainerResponse` (with `WorkingHoursResponse`, `TrainerClientResponse`). Registered in `AddApplication()`.
- **Api** `TrainersController` at `api/trainers`, injects only `ITrainerService`.
- No Domain or repository changes. `TrainerService` uses `ITrainerRepository`, `IClientRepository`, `IUnitOfWork` and `TimeProvider`.

## Roles

Class level: `[Authorize(Roles = "Admin,Receptionist")]`. Every write adds `[Authorize(Roles = Admin)]`, so a Receptionist can only read. Trainers get 403 on all of these endpoints.

## Endpoints

| Method | Route | Roles | Success | Service call |
|---|---|---|---|---|
| GET | `/api/trainers?includeInactive=false` | Admin, Receptionist | 200 `TrainerSummaryResponse[]` | `ListAsync` |
| GET | `/api/trainers/{id}` | Admin, Receptionist | 200 `TrainerResponse` | `GetAsync` |
| POST | `/api/trainers` | Admin | 201 `TrainerResponse` + `Location` | `Trainer.Hire` |
| PUT | `/api/trainers/{id}` | Admin | 200 `TrainerResponse` | `UpdateProfile` |
| POST | `/api/trainers/{id}/activate` | Admin | 204 | `Activate` |
| POST | `/api/trainers/{id}/deactivate` | Admin | 204 | `Deactivate` |
| PUT | `/api/trainers/{id}/working-hours` | Admin | 200 `TrainerResponse` | `SetWorkingHours` |
| POST | `/api/trainers/{id}/clients/{clientId}` | Admin | 204 | `AssignClient` |
| DELETE | `/api/trainers/{id}/clients/{clientId}` | Admin | 204 | `UnassignClient` |
| PUT | `/api/trainers/{id}/identity` | Admin | 204 | `LinkIdentity` |

The list is ordered by last name, then first name. Inactive trainers are hidden unless `includeInactive=true`.

## Requests

- **`TrainerRequest`** (hire and update): `firstName`, `lastName` (required, ≤ 100), `middleName` (optional, ≤ 100), `phone` (required, ≤ 32 chars of input; the domain normalizes it and checks 10–15 digits), `email` (optional, ≤ 254; blank means none; stored lowercase), `specialization` (required, ≤ 100, trimmed).
- **Working hours** body is a JSON array that replaces the whole schedule; an empty array clears it:

  ```json
  [{ "day": "Monday", "start": "08:00:00", "end": "12:00:00" }]
  ```

  `day` is a `DayOfWeek` name (numbers 0–6 are also accepted), `start` and `end` are `TimeOnly`. All three are required. The array may not contain `null` items (`[NoNullItems]`).
- **`LinkIdentityRequest`**: `{ "identityUserId": "auth0|..." }`, required, ≤ 128, trimmed. Relinking the same id to the same trainer is a no-op success.

## Responses

- **`TrainerSummaryResponse`**: `id`, `firstName`, `lastName`, `middleName`, `fullName`, `phone`, `email`, `specialization`, `isActive`.
- **`TrainerResponse`**: the summary fields plus `identityUserId`, `workingHours` (`[{ day, start, end }]`, `day` as a name, sorted by day then start) and `clients` (`[{ clientId, fullName, assignedAt }]`, sorted by assignment time). `assignedAt` is club-local time from `TimeProvider.GetLocalNow()`.
- Client names are looked up one by one through `IClientRepository.GetByIdAsync`. A trainer's client list is small, so this avoids adding a batch query to the repository.

## Rules and error mapping

| Case | Where | Status |
|---|---|---|
| Malformed JSON, missing or too long fields, unknown day name, `null` array item | DataAnnotations / model binding | 400 |
| Invalid phone or email, blank specialization, end ≤ start, overlapping hours on a day | `DomainException` | 400 |
| Assigning a client twice, assigning to an inactive trainer, unassigning a client who is not assigned | `DomainException` | 400 |
| Trainer not found; client not found on assign | `NotFoundException` | 404 |
| Phone used by another trainer (compared after normalization, self excluded on update) | `ConflictException` in service | 409 |
| Identity user id linked to another trainer | `ConflictException` in service | 409 |
| Stale `Version` on save | `ConflictException` | 409 |

Uniqueness of phone and identity id is checked in the service because InMemory doesn't enforce the unique indexes. The indexes stay configured for SQL Server. Each write loads the trainer through the repository, calls one domain method and calls `IUnitOfWork.SaveChangesAsync` once.

## Tests

- `tests/FitnessClub.UnitTests/Application/TrainerServiceTests.cs` against `InMemoryTrainerRepository`, `InMemoryClientRepository`, `FakeUnitOfWork` and `FixedTimeProvider`.
- `tests/FitnessClub.IntegrationTests/Trainers/TrainersEndpointsTests.cs`: happy paths, 400/404/409 cases, 401 anonymous, 403 for Receptionist on every write and for Trainer on reads.
