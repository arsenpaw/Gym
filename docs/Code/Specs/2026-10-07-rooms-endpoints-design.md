---
tags: [spec, api, rooms]
status: implemented
date: 2026-10-07
---

# Rooms Endpoints: Design Spec

Requirements source: [[Fitness Club System]]. Builds on the `Room` aggregate from [[2026-10-05-domain-model-and-architecture-design]] and mirrors the membership plans feature.

## Scope

Admins manage the club's rooms (name, capacity, active flag). All staff can read them, because trainers and receptionists need rooms when scheduling sessions. Rooms are never deleted, only deactivated, so past sessions keep their room.

## Layers

- **Domain** (already built): `Room.Create(name, capacity)`, `Update`, `Activate`, `Deactivate`. `Room` trims the name and checks name length (max 100) and capacity (1 to 500), throwing `DomainException`. `IRoomRepository` has `ListAsync(includeInactive)` and `NameExistsAsync(name, excludeId)` (case-insensitive, trimmed).
- **Application:** `IRoomService` + internal `RoomService` in `Application/Rooms/`, registered in `AddApplication()`. Name uniqueness is checked in the service, because InMemory does not enforce the unique index. Each write calls `IUnitOfWork.SaveChangesAsync` once.
- **Api:** `RoomsController` injects only `IRoomService`.

## Endpoints

Base route: `/api/rooms`. The class-level policy is `Admin, Receptionist, Trainer`. Writes add a method-level `Admin` requirement.

| Method | Route | Roles | Success | Notes |
|---|---|---|---|---|
| GET | `/api/rooms?includeInactive=false` | Admin, Receptionist, Trainer | 200 `RoomResponse[]` | Sorted by name. Inactive rooms only with `includeInactive=true`. |
| GET | `/api/rooms/{id}` | Admin, Receptionist, Trainer | 200 `RoomResponse` | |
| POST | `/api/rooms` | Admin | 201 `RoomResponse` + `Location: /api/rooms/{id}` | New rooms are active. |
| PUT | `/api/rooms/{id}` | Admin | 200 `RoomResponse` | Keeping the room's own name is allowed. |
| POST | `/api/rooms/{id}/activate` | Admin | 204 | Idempotent. |
| POST | `/api/rooms/{id}/deactivate` | Admin | 204 | Idempotent. |

## Shapes

`RoomRequest` (POST and PUT body):

| Field | Type | Validation |
|---|---|---|
| `name` | string | `[Required]`, `[StringLength(100)]`; whitespace-only is rejected |
| `capacity` | int | `[Range(1, 500)]` |

`RoomResponse`: `{ id: Guid, name: string, capacity: int, isActive: bool }`.

## Errors

All error bodies are `application/problem+json`.

| Status | When |
|---|---|
| 400 | Request fails DataAnnotations or the JSON is malformed; `Room` throws `DomainException` |
| 401 | No logged-in user |
| 403 | Receptionist or Trainer calls a write endpoint; any other role calls any endpoint |
| 404 | Room id not found (`NotFoundException`), or the id is not a GUID (route constraint) |
| 409 | Another room already has the name, ignoring case and surrounding spaces (`ConflictException`); stale `Version` on save |

## Tests

- `UnitTests/Application/RoomServiceTests` runs against `Fakes/InMemoryRoomRepository` and `FakeUnitOfWork`. It covers create, the uniqueness conflict, domain errors, not-found on every operation, update keeping its own name, list filtering, and activate/deactivate.
- `IntegrationTests/Rooms/RoomsEndpointsTests` covers the happy paths, 201 with `Location`, 400 bodies, 404, 409, 401, and 403 for Receptionist and Trainer on every write. It also checks that all three staff roles can read.
