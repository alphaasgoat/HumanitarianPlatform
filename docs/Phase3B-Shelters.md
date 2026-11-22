# Phase 3B - Shelter management

## What it does
Register shelters, publish the approved ones for affected people, recommend a suitable shelter for a
triaged household, and let a person place the household. Occupancy can never exceed capacity.

## Placement rules
- Only requests triaged by a human can be placed (Phase 2 governance).
- A request can have one active placement at a time; release it to move the household.
- People count is 1..household size.
- `Occupancy + people <= Capacity`, otherwise HTTP 409.
- Two layers stop over-allocation: the service check, and a concurrency token (`RowVersion`) plus a database
  CHECK constraint (`0 <= Occupancy <= Capacity`). If two people fill the last bed at once, one gets 409.
- A shelter with people placed in it cannot be closed; capacity cannot drop below occupancy.

## Recommendation (Strategy: `IShelterMatcher`)
- Hard constraints: open, enough free spaces, wheelchair access when the request needs it.
- Soft constraints: distance 0.35, room left after placing 0.30, medical support when needed 0.20,
  food and water on site 0.15.
- Missing medical support, food or water appears as a warning. A human may still choose such a shelter;
  the warning is returned and the audit record stores the matcher's top choice and the rank the human chose.

## Public information
`GET /api/shelters/public` returns only verified, open, public shelters with a coarse availability level
(Available / Limited / Full). Exact occupancy is staff-only (privacy).

## Supplies
A shelter administrator raises a supply request (category, quantity). Logistics sees open requests and
resolves them. This covers "request additional resources when supplies are insufficient".

## Endpoints
| Method | Route | Roles |
|---|---|---|
| GET | /api/shelters/public | any signed-in user |
| GET/POST/PUT | /api/shelters ... | view: ShelterAdmin, Coordinator, CaseManager / manage: ShelterAdmin, Coordinator |
| GET | /api/shelters/recommend/{requestId} | view roles |
| POST | /api/shelters/allocate | ShelterAdmin, Coordinator |
| POST | /api/shelters/allocations/{id}/release | ShelterAdmin, Coordinator |
| POST/GET | /api/shelters/{id}/supply-requests, /api/shelters/supply-requests | ShelterAdmin, Coordinator, Logistics |
