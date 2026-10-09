---
tags: [design, architecture]
date: 2026-10-09
---

# Architecture

How the system is put together and how a request moves through it. The static structure is in [[Class Diagram]], the patterns in [[Design Patterns]], and the use cases in [[Use Cases]]. Original designs: [[2026-10-05-api-foundation-design]], [[2026-10-05-domain-model-and-architecture-design]], [[2026-10-07-ui-design]].

## Architectural styles

- **Client–server SPA + REST API.** The React UI talks to the API over HTTP only and never touches the database. Resources are nouns (`/api/clients/{id}/memberships`), state changes that aren't plain updates are sub-resources (`/activate`, `/cancel`, `/retry`), and status codes carry meaning: 201 + `Location` on create, 204 on no-content actions, 400/404/409 as `application/problem+json`.
- **Clean Architecture.** Four projects with dependencies pointing inwards. `FitnessClub.ArchitectureTests` (NetArchTest + Roslyn) fails the build if a layer references one it shouldn't.
- **Domain-Driven Design.** Aggregates guard their own rules, value objects validate themselves, and a domain service (`SessionScheduler`) handles the rules that span aggregates.
- **CQRS-lite read side.** Writes go through aggregates and repositories. Reports skip the aggregates and read the database directly through `IReportQueries` / `ReportQueries`.
- **Contract-first UI client.** A Debug build of the API writes the OpenAPI document `ui/openapi/fitnessclub.json`. Orval generates the UI's types, axios calls and TanStack Query hooks from it.
- **Containers.** Each service is its own Docker image; `deploy/docker-compose.yml` runs the stack.

## Deployment

```mermaid
flowchart LR
    Browser["🖥 Browser<br/>React SPA"]

    subgraph Compose["docker compose"]
        subgraph Frontend["network: frontend"]
            UI["ui<br/>nginx :8080<br/>static files + proxy /api/"]
        end
        API["api<br/>ASP.NET Core :8080<br/>REST + Hangfire server"]
        subgraph Backend["network: backend"]
            DB[("db<br/>SQL Server 2022<br/>dbo + HangFire schemas")]
        end
    end

    Auth0["🔐 Auth0<br/>login, JWT, roles"]
    SendGrid["✉ Twilio SendGrid<br/>v3 mail API"]

    Browser -- "HTTPS :8081" --> UI
    UI -- "/api/* (same origin, no CORS)" --> API
    API -- "EF Core / Hangfire storage" --> DB
    Browser -- "Universal Login, access token" --> Auth0
    API -- "validates JWT (JWKS)" --> Auth0
    API -- "POST /v3/mail/send" --> SendGrid
```

The API sits on both networks, so the database is reachable only from the API. On startup the API runs `Database.MigrateAsync()` (schema + seed data), then registers the recurring job.

## Layers

```mermaid
flowchart TB
    subgraph Api["FitnessClub.Api"]
        A1["Controllers (one per area)"]
        A2["Auth0 JWT setup, role policies"]
        A3["ExceptionToProblemDetailsHandler"]
        A4["OpenAPI + Scalar, Program.cs (composition root)"]
    end
    subgraph Infrastructure["FitnessClub.Infrastructure"]
        I1["FitnessClubDbContext, configurations, migrations"]
        I2["Repositories, UnitOfWork"]
        I3["SendGrid / Logging senders, EmailTemplates"]
        I4["ReportQueries, Hangfire job + RecurringJobs"]
    end
    subgraph Application["FitnessClub.Application"]
        P1["I{Name}Service + internal services (use cases)"]
        P2["Request / response records (DTOs)"]
        P3["Abstractions: IUnitOfWork, INotificationSender,<br/>IEmailTemplates, IReportQueries"]
    end
    subgraph Domain["FitnessClub.Domain (BCL only)"]
        D1["Aggregates, child entities"]
        D2["Value objects (SharedKernel)"]
        D3["Domain service SessionScheduler"]
        D4["Repository interfaces, DomainException"]
    end

    Api --> Application
    Api -. "Program only" .-> Infrastructure
    Infrastructure --> Application
    Application --> Domain
    Infrastructure --> Domain
```

| Layer | May reference | Holds |
|---|---|---|
| Domain | BCL only | Business rules. No EF Core, no ASP.NET. |
| Application | Domain, DI abstractions | Use cases. Never EF Core or `IQueryable`. |
| Infrastructure | Application, Domain | EF Core, SQL Server, Hangfire, SendGrid. Classes are `internal`. |
| Api | Application; Infrastructure only in `Program`; Domain only to map `DomainException` | HTTP, auth, error mapping. |

The UI is organised the same way on a smaller scale: `features/*` (one folder per screen), `api/` (generated client + `http.ts`), `auth/` (`RequireRole`, `AccessTokenBridge`), `layout/`, `app/` (routes, query client, theme).

## Request flows

### Check in a client

```mermaid
sequenceDiagram
    actor R as Receptionist
    participant UI as CheckInPage
    participant C as ClientsController
    participant S as ClientService
    participant CR as IClientRepository
    participant CL as Client
    participant M as Membership
    participant VR as IVisitRepository
    participant U as IUnitOfWork

    R->>UI: choose client, "Check in"
    UI->>C: POST /api/clients/{id}/visits (useClientsCheckIn)
    C->>S: CheckInAsync(id)
    S->>CR: GetByIdAsync(id)
    CR-->>S: Client with memberships
    S->>CL: CheckIn(now)
    CL->>CL: ActiveMembershipOn(today)
    CL->>M: RegisterVisit(today)
    CL-->>S: Visit.Record(clientId, membershipId, now)
    S->>VR: Add(visit)
    S->>U: SaveChangesAsync()
    S-->>C: VisitResponse
    C-->>UI: 201 Created
    UI->>UI: show "Checked in", refreshClient (invalidates client queries)
```

If the client has no active membership or already checked in today, `Client.CheckIn` throws `DomainException`, and the API answers 400 problem details (see the next flow).

### Book a session, and how errors come back

```mermaid
sequenceDiagram
    actor R as Receptionist
    participant C as SessionsController
    participant S as TrainingSessionService
    participant SS as SessionScheduler
    participant TR as ITrainingSessionRepository
    participant TS as TrainingSession
    participant U as IUnitOfWork
    participant H as ExceptionToProblemDetailsHandler

    R->>C: POST /api/sessions/{id}/bookings
    C->>S: BookAsync(sessionId, request)
    S->>S: load session and client
    S->>SS: BookAsync(session, client, now)
    SS->>TR: ClientHasBookingDuringAsync(clientId, slot)
    alt client is busy at that time
        SS-->>H: DomainException
        H-->>R: 400 application/problem+json
    else free
        SS->>TS: Book(client, now)
        TS->>TS: EnsureOpen, membership, duplicate, capacity checks
        TS-->>SS: Booking
        SS-->>S: Booking
        S->>U: SaveChangesAsync()
        alt stale Version or duplicate key
            U-->>H: ConflictException
            H-->>R: 409 application/problem+json
        else saved
            S-->>C: BookingResponse
            C-->>R: 201 Created
        end
    end
```

### Expiry notification run

```mermaid
sequenceDiagram
    participant T as Admin (POST /api/notifications/run) or Hangfire
    participant J as ExpiryNotificationJob
    participant S as ExpiryNotificationService
    participant CR as IClientRepository
    participant NR as INotificationRepository
    participant ET as IEmailTemplates
    participant N as Notification
    participant NS as INotificationSender
    participant U as IUnitOfWork

    T->>J: RunAsync()
    J->>S: RunAsync()
    S->>S: CreateDueNoticesAsync()
    S->>CR: ListWithMembershipsEndingBetweenAsync(today, today + ExpiryNoticeDays)
    loop each membership needing a notice
        S->>NR: ExistsForMembershipAsync(membershipId, MembershipExpiring)
        S->>ET: ExpiryReminder(ExpiryReminderEmail.For(client, membership))
        ET-->>S: NotificationContent (subject, text, HTML)
        S->>N: MembershipExpiring(client, membership, content, now)
        S->>NR: Add(notification)
    end
    S->>U: SaveChangesAsync()
    S->>S: SendPendingAsync()
    S->>NR: ListPendingAsync()
    loop each pending notification
        S->>NS: SendAsync(notification)
        alt sent
            S->>N: MarkSent(now)
        else failed
            S->>N: MarkFailed(reason)
        end
        S->>U: SaveChangesAsync()
    end
    S-->>T: NotificationRunResponse(created, sent, failed)
```

`POST /api/notifications/run` calls `IExpiryNotificationService.RunAsync` directly; the Hangfire job calls the same method. The job is registered in `RecurringJobs.Register` with `Cron.Never()`, so today it runs only when triggered by hand (see [[Requirements Coverage]]).

## Cross-cutting concerns

- **Auth:** Auth0 JWT bearer. A fallback policy requires a signed-in user on every endpoint; controllers add `[Authorize(Roles = ...)]` with the `Roles` constants. The UI gets the token through `AccessTokenBridge` and attaches it in the axios request interceptor.
- **Errors:** one `IExceptionHandler` maps `DomainException` → 400, `NotFoundException` → 404, `ConflictException` → 409, anything else → 500. The UI shows `problemMessage(error)` from a `MutationCache.onError` handler.
- **Concurrency:** every aggregate root has a shadow `Version` token, re-stamped on save. A stale save → `ConflictException` → 409.
- **Time:** services use the injected `TimeProvider` and pass `now` into domain methods.
- **Validation:** DataAnnotations on request records (shape, automatic 400), then entities and value objects (business rules, `DomainException`).
