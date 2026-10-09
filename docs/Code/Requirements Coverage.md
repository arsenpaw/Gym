---
tags: [requirements, api]
date: 2026-10-07
---

# Requirements Coverage

Maps each requirement in [[Fitness Club System]] to its API endpoints. The domain model is in [[2026-10-05-domain-model-and-architecture-design]]. Each area has its own spec, linked below.

## Coverage

| # | Requirement | Endpoints | Spec | Status |
|---|---|---|---|---|
| 1 | Client records (name, age, phone, membership, expiry) | `GET/POST /api/clients`, `GET/PUT /api/clients/{id}`, `POST /api/clients/{id}/memberships`, `POST /api/clients/{id}/memberships/{membershipId}/cancel` | [[2026-10-07-clients-endpoints-design]], [[2026-10-09-client-contacts-and-page-design]] | ✅ Email is the required, unique primary contact; phone is optional |
| 2 | Visits recorded on every check-in | `POST /api/clients/{id}/visits`, `GET /api/clients/{id}/visits?page&pageSize` (paged) | [[2026-10-07-clients-endpoints-design]] | ✅ |
| 3 | Trainer profile (specialization, schedule, client list) | `GET/POST /api/trainers`, `GET/PUT /api/trainers/{id}`, `/activate`, `/deactivate`, `PUT /{id}/working-hours`, `POST/DELETE /{id}/clients/{clientId}`, `PUT /{id}/identity` | [[2026-10-07-trainers-endpoints-design]] | ✅ |
| 4 | Group and individual session sign-ups | Rooms: `GET/POST /api/rooms`, `GET/PUT /api/rooms/{id}`, `/activate`, `/deactivate`. Sessions: `GET /api/sessions?from&to`, `GET /api/sessions/mine`, `GET /api/sessions/{id}`, `POST /api/sessions`, `POST /{id}/cancel`, `POST /{id}/bookings`, `POST /{id}/bookings/{clientId}/cancel` | [[2026-10-07-rooms-endpoints-design]], [[2026-10-07-sessions-endpoints-design]] | ✅ |
| 5 | Configurable plans (single visit, monthly, yearly) | `GET/POST /api/membership-plans`, `GET/PUT /{id}`, `/activate`, `/deactivate` | [[2026-10-05-api-foundation-design]] | ✅ |
| 6 | Automatic expiry notification | Hangfire job `membership-expiry-notifications`, manual trigger only (`Cron.Never()`); `GET /api/notifications?status`, `POST /{id}/retry`, `POST /run` | [[2026-10-07-expiry-notifications-design]] | ⚠️ Partial: the job is not scheduled, so notices go out only when staff run it by hand. Restore a cron in `RecurringJobs.Register` to make it automatic again (email through Twilio SendGrid; every client has an email address). Staff can also send a reminder or promotion: `GET /api/clients/{id}/messages/preview`, `POST/GET /api/clients/{id}/messages` ([[2026-10-08-client-messages-design]]) |
| 7 | Report: clients with visit activity | `GET /api/reports/client-activity?from&to` | [[2026-10-07-reports-design]] | ✅ |
| 8 | Report: revenue per month / year | `GET /api/reports/revenue?year&month` | [[2026-10-07-reports-design]] | ✅ |
| 9 | Report: trainer and room load by day | `GET /api/reports/load?from&to` | [[2026-10-07-reports-design]] | ✅ |

## Roles

| Area | Read | Write |
|---|---|---|
| Membership plans | Admin, Receptionist | Admin |
| Clients, memberships, visits | Admin, Receptionist | Admin, Receptionist |
| Trainers | Admin, Receptionist | Admin |
| Rooms | Admin, Receptionist, Trainer | Admin |
| Sessions, bookings | Admin, Receptionist, Trainer (`/mine`: Trainer only) | Admin, Receptionist |
| Notifications, reports | Admin | Admin |


## UI screens

The staff portal is described in [[2026-10-07-ui-design]].

| # | Requirement | Screen |
|---|---|---|
| 1 | Client records | Clients (`/clients`, `/clients/:id`) |
| 2 | Visits | Check-in (`/check-in`), paged visit history in the client page's Visits tab |
| 3 | Trainer profile | Trainers (`/trainers`, `/trainers/:id`): hours, clients, login link |
| 4 | Group and individual sign-ups | Schedule (`/schedule`): calendar, new session, bookings drawer. Rooms (`/rooms`) |
| 5 | Membership plans | Membership plans (`/plans`), sold from the client page |
| 6 | Expiry notification | Notifications (`/notifications`) |
| 7–9 | Reports | Reports (`/reports`): client activity, revenue, trainer and room load |

## Open items

- Notifications are emailed by `SendGridNotificationSender` when `SendGrid:ApiKey` is set, otherwise only written to the log by `LoggingNotificationSender`. There is no SMS channel; email is required for every client, so every client can be notified.
- "Now" is `TimeProvider.GetLocalNow()`, so the server's time zone must be the club's. Set `TZ` in the Docker image.
- Enums are sent as strings through per-property converters. A global JSON enum converter in `Program.cs` would make this one rule.
- The notifications list has no paging.
