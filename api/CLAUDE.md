# API (C# .NET)

Backend API for the fitness club system. It is the only owner of domain logic and data. See the root `CLAUDE.md` for the project overview.

## Responsibilities

- Clients and their memberships.
- Visit (check-in) records.
- Trainers: profiles, specializations, work schedules, assigned clients.
- Bookings for group classes and individual sessions.
- Membership plans (single visit, monthly, yearly, and so on).
- Membership expiry notifications, run as a background/scheduled process.
- Reports: client visit activity, revenue per month and year, trainer and room load by day.

## Container

The API has its own `Dockerfile` in `api/`. `deploy/` uses it.

## Project layout

TBD

## Commands

TBD: build, run, test, and how to run a single test.

## Persistence

TBD

## Authentication

TBD

## Notification channel

TBD
