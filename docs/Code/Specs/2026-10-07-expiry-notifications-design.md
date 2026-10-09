---
tags: [spec, api, notifications, hangfire]
status: implemented
date: 2026-10-07
---

# Membership Expiry Notifications: Design Spec

Sub-project 4 of the backend. Requirements source: [[Fitness Club System]] ("clients are notified automatically before their membership expires"). The domain model (`Notification`, `Client.NeedsExpiryNotice`, `MembershipsNeedingExpiryNotice`) comes from [[2026-10-05-domain-model-and-architecture-design]]. This spec adds the use cases, the Hangfire job and the admin endpoints. Email delivery moved from SMTP to Twilio SendGrid with HTML templates in [[2026-10-08-client-messages-design]]; the sections below describe the current SendGrid setup.

## Flow

One run = **create due notices**, then **send pending notices**.

1. **Create** (`IExpiryNotificationService.CreateDueNoticesAsync`)
   - `now = TimeProvider.GetLocalNow()`, `today` = its date (club-local).
   - Window: memberships ending from `today` to `today + ExpiryNoticeDays`, both inclusive.
   - Loads candidates with `IClientRepository.ListWithMembershipsEndingBetweenAsync`, then asks each client for `MembershipsNeedingExpiryNotice(today, endsBy)`. That already skips renewed, cancelled, ended and used-up memberships.
   - The service narrows further: a membership whose whole validity is no longer than the window (`EndsOn − StartsOn < ExpiryNoticeDays`) gets no notice. This drops single-visit passes and memberships that haven't started yet, which would otherwise be "warned" on the day they were bought.
   - Skips a membership that already has a `MembershipExpiring` notice (`INotificationRepository.ExistsForMembershipAsync`). Runs are idempotent.
   - Adds `Notification.MembershipExpiring(client, membership, content, now)` for the rest, with the content from the expiry reminder template (`IEmailTemplates.ExpiryReminder`), and saves once.
2. **Send** (`SendPendingAsync`)
   - For each `Pending` notice (`ListPendingAsync`, oldest first), calls `INotificationSender.SendAsync(notification)`.
   - Success → `MarkSent(now)`. Any exception except cancellation → `MarkFailed(exception.Message)`. Then the next notice.
   - Saves **after each notice**, not once per run, so a crash halfway through doesn't resend notices that already went out.
3. **Retry** (`RetryAsync`): `Failed` → `Pending`. The next run sends it again.

`RunAsync` does create then send, and returns `{ created, sent, failed }`.

## Components

| Layer | Type | Notes |
|---|---|---|
| Application | `INotificationSender` (`Abstractions/`) | Sends one notification: recipient, subject, text and optional HTML |
| Application | `IExpiryNotificationService` + internal `ExpiryNotificationService` | Create, send, run, list, retry |
| Application | `ExpiryNotificationOptions` | Section `Notifications`, `ExpiryNoticeDays` (default 7, range 1–60) |
| Domain | `INotificationRepository.ListAsync(status?)` | Newest first, optional status filter |
| Infrastructure | `SendGridNotificationSender` | Posts the notice to SendGrid's v3 Mail Send API, used when `SendGrid:ApiKey` is set. Text and HTML parts, 30 s timeout. A non-2xx response or any channel other than `Email` throws, so the notice is marked `Failed` with the reason |
| Infrastructure | `LoggingNotificationSender` | Writes the message to `ILogger`, used when `SendGrid:ApiKey` is empty (local dev, tests) |
| Infrastructure | `ExpiryNotificationJob` | Hangfire job that calls `RunAsync`. `[DisableConcurrentExecution]` keeps two runs from overlapping |
| Infrastructure | `RecurringJobs.Register` | `membership-expiry-notifications`, `Cron.Never()`: registered but never scheduled, so it runs only when triggered by hand (`POST /api/notifications/run` or **Trigger now** on `/hangfire`) |
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
"SendGrid": { "ApiKey": "", "FromAddress": "", "FromName": "Fitness Club" }
```

- **Email only.** Every client has an email address (required since [[2026-10-09-client-contacts-and-page-design]]), so new notices are always on the `Email` channel. `NotificationChannel.Sms` stays in the enum for older rows (the seeded history has some).
- **Choosing the sender.** `AddInfrastructure()` registers `SendGridNotificationSender` (typed `HttpClient`) when `SendGrid:ApiKey` is set and `LoggingNotificationSender` otherwise. `FromAddress` is required once `ApiKey` is set, checked at startup, and must be a verified sender in SendGrid.
- **Deploy.** `deploy/docker-compose.yml` passes `SENDGRID_*` variables, all optional (an empty `SENDGRID_API_KEY` means log only). `deploy/docker-compose.server.yml` requires them.

The job has no schedule (`Cron.Never()`, fixed in code). To run it daily again, change the cron in `RecurringJobs.Register`.

## Tests

- **Unit** (`ExpiryNotificationServiceTests`, fakes only): window edges and configured window, no duplicates across runs, renewed / cancelled / short or not-started passes skipped, send success and failure with the run continuing, retry, list filter, run.
- **Integration** (`Notifications/`): HTTP list / filter / retry / run, 400 / 401 / 403 / 404 cases; startup registers the recurring job with `Cron.Never()`; triggering the job through Hangfire creates and sends a notice; an invalid `ExpiryNoticeDays` fails startup; the sender is `LoggingNotificationSender` without a SendGrid API key and `SendGridNotificationSender` with one, and a key without `FromAddress` fails startup.
- **SendGrid** (`SendGridNotificationSenderTests`): a stub `HttpMessageHandler` checks the URL, bearer key, from, to, subject and both content parts; text only when there is no HTML; a non-2xx response throws with SendGrid's reason.

## Known gaps

- No SMS channel. Email is required, so every client can get the notice. An SMS provider would be another `INotificationSender` plus restoring the SMS fallback in `Notification.MembershipExpiring`.
- A retried older `Sms` notice fails again with the SendGrid sender.
- Each notice is one API call. That is fine for a few notices a day, but bulk sending should use SendGrid's batch personalizations.
- `POST /run` and the Hangfire job can overlap: `[DisableConcurrentExecution]` only guards job runs. Overlap at worst sends a notice twice, or one run gets a 409 on a stale `Version`.
- A notice that is pending when the client renews or cancels is still sent. There is no "cancelled" notification status.
- The list endpoint has no paging.
