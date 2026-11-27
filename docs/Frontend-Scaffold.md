# Web client (React)

## What it does
A web client for the platform. People sign in or register; citizens submit and follow assistance requests, look at
shelters and can volunteer; staff work through role-specific screens: requests, shelters, supply requests,
volunteers, tasks, resources and allocations, user management and the audit log. Phase 4 adds the notification inbox,
emergency announcements and the message queue, interactive maps, and the operations dashboard.

## Screens
| Route | Roles | Purpose |
|---|---|---|
| `/login`, `/register` | public | Sign in; citizen self-registration (signs the new user in) |
| `/citizen` | Citizen | My requests (paged) |
| `/citizen/requests/new` | Citizen | The request form |
| `/citizen/requests/:id` | Citizen | Status in plain words and the request timeline |
| `/citizen/shelters` | Citizen | Open shelters with coarse availability only |
| `/citizen/volunteer` | Citizen | Register as a volunteer, skills, free time, accept or decline assignments |
| `/staff` | every staff role | Dashboard: one card per module the role may use, with a live number |
| `/staff/requests`, `/staff/requests/:id` | CaseManager, EmergencyCoordinator | Priority queue; detail with AI recommendation, decision, duplicates, shelter suggestions |
| `/staff/crises` | EmergencyCoordinator, SystemAdministrator | Create and list crisis events |
| `/staff/shelters`, `/staff/shelters/:id` | ShelterAdministrator, EmergencyCoordinator, CaseManager (view) | Occupancy, edit, open/close, households placed, check-out |
| `/staff/shelters/new` | ShelterAdministrator, EmergencyCoordinator | Add a shelter |
| `/staff/supply-requests` | ShelterAdministrator, EmergencyCoordinator, LogisticsManager | Shelters ask for supplies; logistics marks them done |
| `/staff/volunteers` | VolunteerCoordinator, EmergencyCoordinator, CaseManager (view) | Verify volunteers, activate/deactivate |
| `/staff/tasks`, `/staff/tasks/:id`, `/staff/tasks/new` | VolunteerCoordinator, EmergencyCoordinator (create, assign); CaseManager (view) | Create tasks, ranked volunteer suggestions, assign, complete, cancel |
| `/staff/resources`, `/staff/resources/new` | LogisticsManager, EmergencyCoordinator (manage); CaseManager (view) | Inventory, low-stock flag, restock, activate/deactivate, add item |
| `/staff/allocations` | LogisticsManager, EmergencyCoordinator (manage); CaseManager (view) | Allocations per crisis: dispatch, deliver, cancel; shortage plan preview and apply (coordinator only) |
| `/notifications` | every signed-in user | Inbox: unread highlighted, mark read, open the request or task; the bell in the header shows the unread count |
| `/staff/operations` | CaseManager, EmergencyCoordinator, SystemAdministrator | Operations dashboard: unresolved/overdue cases, response times, AI override rate, resources, shelters, coverage, delivery health, workload |
| `/staff/map` | ShelterAdministrator, EmergencyCoordinator, CaseManager | Open requests (colour = urgency) and shelters (colour = space left) on one map |
| `/staff/communications` | EmergencyCoordinator, SystemAdministrator | Emergency announcements, and the e-mail / SMS queue with retry for failed messages |
| `/staff/users` | SystemAdministrator | Create staff accounts, activate/deactivate (not your own account) |
| `/staff/audit` | SystemAdministrator | Read-only audit log with filters and paging |

## Design decisions (viva)
- **Route guards are for usability, not security.** `ProtectedRoute` and the role-filtered menu stop people from
  wandering into screens that would fail. The API still checks the JWT role on every call, so editing the client
  gives a 403 and nothing else. The role lists in `auth/roles.js` copy the API's role constants
  (`RequestRoles`, `ShelterRoles`, `VolunteerRoles`).
- **Where the token lives: `sessionStorage`.** It is cleared when the tab closes and is not shared between tabs. The
  cost is that script injection could read it, which React's output escaping and the 60-minute token lifetime reduce.
  A production system would use an httpOnly cookie with refresh tokens.
- **Expired tokens.** The client signs the user out when the token's expiry time passes, and also on any 401 from a
  protected call, then returns them to the page they wanted after they sign in again.
- **Idempotent submission.** The request form creates one `idempotencyKey` per submission attempt and re-sends the same
  key on retries, so a lost response never creates a duplicate (HTTP 200 returns the existing request).
- **One error path.** The API reports problems as RFC 7807 ProblemDetails. `api/errors.js` turns them into one
  `ApiError`; validation errors are mapped back onto the fields. 409 (a shelter that filled up meanwhile, a shelter
  that still has people and cannot be closed), 403 with an empty body and 429 get readable messages.
- **Validation twice.** `lib/validation.js` repeats the API's rules for instant feedback. The API remains the authority.
- **Humans decide.** The AI recommendation, the shelter ranking and the volunteer ranking are shown as advice with their
  reasons and warnings. Only a button press by a staff member records a decision, under their name.
- **Fair sharing of scarce stock.** The Allocations page shows the shortage plan before anything changes: the same
  stock is shared between competing requests by priority, so a shortage is visible rather than first-come-first-served.
  Applying the plan is coordinator-only and safe to repeat (nothing is duplicated). Critical requests need a coordinator
  to reserve stock, which the API enforces and the screen reports.
- **Separation of duties.** Coordinators and case managers decide on requests; logistics dispatches and delivers;
  only the administrator manages accounts and reads the audit log. The screens show only what the role may use.
- **Location capture with a safety net.** "Find my location from this address" calls the platform's map service. If that service
  is down the message says so and the coordinates can be typed; a request without coordinates is still accepted. The request page
  also shows the road distance to each suggested shelter, labelled as an estimate when the routing service is unavailable.
- **Maps.** Leaflet with OpenStreetMap tiles. Popup text is inserted as text, never as HTML, so a name from the database
  cannot inject markup. Circle markers are used (no image files). Citizens see shelters with availability only.
- **Dashboard charts are plain HTML/CSS** (no chart library): every bar has its number written next to it, so the figures can be read
  without colour, and the numbers come straight from `GET /api/metrics/operational`.
- **Notifications are polled** (the bell every 30 s, the message queue every 15 s, the dashboard every minute): simple and robust;
  server push (SignalR) would be the next step.
- **Shelter placement rules are visible.** Suggestions never include closed or full shelters, or inaccessible shelters for
  a wheelchair user. Placement is re-checked by the server at the moment of placing (capacity cannot be exceeded).
- **Volunteers are citizen accounts with a profile**, not a new role. The citizen page lets them register, add free time
  and answer assignments. Changing skills resets verification, and the form warns about it before saving.
- **Privacy.** Citizens see only availability (available / limited / full) for shelters, never exact occupancy; exact
  numbers, priority scores, AI details and actor e-mails are staff-only and the API does not send them to citizens.
- **Dev proxy instead of CORS.** Vite forwards `/api` to the API, so the browser sees one origin.
- **UTC timestamps.** EF Core returns dates without a `Z`; `lib/dates.js` adds it. Date-time inputs are converted
  from local time to UTC before they are sent (`toUtcIso`).
- **Accessibility.** Every field has a label, errors use `aria-invalid` and `aria-describedby`, focus moves to the first
  invalid field, there is a skip link, and colour is never the only signal.

## Run
See `frontend/README.md`. `npm test` runs the pure-logic unit tests with Node's built-in runner.
