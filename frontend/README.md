# Humanitarian Platform - web client (React)

Web interface for citizens and staff: sign-in, an API client, role-based routes, the citizen "ask for help" form,
shelters, volunteers, tasks, resources, users and the audit log. React 18 + Vite + React Router. Plain JavaScript, no UI framework.

## Run it

You need Node.js 18 or newer and the API running. Run `npm install` again after updating: Phase 4 added the `leaflet` map library.

```powershell
# 1. terminal 1 - the API (from the repository root)
dotnet run --project src\Humanitarian.Api --launch-profile https

# 2. terminal 2 - this client
cd frontend
npm install
npm run dev
```

Open http://localhost:5173. In development the sign-in page lists the demo accounts created by the seeder.

`npm test` runs the unit tests (validation, date handling, error mapping). They need no `npm install`.
`npm run build` makes a production build in `dist/`.

> The seeded database has **no crisis event, shelter or volunteer**. A good first run:
> 1. `coordinator@demo.org` creates a crisis event (Crises).
> 2. `citizen@demo.org` submits a request, and registers as a volunteer (Volunteer).
> 3. `casemanager@demo.org` opens the request, approves the AI recommendation, then "Suggest shelters".
> 4. `shelter@demo.org` adds a shelter (Shelters); `volunteers@demo.org` verifies the volunteer and creates a task.
> 5. `logistics@demo.org` adds stock (Resources); the coordinator reserves it for the request ("Suggest supplies"),
>    then logistics dispatches and delivers it (Allocations).
> 6. `admin@demo.org` creates a staff account (Users) and reads what happened in the Audit log.

## How the pieces fit

| Path | What it does |
|---|---|
| `src/api/client.js` | One `fetch` wrapper: adds the Bearer token, turns every failure into an `ApiError`, signs out on 401 |
| `src/api/errors.js` | Maps RFC 7807 ProblemDetails (and ASP.NET validation errors) to messages and per-field errors |
| `src/api/endpoints.js` | One small object per API controller; components never build URLs |
| `src/auth/` | `AuthContext` (session, login, register, auto sign-out), `session.js` (storage), `roles.js` (roles and menus), `ProtectedRoute` |
| `src/lib/validation.js` | Form rules that mirror the API's DataAnnotations, and the request payload builder |
| `src/hooks/` | `useApi` (load data), `useAction` (button actions with busy/error), `useForm` (form state) |
| `src/components/` | Layout, shared UI pieces, `PromptButton` (one-field confirm), review and shelter-placement panels |
| `src/pages/` | `citizen/`, `staff/`, `shared/` screens, plus Login, Register, Forbidden, NotFound |

## Adding a screen for your module

1. Add the call to `src/api/endpoints.js`.
2. Create the page under `src/pages/<area>/`.
3. Add a `<Route>` in `src/App.jsx` inside the right `ProtectedRoute roles={...}` block.
4. Add a menu entry in `src/auth/roles.js` (`NAV_ITEMS`) and, for staff, a card in `StaffHome.jsx`.

Keep the role lists identical to the API's `[Authorize(Roles = ...)]` for that endpoint.
