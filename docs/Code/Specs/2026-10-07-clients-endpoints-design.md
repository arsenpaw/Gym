---
tags: [spec, api, clients]
status: implemented
date: 2026-10-07
---

# Clients, Memberships and Visits Endpoints: Design Spec

Requirements source: [[Fitness Club System]]. Builds on the `Client`, `Membership`, `Payment` and `Visit` aggregates from [[2026-10-05-domain-model-and-architecture-design]] (sub-project 2). No domain or repository changes were needed.

## Goal

Let the front desk register clients, keep their profiles current, sell and cancel memberships, and check clients in, with every visit recorded.

## Layers

- **Application:** `IClientService` and the internal `ClientService` in `Application/Clients`, registered in `AddApplication()`. It depends on `IClientRepository`, `IMembershipPlanRepository`, `IPaymentRepository`, `IVisitRepository`, `IUnitOfWork` and `TimeProvider`. Each use case calls `SaveChangesAsync` exactly once.
- **Api:** `ClientsController` at `api/clients`. It injects only `IClientService`.
- **Time:** "now" is `TimeProvider.GetLocalNow()` (club-local) and "today" is its date. Ages, active memberships and `IsActive` are computed for today on every read.

## Endpoints

Every endpoint requires the `Admin` or `Receptionist` role (class-level `[Authorize]`). Anonymous callers get 401 and `Trainer`-only users get 403.

| Method | Route | Body | Success | Use case |
|---|---|---|---|---|
| GET | `/api/clients` | | 200 `ClientSummaryResponse[]` | List, ordered by last name, then first name |
| GET | `/api/clients/{id}` | | 200 `ClientDetailsResponse` | Details with every membership |
| POST | `/api/clients` | `ClientRequest` | 201 `ClientDetailsResponse`, `Location: /api/clients/{id}` | `Client.Register` |
| PUT | `/api/clients/{id}` | `ClientRequest` | 200 `ClientDetailsResponse` | `Client.UpdateProfile` |
| POST | `/api/clients/{id}/memberships` | `PurchaseMembershipRequest` | 201 `MembershipResponse`, `Location: /api/clients/{id}` | `Client.PurchaseMembership`, and the returned `Payment` is added to `IPaymentRepository` in the same unit of work |
| POST | `/api/clients/{id}/memberships/{membershipId}/cancel` | | 204 | `Client.CancelMembership` |
| POST | `/api/clients/{id}/visits` | | 201 `VisitResponse`, `Location: /api/clients/{id}/visits` | `Client.CheckIn`, and the returned `Visit` is added to `IVisitRepository` in the same unit of work |
| GET | `/api/clients/{id}/visits?page=1&pageSize=10` | | 200 `VisitPageResponse`, newest first | `IVisitRepository.ListForClientAsync(skip, take)` and `CountForClientAsync` |

## Requests

`ClientRequest`:

| Field | Shape rule (400 from `[ApiController]`) | Domain rule (400 from `DomainException`) |
|---|---|---|
| `firstName`, `lastName` | required, ≤ 100 | not blank after trim |
| `middleName` | optional, ≤ 100 | blank is stored as none |
| `dateOfBirth` | required, `yyyy-MM-dd` | not in the future, age ≤ 120 |
| `email` | required, ≤ 254 | valid address; stored lower-case; the client's primary contact |
| `phone` | optional, ≤ 32 characters of input | 10–15 digits, plus spaces, dashes, parentheses and a leading `+`; blank is stored as none; stored normalized; may repeat across clients |

Visits query: `page` ≥ 1 (default 1), `pageSize` 1–100 (default 10).

`PurchaseMembershipRequest`:

| Field | Rule |
|---|---|
| `planId` | required |
| `startsOn` | optional, defaults to today; must not be in the past |
| `paymentMethod` | required, `"Cash"` or `"Card"` (a JSON string enum on this property; integers are accepted too, and an undefined value is a domain 400) |

## Responses

- `ClientSummaryResponse`: `id`, `fullName`, `age`, `email`, `phone` (or `null`), `activeMembership` (or `null`).
- `ActiveMembershipResponse`: `id`, `planName`, `startsOn`, `endsOn`, `visitsLeft` (`null` means unlimited). It is `Client.ActiveMembershipOn(today)`: the active membership that ends first.
- `ClientDetailsResponse`: `id`, `firstName`, `lastName`, `middleName`, `fullName`, `dateOfBirth`, `age`, `email`, `phone` (or `null`), `registeredAt`, `activeMembership`, `memberships` (newest start first).
- `MembershipResponse`: `id`, `planId`, `planName`, `price`, `startsOn`, `endsOn`, `visitLimit`, `visitsUsed`, `visitsLeft`, `purchasedAt`, `cancelledAt`, `isActive` (for today).
- `VisitResponse`: `id`, `clientId`, `membershipId`, `checkedInAt`.
- `VisitPageResponse`: `items` (`VisitResponse[]`, one page) and `totalCount`.

## Errors

All error bodies are `application/problem+json`.

| Status | When |
|---|---|
| 400 | Request shape (missing or malformed fields, unknown enum name, invalid JSON, visits `page` or `pageSize` out of range). Domain rules: invalid phone or email, future birth date, inactive plan, start in the past, overlapping membership, cancelling twice or after the end, check-in without an active membership, a second check-in on the same day. |
| 404 | Unknown client id, unknown plan id on purchase, or a membership id that doesn't belong to the client on cancel. The service checks ownership first, so this is 404 and not the domain's 400. |
| 409 | Email already used by another client (the service checks `EmailExistsAsync` on the lower-cased email, excluding the client being updated). Since [[2026-10-09-client-contacts-and-page-design]] the email is unique, not the phone. A stale concurrent save of the same client. |

## Testing

- **Unit (`ClientServiceTests`):** the service against in-memory fakes (`InMemoryClientRepository`, `InMemoryPaymentRepository`, `InMemoryVisitRepository`) and a `FakeTimeProvider`. They cover every use case, the single save per use case, and that a payment and a visit are added with the client change.
- **Integration (`ClientsEndpointsTests`):** HTTP happy paths for every endpoint, 400 for shape and domain rules, 404, 409 for duplicate emails, visits paging, 401 when anonymous and 403 for a `Trainer`.

## Not covered

- Search, filtering and paging on the client list.
- Refunding a cancelled membership's payment.
- Deleting or archiving clients.
