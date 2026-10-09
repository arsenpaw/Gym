---
tags: [design, use-cases]
date: 2026-10-09
---

# Use Cases

Use case diagram of the fitness club system as it is built. Requirements are in [[Fitness Club System]], endpoints per requirement in [[Requirements Coverage]], and the classes behind each use case in [[Class Diagram]].

## Actors

| Actor | Kind | Who it is |
|---|---|---|
| Receptionist | primary | Front desk staff. Role `Receptionist` in `Roles.cs`. |
| Admin | primary | Club manager. Role `Admin`. Can do everything a receptionist can, plus settings, notifications and reports. |
| Trainer | primary | Role `Trainer`. Sees the schedule and rooms, read-only, and their own sessions. |
| Scheduler | secondary | The Hangfire job `membership-expiry-notifications` (`ExpiryNotificationJob`). |
| Auth0 | secondary | Identity provider. Signs users in and puts their roles into the access token. |
| Twilio SendGrid | secondary | Email delivery for expiry notices, reminders and promotions. |

## Diagram

Mermaid has no native use case diagram, so this is a flowchart in UML style: actors outside the system boundary, use cases as rounded nodes, dashed `«include»` / `«extend»` edges. Admin generalizes Receptionist, so Admin has every receptionist use case too.

```mermaid
flowchart LR
    Receptionist(["👤 Receptionist"])
    Admin(["👤 Admin"])
    Trainer(["👤 Trainer"])
    Scheduler(["⚙ Scheduler<br/>(Hangfire)"])
    Auth0(["🔐 Auth0"])
    SendGrid(["✉ Twilio SendGrid"])

    Admin -- "generalizes" --> Receptionist

    subgraph System["Fitness Club System"]
        direction TB
        UC_SignIn(["Sign in"])

        subgraph Front["Front desk"]
            UC_CheckIn(["Check in client"])
            UC_FindClient(["Find client"])
            UC_VerifyMembership(["Verify active membership"])
            UC_Register(["Register / edit client"])
            UC_Sell(["Sell membership"])
            UC_Payment(["Record payment"])
            UC_CancelMembership(["Cancel membership"])
            UC_Visits(["View visit history"])
            UC_Message(["Send reminder / promotion email"])
        end

        subgraph Training["Schedule and bookings"]
            UC_Schedule(["Schedule / cancel session"])
            UC_CheckConflicts(["Check trainer, room and client conflicts"])
            UC_Book(["Book / cancel booking"])
            UC_ViewSchedule(["View schedule"])
            UC_MySchedule(["View own sessions"])
            UC_ViewRooms(["View rooms"])
        end

        subgraph Settings["Administration"]
            UC_Plans(["Manage membership plans"])
            UC_Trainers(["Manage trainers"])
            UC_Hours(["Set working hours"])
            UC_Assign(["Assign clients to trainer"])
            UC_Link(["Link trainer login"])
            UC_Rooms(["Manage rooms"])
        end

        subgraph Notify["Notifications and reports"]
            UC_RunNotices(["Run expiry notifications"])
            UC_Retry(["Retry failed notification"])
            UC_SendEmail(["Send email"])
            UC_Reports(["View reports:<br/>client activity, revenue, load"])
        end
    end

    Receptionist --- UC_SignIn
    Trainer --- UC_SignIn
    UC_SignIn -.- Auth0

    Receptionist --- UC_CheckIn
    Receptionist --- UC_Register
    Receptionist --- UC_Sell
    Receptionist --- UC_CancelMembership
    Receptionist --- UC_Visits
    Receptionist --- UC_Message
    Receptionist --- UC_Schedule
    Receptionist --- UC_Book
    Receptionist --- UC_ViewSchedule

    Trainer --- UC_ViewSchedule
    Trainer --- UC_MySchedule
    Trainer --- UC_ViewRooms

    Admin --- UC_Plans
    Admin --- UC_Trainers
    Admin --- UC_Rooms
    Admin --- UC_RunNotices
    Admin --- UC_Retry
    Admin --- UC_Reports

    Scheduler --- UC_RunNotices

    UC_CheckIn -. "«include»" .-> UC_FindClient
    UC_CheckIn -. "«include»" .-> UC_VerifyMembership
    UC_Sell -. "«include»" .-> UC_Payment
    UC_Book -. "«include»" .-> UC_VerifyMembership
    UC_Book -. "«include»" .-> UC_CheckConflicts
    UC_Schedule -. "«include»" .-> UC_CheckConflicts
    UC_Hours -. "«extend»" .-> UC_Trainers
    UC_Assign -. "«extend»" .-> UC_Trainers
    UC_Link -. "«extend»" .-> UC_Trainers
    UC_RunNotices -. "«include»" .-> UC_SendEmail
    UC_Retry -. "«include»" .-> UC_SendEmail
    UC_Message -. "«include»" .-> UC_SendEmail
    UC_SendEmail --- SendGrid
```

## Use case → endpoint → screen

All endpoints need a signed-in user (Auth0 JWT). Role checks are `[Authorize(Roles = ...)]` on the controllers, and the UI hides screens with `RequireRole` and `navItems` in `ui/src/layout/navigation.ts`.

| Use case | Actors | Endpoints | UI screen |
|---|---|---|---|
| Sign in | All staff, Auth0 | Auth0 Universal Login; every API call carries the access token | Login redirect |
| Check in client | Receptionist, Admin | `POST /api/clients/{id}/visits` | `/check-in` |
| Register / edit client | Receptionist, Admin | `GET/POST /api/clients`, `GET/PUT /api/clients/{id}` | `/clients`, `/clients/:clientId` |
| Sell membership (+ record payment) | Receptionist, Admin | `POST /api/clients/{id}/memberships` | Client page → Memberships |
| Cancel membership | Receptionist, Admin | `POST /api/clients/{id}/memberships/{membershipId}/cancel` | Client page → Memberships |
| View visit history | Receptionist, Admin | `GET /api/clients/{id}/visits?page&pageSize` | Client page → Visits |
| Send reminder / promotion email | Receptionist, Admin, SendGrid | `GET /api/clients/{id}/messages/preview`, `POST/GET /api/clients/{id}/messages` | Client page → Messages |
| Schedule / cancel session | Receptionist, Admin | `POST /api/sessions`, `POST /api/sessions/{id}/cancel` | `/schedule` |
| Book / cancel booking | Receptionist, Admin | `POST /api/sessions/{id}/bookings`, `POST /api/sessions/{id}/bookings/{clientId}/cancel` | `/schedule` → session drawer |
| View schedule | Receptionist, Admin, Trainer | `GET /api/sessions?from&to`, `GET /api/sessions/{id}` | `/schedule` |
| View own sessions | Trainer | `GET /api/sessions/mine` | `/schedule` (My schedule) |
| View rooms | Trainer (read), Receptionist, Admin | `GET /api/rooms`, `GET /api/rooms/{id}` | `/rooms` |
| Manage membership plans | Admin | `POST /api/membership-plans`, `PUT /{id}`, `/activate`, `/deactivate` | `/plans` |
| Manage trainers (+ hours, clients, login) | Admin | `POST /api/trainers`, `PUT /{id}`, `/activate`, `/deactivate`, `PUT /{id}/working-hours`, `POST/DELETE /{id}/clients/{clientId}`, `PUT /{id}/identity` | `/trainers`, `/trainers/:trainerId` |
| Manage rooms | Admin | `POST /api/rooms`, `PUT /{id}`, `/activate`, `/deactivate` | `/rooms` |
| Run expiry notifications | Admin, Scheduler, SendGrid | `POST /api/notifications/run`; Hangfire job `membership-expiry-notifications` (registered with `Cron.Never()`, so it runs only by hand) | `/notifications` |
| Retry failed notification | Admin | `POST /api/notifications/{id}/retry` | `/notifications` |
| View reports | Admin | `GET /api/reports/client-activity`, `/revenue`, `/load` | `/reports` |
