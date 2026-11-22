# Phase 3B - Volunteer management

## What it does
Volunteers register a profile with skills and a location, publish when they are available, and a coordinator
verifies them, creates tasks, and assigns people. The volunteer accepts or declines. Hours are recorded when the
work is completed.

## Design decision: a volunteer is a profile on a citizen account
There is no extra "Volunteer" role. A citizen account can register a volunteer profile
(`POST /api/volunteers/me`). A volunteer can only see and answer their own assignments; staff roles see the rest.
Adding a role would have changed shared Phase 1 code for no gain.

## Governance
- Only VERIFIED and ACTIVE volunteers can be assigned (verification is a coordinator decision).
- Changing the skill list resets verification: a new claim such as "Medical" must be checked again.
- The matcher only RECOMMENDS. A coordinator assigns, and the audit record stores the matcher's top choice and
  the rank the coordinator chose.

## Matching (Strategy: `IVolunteerMatcher`)
- Hard constraints: active, verified, has the required skill, free for the WHOLE shift, no clash with another
  active task.
- Soft constraints: skill fit 0.40, distance 0.35, workload fairness 0.25 (fewer active tasks scores higher),
  so the same few people are not given every task.
- Assignment re-checks every hard constraint on the server and returns HTTP 409 with a clear reason.

## Lifecycle
Task: Open -> Filled (enough volunteers) -> Completed, or Cancelled.
Assignment: Assigned -> Accepted -> Completed, or Declined / Cancelled. A declined or cancelled assignment
reopens a Filled task.

## Endpoints
| Method | Route | Roles |
|---|---|---|
| POST/GET/PUT | /api/volunteers/me | Citizen (own profile) |
| POST/GET | /api/volunteers/me/availability | Citizen |
| GET | /api/volunteers, /api/volunteers/{id} | VolunteerCoordinator, Coordinator, CaseManager |
| POST | /api/volunteers/{id}/verify | VolunteerCoordinator, Coordinator |
| POST/GET | /api/volunteer-tasks | create: VolunteerCoordinator, Coordinator / list: + CaseManager |
| GET | /api/volunteer-tasks/{id}/recommend | VolunteerCoordinator, Coordinator |
| POST | /api/volunteer-tasks/{id}/assign | VolunteerCoordinator, Coordinator |
| GET | /api/volunteer-tasks/my-assignments | Citizen (own) |
| POST | /api/volunteer-tasks/assignments/{id}/accept, /decline | Citizen (own) |
| POST | /api/volunteer-tasks/assignments/{id}/complete, /cancel | VolunteerCoordinator, Coordinator |
