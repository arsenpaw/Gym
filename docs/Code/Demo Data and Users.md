# Demo data and users

## Mock data

The `SeedMockData` EF migration fills a new SQL Server database with demo data, generated in `api/src/FitnessClub.Infrastructure/Persistence/Migrations/MockData.cs`. The InMemory database (no connection string) stays empty.

Dates are relative to the moment the migration runs, so the data always looks current: memberships expire soon, sessions run this week, and revenue spreads over the last months.

| Area | What is seeded |
|---|---|
| Membership plans | Single visit, Monthly unlimited, Monthly 12 visits, Quarterly unlimited, Yearly unlimited, and an inactive Student monthly |
| Rooms | Main gym floor, Yoga studio, Cycling studio, Personal training room, and an inactive Old aerobics hall |
| Trainers | Olena Kovalenko (yoga), Dmytro Shevchenko (strength), Iryna Bondarenko (cycling, HIIT), each with working hours and assigned clients, and an inactive Andrii Melnyk |
| Clients | 16 clients covering every membership state: active, expiring in the next week, expired, cancelled, visits used up, starting in the future, renewed in advance, and none |
| Visits and payments | A check-in history and one payment per membership |
| Schedule | Group classes and individual sessions from 4 weeks back to 2 weeks ahead, with bookings. One upcoming HIIT class is cancelled |
| Notifications | One sent and one failed expiry notice. The daily job adds notices for the memberships that expire soon |

Every seeded id starts with `5eed`. Rolling back the migration deletes only those rows and the rows that belong to them.

## Demo users

Create these users in Auth0 (User Management → Users, connection `Username-Password-Authentication`) and assign the matching role. These are shared test accounts, so use them only in the dev tenant.

| Role | Email | Password | Auth0 user id | Notes |
|---|---|---|---|---|
| Admin | `admin.demo@example.com` | `4T9w8XKtgGxe#93` | `auth0\|6ac79476749f077135998951` | Everything, including reports, notifications and setup |
| Receptionist | `reception.demo@example.com` | `7UNY3Nqjisgm#73` | `auth0\|6ac7948a4fb3adf72cd1939e` | Check-in, clients, memberships, schedule and bookings |
| Trainer | `olena.kovalenko@example.com` | `V2adqbrHTize#11` | `auth0\|6ac7949ea61c223ad590f4be` | The seeded trainer Olena Kovalenko. Sees only her own schedule, read-only |

The mock data links the seeded trainer Olena Kovalenko to the Trainer user's Auth0 user id, so she sees "My schedule" right away. If the Auth0 user is ever recreated, it gets a new id: update it in `MockData.cs` for new databases, or sign in as the admin and change it on Trainers → Olena Kovalenko.
