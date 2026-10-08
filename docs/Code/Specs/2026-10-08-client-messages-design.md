---
tags: [spec, api, ui, notifications]
status: approved
date: 2026-10-08
---

# Client Messages over Twilio SendGrid: Design Spec

All client email goes through **Twilio SendGrid**, and the club's own SMTP (MailKit) is removed. There are two branded HTML templates:

- an **expiry reminder**, used by the daily job and by staff
- a **promotion** (10% off any membership, valid until the end of the month), sent by staff only

Staff open a client's page, choose a template, see the real email rendered with that client's data, and send it with one click. Every send is stored as a `Notification`. This replaces the SMTP part of [[2026-10-07-expiry-notifications-design]].

## Decisions

- **SendGrid only.** It is reached through SendGrid's v3 Mail Send REST API (`POST https://api.sendgrid.com/v3/mail/send`) with an API key, using a typed `HttpClient`. There is no SDK package: it would add dependencies for a single JSON POST. Without an API key, notices are only logged (`LoggingNotificationSender`), as before. SMS is out of scope.
- **Templates in our repo.** The two HTML files are embedded resources in Infrastructure. The API fills them and sends finished HTML plus a plain-text part. SendGrid Dynamic Templates are not used, so the in-app preview is exactly the email that goes out, and the templates are versioned and tested.
- **Fixed text.** There is no template editor and no staff input. The promotion is always 10% off, valid until the last day of the current month.
- **Email only.** A client without an email address can't be messaged.
- **Kept as history.** Each send is a `Notification`. It shows on the Notifications page, can be retried there, and is listed on the client page.
- **Roles.** Admin and Receptionist, the same as the rest of the client page.

## Style of the templates

The templates match the staff portal:

- Layout: email-safe 600px table layout with inline CSS only.
- Fonts: the system font stack with Inter first.
- Header: a teal `#0ca678` band with a "Fitness Club" wordmark. There's no SVG logo, because Gmail strips SVG.
- Body: a white rounded card on `#f1f3f5`, and a small grey footer.

The two templates:

- **Expiry reminder** (`ExpiryReminder.html`): the headline "Your membership ends in N days", a box with the plan name and end date, and "Renew at the reception desk to keep training without a break."
- **Promotion** (`Promotion.html`): a large teal "10% OFF" badge, "on any membership", the valid-until date, and "Show this email at the reception desk."

Every value inserted into a template is HTML-encoded.

## Domain

- `NotificationContent(Subject, Text, Html?)` value object:
  - subject required, ≤ 200 characters
  - text required, ≤ 1000 characters
  - HTML optional
- `Notification` gains:
  - `Subject` (required, ≤ 200)
  - `HtmlBody` (optional, unlimited)
  - `Message` stays the plain-text body.
- `NotificationType` gains `ExpiryReminder` (sent by staff) and `Promotion`.
- Factories take the content instead of writing the wording themselves:

| Factory | Rule |
|---|---|
| `MembershipExpiring(client, membership, content, now)` | Unchanged: the client owns the membership and it needs an expiry notice |
| `ExpiryReminder(client, membership, content, now)` | The client has an email, owns the membership, and the membership is active today. `MembershipId` stays null, so the daily job's unique `(MembershipId, Type)` index never blocks it |
| `Promotion(client, content, now)` | The client has an email |

## Application

- `INotificationSender.SendAsync(Notification, CancellationToken)`. The sender reads the channel, recipient, subject, text and HTML from the notification.
- `IEmailTemplates` builds the subject, text and HTML of each template:
  - `ExpiryReminder(ExpiryReminderEmail(FirstName, PlanName, EndsOn, DaysLeft))`
  - `Promotion(PromotionEmail(FirstName, DiscountPercent, ValidUntil))`
  - The daily job uses the reminder template.
- `IClientMessageService` (`Application/ClientMessages/`):
  - `PreviewAsync(clientId, template)`:
    - Loads the client (404 if missing).
    - For a reminder, picks the active membership. With none: `DomainException("The client has no active membership to remind about.")`.
    - Builds the notification without adding it and returns `ClientMessagePreviewResponse(template, recipient, subject, html)`.
  - `SendAsync(clientId, template)`:
    - Builds the notification the same way, adds it, saves, then sends.
    - Success → `MarkSent`. An exception that isn't a cancellation → `MarkFailed(reason)`. Then it saves again.
    - Returns the `NotificationResponse`.
  - `ListAsync(clientId)`: every notification of the client, newest first.
- `NotificationResponse` gains `Subject`.

## API

`ClientMessagesController`, route `api/clients/{id}/messages`, Admin and Receptionist.

| Method | Path | Result |
|---|---|---|
| GET | `/preview?template=ExpiryReminder\|Promotion` | 200 `{ template, recipient, subject, html }`. 400 for a missing or unknown template, no client email, or (reminder) no active membership. 404 for an unknown client |
| POST | `/` with body `{ template }` | 200 with the stored `NotificationResponse` (`Sent`, or `Failed` with SendGrid's reason). 400 and 404 as above |
| GET | `/` | 200, the client's notifications, newest first. 404 for an unknown client |

## Infrastructure

- `SendGridOptions`, section `SendGrid`:
  - `ApiKey`
  - `FromAddress`: must be a verified sender in SendGrid, and is required when `ApiKey` is set (checked at startup)
  - `FromName`: default `Fitness Club`
- `SendGridNotificationSender`:
  - Posts the v3 Mail Send JSON with `Authorization: Bearer <ApiKey>` through a typed `HttpClient` (base address `https://api.sendgrid.com/`, 30 s timeout).
  - Sends `text/plain` and, when the notification has HTML, `text/html`.
  - A non-2xx response throws with the status and response body, so the notice is stored as `Failed` with the reason.
  - Channels other than email throw.
- No API key → `LoggingNotificationSender`.
- A migration adds `Subject` (existing rows get `Your membership expires soon`) and a nullable `HtmlBody`. Old rows without HTML are sent as text only.
- `SmtpNotificationSender`, `SmtpOptions`, MailKit and the Mailpit test fixture are removed.

## UI

`ClientMessagesCard` on `ClientDetailsPage`, below Visits:

- **Template:** a `SegmentedControl` with `Expiry reminder | Promotion`.
- **Preview:** To and Subject, then the HTML in a sandboxed `<iframe srcDoc>`.
  - A 400 shows its reason in an `Alert` and disables **Send**.
- **Send:** a confirm modal ("Send this email to {recipient}?"), then POST.
  - `Sent`: a teal toast "Email sent".
  - `Failed`: a red toast with the reason.
  - Afterwards the client's messages and the Notifications list refresh.
- **Sent messages:** a table with Created, Type (Expiry notice / Reminder / Promotion), Subject, Status, Sent.

The Notifications page shows the new type labels and a Subject column.

## Configuration

| Setting | Env (compose) | Notes |
|---|---|---|
| `SendGrid:ApiKey` | `SENDGRID_API_KEY` | Empty → notices are only logged. Required on the server |
| `SendGrid:FromAddress` | `SENDGRID_FROM_ADDRESS` | A verified single sender or an address on an authenticated domain |
| `SendGrid:FromName` | `SENDGRID_FROM_NAME` | Default `Fitness Club` |

## Testing

- **Unit:**
  - Factories: rules, content copied, no membership id on manual messages.
  - `NotificationContent` limits.
  - Templates: values filled and HTML-encoded, no `{{` left, the promotion's valid-until in December and February.
- **Integration:**
  - SendGrid sender against a stub `HttpMessageHandler`: URL, bearer key, from/to/subject, both content parts, non-2xx → exception.
  - Host configuration: logging without a key, SendGrid with a key, a key without a from address fails startup.
  - Repository: subject/HTML round-trip, `ListForClientAsync`.
  - Service: preview doesn't save, send → Sent or Failed, unknown client, list.
  - HTTP: Admin and Receptionist allowed, Trainer 403, anonymous 401, 400s, 404.
- **UI (Vitest):**
  - preview and template switch
  - a 400 disables Send
  - confirm → toasts
  - the history table

## Known gaps

- No send limit. Only the confirm modal stops staff from sending the same promotion several times.
- The send runs inside the request, so a slow SendGrid response can make it wait up to 30 s.
- A manual `Pending` message could be picked up by the daily job at the same moment. The `Version` check makes one save fail, and at worst the email goes out twice.
- Without an API key, messages are marked `Sent` even though only the log received them.
