# UI (React SPA)

React single-page app for the fitness club staff. It gets all of its data from the API (`api/`) over HTTP and has no direct database access. See the root `CLAUDE.md` for the project overview. The design is in `docs/Code/Specs/2026-10-07-ui-design.md`.

## Screens (from requirements)

- Check-in: find a client and record a visit.
- Clients and memberships: list, register, edit, sell and cancel memberships, visit history, and the Messages card (preview and send the expiry reminder or promotion email).
- Trainers: profile, working hours, assigned clients, Auth0 login link.
- Schedule: calendar of group and individual sessions, scheduling, bookings. Trainers see their own sessions, read-only.
- Rooms and membership plans.
- Notifications (expiry notices, reminders and promotions) and reports: client activity, revenue, trainer and room load.

## Container

The UI has its own `Dockerfile` in `ui/`. It builds with Node and serves with unprivileged nginx on port 8080. nginx proxies `/api/` to the `api` service. `deploy/` uses it.

## Tooling and build

- Vite 8, React 19, TypeScript 6, oxlint. Node 24.
- **Libraries only, no custom widgets.** Mantine 9 for UI, mantine-datatable for tables, react-big-calendar for the schedule, `@mantine/charts` for charts, Tabler icons. If a component is missing, find a library before writing one.
- **API client is generated.** `orval.config.ts` reads `openapi/fitnessclub.json` and writes `src/api/generated/` (git-ignored): types, axios calls, TanStack Query hooks, zod schemas and MSW handlers. Never edit generated files.
- `openapi/fitnessclub.json` is written by a Debug build of the API (`dotnet build` in `api/`). After an API change, rebuild the API, then run `npm run generate`. Commit the JSON file.

## Commands

Run from `ui/`. The first time, run `npm install` and `cp .env.example .env.local`, then fill in the Auth0 values.

```sh
npm run dev            # http://localhost:5173, proxies /api to API_PROXY_TARGET (default http://localhost:5080)
npm test               # all tests (generates the client first)
npm test -- src/features/clients              # one folder
npm test -- src/features/clients/ClientsPage.test.tsx -t "registers a client"   # one test
npm run typecheck
npm run lint
npm run build          # dist/
npm run generate       # regenerate src/api/generated from openapi/fitnessclub.json
```

## Auth0

- The UI is an Auth0 **Single Page Application**. Allowed Callback URLs, Logout URLs and Web Origins: `http://localhost:5173` and `http://localhost:8081`.
- `VITE_AUTH0_AUDIENCE` must equal the API's `Auth0:Audience`. Roles come from the access token's `https://fitnessclub/roles` claim (the post-login Action in `api/CLAUDE.md`).
- A trainer sees "My schedule" only after an admin links their Auth0 user id on the trainer page.

## State and data fetching

- Server state lives only in TanStack Query, through the generated hooks (`useClientsList`, `useSessionsBook`, …). No `useEffect` fetching and no hand-written DTO types.
- After a write, invalidate the generated query keys it affects (`getClientsGetQueryKey(id)`, …). Sessions use `invalidateSessions` (prefix `/api/sessions`).
- Mutation errors show a notification from `MutationCache.onError`. Set `meta: { errorTitle }` on a mutation for a better title. The text comes from `problemMessage` (problem `detail` first).
- Local UI state (open modals, filters) uses `useState` / `useDisclosure`.

## Forms

- react-hook-form with `zodResolver`, and inputs from `react-hook-form-mantine` (`<TextInput control={control} name="..." />`).
- Each form schema `.extend()`s the generated zod request schema and only tightens it. Schemas must not change types (no `transform`). Map values to the request with a `toXRequest()` function, for example empty optional text to `null`.
- Submit with `await mutation.mutateAsync(...).catch(() => undefined)`, and give the button `loading={formState.isSubmitting}`, so a double click sends one request.
- Date fields are `<TextInput type="date" />` (the browser's native date control). Its value is always a full real date (`YYYY-MM-DD`) or `''`, so an impossible or half-typed date can never be saved as another date. Don't use Mantine's `DateInput` in forms: when the typed text isn't a date it keeps the previous value. Required dates check `^\d{4}-\d{2}-\d{2}$` with an "Enter a full date…" message, and zod validates ranges. Don't give a date field a meaning for "empty" (an impossible date is also `''`); prefill it instead, as the membership start date is prefilled with today.

## Styling

- Mantine theme in `src/app/theme.ts`: teal primary, `md` radius, Inter font, light, dark and auto color scheme. Use Mantine props and components. There are no CSS modules. The only global CSS besides the library styles imported in `main.tsx` is `src/app/calendarTheme.css`, which maps react-big-calendar's hardcoded light colors to Mantine CSS variables so the calendar works in dark mode. Vitest processes only that CSS file (`css.include` in `vite.config.ts`), so its test can read it.

## Tests

- Vitest + Testing Library + MSW. Use the generated `get…MockHandler(response)` helpers with fixed data from `src/test/fixtures.ts`.
- `renderPage(<Page />)` or `renderRoute(routes, path)` from `src/test/render.tsx`. `signInAs('Admin')` sets the roles in the fake Auth0 token.
- Mantine runs with `env="test"` (no transitions or portals). Find a `Select` with `getByRole('combobox', { name })` and a `SegmentedControl` option with `getByRole('radio', { name })`.
