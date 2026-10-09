---
tags: [spec, api, reports]
status: implemented
date: 2026-10-07
---

# Reports: Design Spec

Sub-project 5 of the backend. Requirements source: [[Fitness Club System]]. Builds on [[2026-10-05-domain-model-and-architecture-design]], which says report queries are read models behind an Application interface implemented in Infrastructure, not aggregates.

## Goal

Three admin-only reports over HTTP:

- Clients with their visit activity.
- Club revenue per month and per year.
- Trainer and room load by day.

## Layers

| Layer | Type | Role |
|---|---|---|
| Application | `IReportService` / internal `ReportService` | Validates parameters, applies defaults, turns club-local dates into half-open instant ranges, and aggregates rows into report responses |
| Application | `IReportQueries` | Read-model contract. Returns flat rows (`PaymentEntry`, `SessionLoadEntry`) or finished `ClientActivityItem`s |
| Infrastructure | internal `ReportQueries` | `AsNoTracking` EF queries over `FitnessClubDbContext`. Registered in `AddInfrastructure()` |
| Api | `ReportsController` (`api/reports`, `[Authorize(Roles = Admin)]`) | Injects only `IReportService` |

Rules:
- Queries use only constructs that translate on both InMemory and SQL Server: plain filters, projections to owned or converted members, and one `GroupBy` with `COUNT(CASE …)` / `MAX`. Grouping by club-local day or month happens in memory in `ReportService`, since time zone conversion doesn't translate.
- `Money` is projected whole and unwrapped in memory, because `p.Amount.Amount` on a converted property doesn't translate.
- Client activity loads `Client` aggregates without tracking so it can reuse the domain's `AgeOn` and `ActiveMembershipOn` rules instead of copying them into SQL.

## Time

- "Today" is `TimeProvider.GetLocalNow()`. Club-local dates become instants at local midnight in `TimeProvider.LocalTimeZone`.
- Every range is half-open, from the first day's local midnight to the local midnight after the last day, the same as `IPaymentRepository.ListPaidBetweenAsync`.
- Payments and sessions are bucketed by the local date of `PaidAt` / slot start, converted to the club zone.

## Endpoints

All `GET`. Errors are problem details: 400 for a bad parameter, 401 anonymous, 403 for any role other than Admin.

### `GET /api/reports/client-activity?from=&to=`

- `from`/`to` are inclusive club-local dates (`yyyy-MM-dd`). `to` defaults to today and `from` defaults to `to` − 29, so the default is the last 30 days. `from > to` → 400.
- Lists every client, ordered by full name ("Last First Middle"; invariant culture, case-insensitive), then by id.

```json
{ "from": "2026-09-08", "to": "2026-10-07", "clients": [ {
  "clientId": "…", "fullName": "Avramenko Anna", "age": 35, "email": "anna@example.com", "phone": "+380…",
  "activeMembership": { "membershipId": "…", "planName": "Monthly", "startsOn": "2026-08-25", "endsOn": "2026-10-23", "remainingVisits": null },
  "visitCount": 2, "lastVisitAt": "2026-10-02T18:00:00+03:00" } ] }
```

- `age` is the age today, and `activeMembership` is the membership active today (`Client.ActiveMembershipOn`), or `null`.
- `visitCount` counts visits in the range. `lastVisitAt` is the latest visit at any time, or `null`.

### `GET /api/reports/revenue?year=&month=`

- `year` defaults to the current club year and must be 2000–2100. `month` is optional and must be 1–12. Out-of-range or non-numeric values → 400.
- Without `month`, the breakdown has all 12 months. With `month`, it has every day of that month. Both are zero-filled.

```json
{ "year": 2024, "month": null, "from": "2024-01-01", "to": "2024-12-31", "total": 1649.50, "paymentCount": 4,
  "breakdown": [ { "from": "2024-01-01", "to": "2024-01-31", "total": 0, "paymentCount": 0 }, … ] }
```

### `GET /api/reports/load?from=&to=`

- `from` defaults to today and `to` defaults to `from` + 6. `from > to` → 400, and so does a range of more than 93 days (inclusive).
- Includes only sessions with status `Scheduled`, bucketed by the local date of their start. Sessions can't cross midnight because a trainer's working hours are within one day.
- `days` has one entry for every date in the range. A day without sessions has empty lists.

```json
{ "from": "2026-11-02", "to": "2026-11-04", "days": [ { "date": "2026-11-02",
  "trainers": [ { "trainerId": "…", "trainerName": "Bondar Taras", "sessions": 2, "bookedHours": 2.5, "clientsBooked": 2 } ],
  "rooms": [ { "roomId": "…", "roomName": "Hall A", "sessions": 1, "occupiedHours": 1.0, "bookedPlaces": 2, "totalPlaces": 10, "utilizationPercent": 20.0 } ] } ] }
```

- **Trainer:** `bookedHours` is the sum of the session durations. `clientsBooked` is the number of distinct clients with an active booking in the trainer's sessions that day.
- **Room:** `occupiedHours` is the sum of the session durations. `bookedPlaces` is the number of active bookings, and `totalPlaces` is the sum of the session capacities (places offered, not room capacity × sessions). `utilizationPercent` = booked / total × 100, rounded to 1 decimal place.
- Trainers and rooms are ordered by name. Hours are rounded to 2 decimal places.

## Tests

`tests/FitnessClub.IntegrationTests/Reports`:
- `ReportsApiFixture` runs the API with a fixed club clock: 2026-10-07 12:00 in a custom +03:00 zone. Each test class gets its own InMemory database. Data is seeded through repositories and the domain, with explicit `now` values.
- Correctness of each report: local-midnight boundaries, payments stored in UTC offsets, zero filling, cancelled sessions and bookings, distinct clients, and utilization rounding.
- Defaults and 400 validation for each endpoint. 403 for Receptionist and Trainer, 401 anonymous, 200 for Admin.

## Out of scope

- Export (CSV/PDF) and paging. Client activity returns every client.
- Revenue by plan or payment method, and refunds (payments have no refunds yet).
- Room load against working hours or opening hours (there is no club schedule in the model).
