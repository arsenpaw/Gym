---
tags: [spec, api, notifications, hangfire]
status: implemented
date: 2026-10-07
---

# Membership Expiry Notifications: Design Spec

Sub-project 4 of the backend. Requirements source: [[Fitness Club System]] ("clients are notified automatically before their membership expires"). The domain model (`Notification`, `Client.NeedsExpiryNotice`, `MembershipsNeedingExpiryNotice`) comes from [[2026-10-05-domain-model-and-architecture-design]]. This spec adds the use cases, the daily Hangfire job and the admin endpoints.

## Flow

One run = **create due notices**, then **send pending notices**.

1. **Create** (`IExpiryNotificationService.CreateDueNoticesAsync`)
   - `now = TimeProvider.GetLocalNow()`, `today` = its date (club-local).
   - Window: memberships ending from `today` to `today + ExpiryNoticeDays`, both inclusive.
   - Loads candidates with `IClientRepository.ListWithMembershipsEndingBetweenAsync`, then asks each client for `MembershipsNeedingExpiryNotice(today, endsBy)`. That already skips clients without an email address and renewed, cancelled, ended and used-up memberships.
   - The service narrows further: a membership whose whole validity is no longer than the window (`EndsOn − StartsOn < ExpiryNoticeDays`) gets no notice. This drops single-visit passes and memberships that haven't started yet, which would otherwise be "warned" on the day they were bought.
   - Skips a membership that already has a `MembershipExpiring` notice (`INotificationRepository.ExistsForMembershipAsync`). Runs are idempotent.
   - Adds `Notification.MembershipExpiring(client, membership, now)` for the rest and saves once.
2. **Send** (`SendPendingAsync`)
   - For each `Pending` notice (`ListPendingAsync`, oldest first), calls `INotificationSender.SendAsync(channel, recipient, message)`.
   - Success → `MarkSent(now)`. Any exception except cancellation → `MarkFailed(exception.Message)`. Then the next notice.
   - Saves **after each notice**, not once per run, so a crash halfway through doesn't resend notices that already went out.
3. **Retry** (`RetryAsync`): `Failed` → `Pending`. The next run sends it again.

`RunAsync` does create then send, and returns `{ created, sent, failed }`.

## Components

| Layer | Type | Notes |
|---|---|---|
| Application | `INotificationSender` (`Abstractions/`) | Sends one message: channel, recipient, message |
| Application | `IExpiryNotificationService` + internal `ExpiryNotificationService` | Create, send, run, list, retry |
| Application | `ExpiryNotificationOptions` | Section `Notifications`, `ExpiryNoticeDays` (default 7, range 1–60) |
| Domain | `INotificationRepository.ListAsync(status?)` | Newest first, optional status filter |
| Infrastructure | `SmtpNotificationSender` | Emails the notice over SMTP with MailKit, used when `Smtp:Host` is set. Plain-text body, subject "Your membership expires soon", a new connection per notice (`SecureSocketOptions.Auto`, 30 s timeout). Any channel other than `Email` throws, so the notice is marked `Failed` |
| Infrastructure | `LoggingNotificationSender` | Writes the message to `ILogger`, used when `Smtp:Host` is empty (local dev, tests) |
| Infrastructure | `ExpiryNotificationJob` | Hangfire job that calls `RunAsync`. `[DisableConcurrentExecution]` keeps two runs from overlapping |
| Infrastructure | `RecurringJobs.Register` | `membership-expiry-notifications`, `Cron.Daily(8)` (08:00) in `TimeZoneInfo.Local` |
| Api | `NotificationsController` | Admin only |

Application may not reference `Microsoft.Extensions.Options`, so `AddInfrastructure()` binds and validates the options (`ValidateDataAnnotations`, `ValidateOnStart`) and registers the bound `ExpiryNotificationOptions` instance as a singleton for the service. An invalid value stops startup with `OptionsValidationException`.

## Endpoints

All under `api/notifications`, role **Admin** (401 without a user, 403 for other roles).

| Method | Path | Result |
|---|---|---|
| GET | `/api/notifications?status=` | 200, list of `{ id, clientId, membershipId, type, channel, recipient, message, status, createdAt, sentAt, failureReason }`, newest first. `status` is `Pending`, `Sent` or `Failed` (any case). Anything else → 400 |
| POST | `/api/notifications/{id}/retry` | 204. 404 if missing, 400 if the notice isn't `Failed` |
| POST | `/api/notifications/run` | 200 with `{ created, sent, failed }`. Runs a full create + send right away (for ops and testing) |

`run` executes inline and returns its counts, so it answers 200 with a body, not 202/204.

## Configuration

```json
"Notifications": { "ExpiryNoticeDays": 7 },
"Smtp": { "Host": "", "Port": 587, "Username": "", "Password": "", "FromAddress": "", "FromName": "Fitness Club" }
```

- **Email only.** `Client.NeedsExpiryNotice` requires an email address, so new notices are always on the `Email` channel. `NotificationChannel.Sms` stays in the enum for older rows (the seeded history has some).
- **Choosing the sender.** `AddInfrastructure()` registers `SmtpNotificationSender` when `Smtp:Host` is set and `LoggingNotificationSender` otherwise. `SmtpOptions` is validated at startup: `Port` 1–65535, and `FromAddress` is required once `Host` is set. Authentication runs only when `Username` is set. Port 587 uses STARTTLS, 465 SSL.
- **Deploy.** `deploy/docker-compose.yml` passes `SMTP_*` variables, all optional (an empty `SMTP_HOST` means log only). `deploy/docker-compose.server.yml` requires them. Gmail works with `smtp.gmail.com:587` and an app password.

The job time (08:00 club-local) is fixed in code.

## Tests

- **Unit** (`ExpiryNotificationServiceTests`, fakes only): window edges and configured window, clients without email skipped, no duplicates across runs, renewed / cancelled / short or not-started passes skipped, send success and failure with the run continuing, retry, list filter, run.
- **Integration** (`Notifications/`): HTTP list / filter / retry / run, 400 / 401 / 403 / 404 cases; startup registers the recurring job with the right cron and time zone; triggering the job through Hangfire creates and sends a notice; an invalid `ExpiryNoticeDays` fails startup; the sender is `LoggingNotificationSender` without an SMTP host and `SmtpNotificationSender` with one, and a host without `FromAddress` fails startup.
- **SMTP** (`SmtpNotificationSenderTests`): sends through a Mailpit container (Testcontainers) and checks from, to, subject and body through its API; a non-email channel and an unreachable server throw.

## Known gaps

- No SMS channel. Clients without an email address get no expiry notice. An SMS provider would be another `INotificationSender` plus restoring the SMS fallback in `Notification.MembershipExpiring`.
- A retried older `Sms` notice fails again with the SMTP sender.
- Each notice opens its own SMTP connection. That is fine for a few notices a day, but not for bulk sending.
- `POST /run` and the Hangfire job can overlap: `[DisableConcurrentExecution]` only guards job runs. Overlap at worst sends a notice twice, or one run gets a 409 on a stale `Version`.
- A notice that is pending when the client renews or cancels is still sent. There is no "cancelled" notification status.
- The list endpoint has no paging.
