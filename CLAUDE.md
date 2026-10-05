# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What we're building

An information system that automates the day-to-day work of a fitness club. It covers:

- **Clients**: full name, age, phone, membership, and the membership's expiry date.
- **Visits**: every client check-in is recorded.
- **Trainers**: a profile for each trainer with specialization, work schedule, and client list.
- **Bookings**: sign-ups for group classes and individual sessions.
- **Membership plans**: configurable plans such as single visit, monthly, and yearly.
- **Expiry notifications**: clients are notified automatically before their membership expires.
- **Reports**: client list with visit activity, club revenue per month and year, and trainer and room load by day.

The source of truth for requirements is `docs/Requirements/Fitness Club System.md`.

## Repository map

Each folder has its own CLAUDE.md with the details for that area. This file covers only facts that apply across the whole project.

- `api/`: C# .NET backend API.
- `ui/`: React single-page app.
- `docs/`: Obsidian vault with requirements and code documentation.
- `deploy/`: Docker / docker compose deployment.

## Architecture

- The UI talks to the API over HTTP only. It never accesses data storage directly.
- The API owns all business logic, data storage, scheduled jobs (expiry notifications), and reports.
- Each service is built as its own Docker image, and docker compose runs the whole stack.

## Status

- **Backend (`api/`):** foundation built, with membership plans as the first feature. Stack and commands are in `api/CLAUDE.md`. Next backend parts: clients/memberships/visits, trainers/bookings, expiry notifications, reports. Each gets its own spec in `docs/Code/Specs/`.
- **UI (`ui/`):** not started. Its libraries and tooling are TBD. Ask before choosing any.
- **Specs and plans:** design specs go in `docs/Code/Specs/` and implementation plans in `docs/Code/Plans/`, not in Superpowers' default `docs/superpowers/`.

When requirements or behavior change, update the matching note in `docs/`.
