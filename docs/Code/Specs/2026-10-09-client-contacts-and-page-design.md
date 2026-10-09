---
tags: [spec, api, ui, clients]
status: approved
date: 2026-10-09
---

# Email as Primary Contact and the Client Page: Design Spec

Clients are contacted by email: expiry notices, reminders and promotions all go through Twilio SendGrid ([[2026-10-08-client-messages-design]]). Until now email was optional and phone was required and unique, so a client registered without an email silently got no notices. This change makes email the required primary contact and reshapes the client page around it.

## Decisions

- **Email is required and unique.** `Client.Email` is an `EmailAddress` and no longer nullable. The unique index moves from `Clients.Phone` to `Clients.Email`. Emails are stored lower-case, so the check ignores case.
- **Phone is optional and not unique.** `Client.Phone` is a `PhoneNumber?`. Family members can share a phone.
- **Clients only.** Trainers keep a required, unique phone and an optional email.
- **Visits are paged on the server.** A long-time member has hundreds of visits, so the page loads one page at a time.
- **The email preview fits its content.** No inner scrollbar. Sent emails are still not reopened from the history table.

## API

| Change | Detail |
|---|---|
| `ClientRequest` | `email` required (≤ 254), `phone` optional (≤ 32 characters of input; blank is stored as none) |
| `POST/PUT /api/clients` | 409 `A client with email '…' already exists.` when another client has the email (`IClientRepository.EmailExistsAsync`) |
| `GET /api/clients/{id}/visits?page=1&pageSize=10` | 200 `VisitPageResponse { items, totalCount }`, newest first. `page` ≥ 1 (default 1), `pageSize` 1–100 (default 10), otherwise 400 |
| Responses | `email` is a string and `phone` is nullable in `ClientSummaryResponse`, `ClientDetailsResponse` and the client activity report item, which also gains `email` |

Domain:
- `Client.Register` and `UpdateProfile` take `(name, dateOfBirth, email, phone?, now)`.
- `Client.NeedsExpiryNotice` no longer checks for an email.
- `Notification` no longer throws "The client has no email address."

`IVisitRepository.ListForClientAsync(clientId, skip, take)` and `CountForClientAsync(clientId)` back the paging.

## Migration

`ClientEmailRequired`:
1. Backfills existing rows that have no email with `client-<id>@example.com`.
2. Drops the unique phone index and makes `Phone` nullable.
3. Makes `Email` NOT NULL and adds a unique index on it.

`Down` gives clients without a phone a placeholder `+999…` number before restoring the old shape.

`MockData` gives every seeded client an `@example.com` address, so all seeded expiry notices are on the `Email` channel.

## UI

- **Client form:** Email first and required ("Expiry notices and messages are sent here"), then an optional Phone and the date of birth. `requiredEmail` and `optionalPhone` in `lib/formSchemas.ts`.
- **Lists and pickers:**
  - The Clients table shows Email before Phone and searches both.
  - Check-in and the session booking picker label clients `name · email · phone` (the phone only when present).
  - The client activity report has an Email column.
- **Client page** (`/clients/:id`) is a header card plus tabs.
  - `ClientHeader`:
    - avatar initials, name and the membership badge;
    - email, phone (only when present), date of birth with age, and the registration date;
    - a one-line summary of the current membership;
    - Edit, Sell membership and Check in buttons.
  - Tabs `Memberships (n) | Visits (total) | Messages`. The active tab is kept in `?tab=` like the Reports page, and inactive panels are unmounted, so the email preview loads only when the tab is opened.
  - `ClientVisitsTable` uses mantine-datatable server paging with 10, 25 or 50 rows per page. A check-in jumps back to page 1.
  - `ClientMessagesPanel` is the old Messages card without its card frame.
  - `EmailPreviewFrame` sizes its iframe to the email's `body.scrollHeight` on load and when its width changes, with a minimum of 200px.
    - It uses `sandbox="allow-same-origin"`, so the page can measure the email while scripts stay blocked.
    - The template's own gray backdrop frames it.

## Testing

- **Unit:**
  - registering without a phone;
  - updating the email and clearing the phone;
  - notices no longer depend on an email.
- **Integration:**
  - a missing email → 400;
  - a duplicate email in another case → 409;
  - no phone → 201;
  - a shared phone is allowed;
  - visits paging and invalid paging → 400;
  - `EmailExistsAsync`, and repository paging with the count.
- **UI:**
  - form schemas;
  - the registration form requires email but not phone;
  - search by email;
  - the header with and without a phone;
  - the tabs, and requesting page 2 of the visits;
  - the preview in the Messages tab.
