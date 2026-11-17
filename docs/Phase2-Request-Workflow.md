# Phase 2 - Assistance requests and human-in-the-loop AI

## Workflow
```
Citizen submits  ->  AI analysis (classify + score + confidence + rationale)
                 ->  duplicate check (flag only)        ->  status UnderReview
Staff review     ->  Approve | Modify (+note) | Reject (+note) | Defer
                 ->  Triaged  (ready for resource / shelter allocation in Phase 3)
Other actions    ->  Analyze again, Escalate, Merge duplicate, Dismiss duplicate flag, Close
```

## Governance rules (what to say in the viva)
- AI never changes a request. Only an authorised person can (`ReviewAsync`).
- `AiRecommendation.Suggested*` is never overwritten, so the original recommendation, confidence and rationale
  stay available for post-incident review. The human decision is stored next to it
  (`HumanAction`, `Final*`, `DecidedBy*`, `DecidedAtUtc`, `DecisionNote`).
- Every decision writes an audit event (`AiDecisionApproved|Modified|Rejected|Deferred`) containing the
  recommendation, confidence, model, human action, final values and timestamp.
- Critical priority can only be approved by an Emergency Coordinator; escalated requests too.
- Modify and Reject require a written reason.

## Reliability rules
- `ResilientRequestAnalyzer`: per-strategy timeout, ordered fallback, final "manual review required" result.
  A fallback is visible in the rationale and in `UsedFallback`.
- Idempotent submission: `IdempotencyKey` + unique index (RequesterId, IdempotencyKey).
- Duplicates are suspected links (`DuplicateLink`), never deletions. Merge and dismiss are human actions.

## Patterns used
Strategy (`IAnalysisStrategy`), Decorator-like facade (`ResilientRequestAnalyzer`), Repository + Unit of Work,
Dependency Injection, modular registration (`RequestsModule`).

## Endpoints
| Method | Route | Roles |
|---|---|---|
| POST | /api/requests | Citizen |
| GET | /api/requests/mine | Citizen |
| GET | /api/requests | CaseManager, EmergencyCoordinator |
| GET | /api/requests/{id} | Citizen (own), staff |
| POST | /api/requests/{id}/analyze | staff |
| POST | /api/requests/{id}/review | staff |
| POST | /api/requests/{id}/escalate | staff |
| POST | /api/requests/{id}/merge | staff |
| POST | /api/requests/{id}/duplicates/{linkId}/dismiss | staff |
| POST | /api/requests/{id}/close | staff |
