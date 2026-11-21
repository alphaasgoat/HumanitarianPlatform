# Phase 3A - Resources and allocation

## What it does
Inventory of aid (food, water, medicine, vehicles, equipment) and the allocation of that stock to
**triaged** assistance requests. Nothing is allocated by the system on its own: the matcher recommends,
a person approves.

## Multi-constraint matching (advanced requirement 4)
`ResourceMatcher` ranks candidates; it is not "nearest wins".
- Hard constraints: right category, active, stock > 0.
- Soft constraints: coverage of the need, distance, stock left after allocating (vs reorder level).
- High/Critical requests weight proximity most (0.50); normal requests weight coverage and stock health.
- The best candidate is "preferred"; the rest are alternatives. If nothing can cover the need the response
  suggests requesting partner assistance.

## Shortage scenario (advanced requirement 5)
`ShortagePlanner` shares limited stock between competing requests:
1. order by urgency (Critical first), then priority score, then oldest first;
2. each request takes stock (split across depots if needed) until stock runs out;
3. outcome per request: Full, Partial or Unfulfilled; the plan also shows the total shortfall.
`GET /api/allocations/plan` changes nothing. `POST /api/allocations/plan/apply` (Emergency Coordinator only)
recomputes the plan on the server and applies it in one transaction. Applying twice creates nothing new.

## Safety rules
- Only requests that a human triaged (Phase 2) can receive resources.
- Critical requests and shortage plans need an Emergency Coordinator.
- Allocation never exceeds available stock or the estimated need (3 days of food/water per person).
- Concurrency: `ResourceItem.RowVersion` + a CHECK constraint (0 <= available <= total).
  Two people reserving the last units at once: one gets HTTP 409.
- Every allocation, dispatch, delivery and cancellation is audited; the audit record also stores the
  matcher's top recommendation and the rank the human chose.

## Lifecycle
Approved -> Dispatched -> Delivered (stock leaves the depot) or Cancelled (stock returns).

## Endpoints
| Method | Route | Roles |
|---|---|---|
| GET/POST | /api/resources | view: Logistics, Coordinator, CaseManager / create: Logistics, Coordinator |
| POST | /api/resources/{id}/restock | Logistics, Coordinator |
| GET | /api/allocations/recommend/{requestId} | view roles |
| POST | /api/allocations | Logistics, Coordinator (Critical: Coordinator) |
| GET | /api/allocations/plan | view roles |
| POST | /api/allocations/plan/apply | Coordinator |
| POST | /api/allocations/{id}/dispatch, /deliver, /cancel | Logistics, Coordinator |

## Patterns
Strategy (`IResourceMatcher`), Repository + Unit of Work, optimistic concurrency, auto-discovered modules
(`IPlatformModule`), Dependency Injection.
