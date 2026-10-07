---
tags: [spec, api, sessions]
status: implemented
date: 2026-10-07
---

# Training Sessions and Bookings Endpoints: Design Spec

Part of sub-project 3 (trainers, schedules, rooms, bookings). Requirements source: [[Fitness Club System]]. Domain model: [[2026-10-05-domain-model-and-architecture-design]].

Covers group classes and individual sessions, and client sign-ups (bookings) for them. Built on the existing `TrainingSession` aggregate, `ISessionScheduler` and repositories. No Domain or repository changes.

## Layers

- **Application (`Application/Training`):** `ITrainingSessionService` with the internal `TrainingSessionService`, registered in `AddApplication()`. It loads aggregates through `ITrainingSessionRepository`, `ITrainerRepository`, `IRoomRepository` and `IClientRepository`, creates sessions and bookings only through `ISessionScheduler`, and calls `IUnitOfWork.SaveChangesAsync` once per write.
- **Time:** "now" is `TimeProvider.GetLocalNow()` (club-local).
- **Api:** `SessionsController` at `api/sessions`. It injects only `ITrainingSessionService`. For `mine` it reads the caller's `sub` claim and passes the string to the service. The service never reads `HttpContext`.

## Endpoints and roles

The controller requires Admin, Receptionist or Trainer. Writes add Admin or Receptionist. `mine` adds Trainer.

| Method | Route | Roles | Success |
|---|---|---|---|
| GET | `/api/sessions?from=&to=` | Admin, Receptionist, Trainer | 200 `SessionSummaryResponse[]` |
| GET | `/api/sessions/mine?from=&to=` | Trainer | 200 `SessionSummaryResponse[]` |
| GET | `/api/sessions/{id}` | Admin, Receptionist, Trainer | 200 `SessionResponse` |
| POST | `/api/sessions` | Admin, Receptionist | 201 `SessionResponse`, `Location: /api/sessions/{id}` |
| POST | `/api/sessions/{id}/cancel` | Admin, Receptionist | 204 |
| POST | `/api/sessions/{id}/bookings` | Admin, Receptionist | 201 `BookingResponse`, `Location: /api/sessions/{id}` |
| POST | `/api/sessions/{id}/bookings/{clientId}/cancel` | Admin, Receptionist | 204 |

- **Lists** return sessions whose start is in `[from, to)`, ordered by start. Both statuses are included.
- **`mine`** finds the trainer by `ITrainerRepository.GetByIdentityUserIdAsync(sub)`, then lists that trainer's sessions. If no trainer is linked to the caller, it returns 404.

## Shapes

Enums are serialized as strings: `type` is `Group` or `Individual`, and `status` is `Scheduled` or `Cancelled`. Requests also accept the numeric value.

**`SessionPeriod`** (query): `from`, `to` (`DateTimeOffset`, both required). `to` must be after `from`, and the range can be at most 92 days.

**`ScheduleSessionRequest`:**

| Field | Rule |
|---|---|
| `title` | required, ≤ 100 chars |
| `type` | required |
| `trainerId`, `roomId` | required |
| `start`, `end` | required |
| `capacity` | 1..500 |

**`BookSessionRequest`:** `clientId`, required.

**`SessionSummaryResponse`:** `id`, `title`, `type`, `trainerId`, `roomId`, `start`, `end`, `capacity`, `status`, `activeBookingCount`.

**`SessionResponse`:** the summary fields plus `bookings`. These include cancelled bookings and are ordered by `bookedAt`.

**`BookingResponse`:** `clientId`, `bookedAt`, `cancelledAt` (null while active).

## Errors

All error bodies are `application/problem+json`.

| Status | When |
|---|---|
| 400 (model validation) | Missing or out-of-range fields, an unknown `type` string, malformed JSON, or a missing or invalid `from`/`to` |
| 400 (`DomainException`) | `end` ≤ `start` |
| | Start in the past, or duration outside 15 min–4 h |
| | Inactive trainer or room, or the trainer isn't working during the slot |
| | Individual session with a capacity other than 1, or capacity above the room's capacity |
| | Trainer or room already busy |
| | Client has no active membership on the session date, is already booked, or has an overlapping booking |
| | Session is full, cancelled or already started |
| | Cancelling a booking the client doesn't have |
| 401 | Not logged in |
| 403 | Wrong role. This includes non-trainers calling `mine` and trainers calling write endpoints |
| 404 | Session, trainer, room or client not found. Also returned for `mine` when no trainer is linked to the caller |
| 409 | A concurrent change to the same session, for example two bookings racing for the last place (the `Version` token) |

## Tests

- **Unit (`TrainingSessionServiceTests`):** the service against in-memory fakes (`InMemoryTrainerRepository`, `InMemoryRoomRepository`, `InMemoryClientRepository`, `FixedTimeProvider`). Covers not found, domain errors with no save, clock usage, and `mine` resolution.
- **Integration (`Sessions/SessionsEndpointsTests`, `Sessions/MySessionsEndpointsTests`):** HTTP happy paths, 400/401/403/404, a concurrent last-place booking that never overbooks (201 plus 400 or 409), and `mine`. Test data is seeded through repositories (`SessionSeeder`). `mine` runs in its own test class, because the test user's `sub` (`test-user`) can be linked to only one trainer per database.
