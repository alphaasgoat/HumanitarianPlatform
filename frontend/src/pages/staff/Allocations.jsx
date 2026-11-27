import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { allocationApi, crisisApi, resourceApi } from '../../api/endpoints.js';
import { formatDateTime } from '../../lib/dates.js';
import { SUPPLY_CATEGORIES, humanize } from '../../lib/validation.js';
import AllocationActions from '../../components/AllocationActions.jsx';
import { Badge, EmptyState, ErrorMessage, Notice, ShortId, Spinner, StatTile } from '../../components/ui.jsx';

const STATUSES = ['Approved', 'Dispatched', 'Delivered', 'Cancelled'];

function ShortagePlan({ crisisId, onApplied }) {
  const { hasRole } = useAuth();
  const canApply = hasRole(GROUPS.planApply);
  const [category, setCategory] = useState('Food');
  const [plan, setPlan] = useState(null);
  const [message, setMessage] = useState('');
  const preview = useAction();
  const apply = useAction();

  async function show() {
    setMessage('');
    const result = await preview.run(() => allocationApi.plan(crisisId, category));
    if (result.ok) setPlan(result.value);
  }

  async function applyPlan() {
    const result = await apply.run(() => allocationApi.applyPlan(crisisId, category));
    if (result.ok) {
      const { allocationsCreated, requestsNotServed } = result.value;
      setMessage(`${allocationsCreated} allocations created. ${requestsNotServed} requests could not be served from current stock.`);
      onApplied();
      show();
    }
  }

  return (
    <section className="card">
      <h2>Shortage plan</h2>
      <p className="muted">
        When stock is not enough for everyone, the plan shares it between the competing requests by priority.
        Showing the plan changes nothing; applying it reserves the stock (and is safe to repeat: nothing is duplicated).
      </p>
      <div className="toolbar">
        <div>
          <label htmlFor="plan-category">Category</label>
          <select id="plan-category" value={category} onChange={(e) => { setCategory(e.target.value); setPlan(null); }}>
            {SUPPLY_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>
        <button type="button" className="btn btn-primary" onClick={show} disabled={preview.busy}>
          {preview.busy ? 'Calculating…' : 'Show plan'}
        </button>
      </div>
      <ErrorMessage error={preview.error} />
      <ErrorMessage error={apply.error} />
      <Notice kind="success">{message}</Notice>

      {plan && (
        <>
          <div className="stat-grid">
            <StatTile label="Needed" value={plan.totalNeeded} />
            <StatTile label="Available" value={plan.totalAvailable} />
            <StatTile label="Shortfall" value={plan.shortfall} hint={plan.hasShortage ? 'not everyone can be served in full' : 'enough for everyone'} />
          </div>
          {plan.lines.length === 0 ? (
            <EmptyState title="No open requests need this category right now." />
          ) : (
            <div className="table-wrap inner">
              <table>
                <thead>
                  <tr><th>Request</th><th>Priority</th><th>Urgency</th><th>People</th><th>Needs</th><th>Plan gives</th><th>Outcome</th></tr>
                </thead>
                <tbody>
                  {plan.lines.map((l) => (
                    <tr key={l.requestId}>
                      <td><ShortId id={l.requestId} /></td>
                      <td>{l.priorityScore}</td>
                      <td><Badge kind="urgency" value={l.urgency} /></td>
                      <td>{l.peopleAffected}</td>
                      <td>{l.quantityNeeded}</td>
                      <td>{l.quantityProposed}{l.takes?.length > 0 && <div className="muted">{l.takes.map((t) => `${t.quantity} from ${t.resourceName}`).join(', ')}</div>}</td>
                      <td className="wrap">{humanize(l.outcome)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {canApply && plan.lines.length > 0 && (
            <div className="actions panel-foot">
              <button type="button" className="btn btn-primary" onClick={applyPlan} disabled={apply.busy}>
                {apply.busy ? 'Applying…' : 'Apply this plan'}
              </button>
              <span className="muted">Only an Emergency Coordinator can apply a plan.</span>
            </div>
          )}
        </>
      )}
    </section>
  );
}

export default function Allocations() {
  const { hasRole } = useAuth();
  const canManage = hasRole(GROUPS.resourceManage);
  const canSeeRequests = hasRole(GROUPS.requestStaff);
  const crises = useApi((signal) => crisisApi.list(undefined, signal), []);
  const resources = useApi((signal) => resourceApi.list({}, signal), []);
  const [crisisId, setCrisisId] = useState('');
  const [status, setStatus] = useState('');

  // Pick the first crisis automatically so the page is not empty on arrival.
  useEffect(() => {
    if (!crisisId && crises.data?.length) setCrisisId(crises.data[0].id);
  }, [crises.data, crisisId]);

  const allocations = useApi(
    (signal) => (crisisId ? allocationApi.byCrisis(crisisId, status, signal) : Promise.resolve([])),
    [crisisId, status]
  );
  const names = useMemo(() => new Map((resources.data ?? []).map((r) => [r.id, r.name])), [resources.data]);
  const list = allocations.data ?? [];

  function refresh() {
    allocations.reload();
    resources.reload();
  }

  return (
    <>
      <div className="page-head"><h1>Allocations</h1></div>
      <p className="muted">Stock reserved for requests. Logistics dispatches and delivers it, or cancels it so the stock goes back.</p>

      <ErrorMessage error={crises.error} onRetry={crises.reload} />
      {crises.data && crises.data.length === 0 && <EmptyState title="There are no crisis events yet." />}

      {crises.data && crises.data.length > 0 && (
        <>
          <div className="toolbar">
            <div>
              <label htmlFor="crisis">Crisis</label>
              <select id="crisis" value={crisisId} onChange={(e) => setCrisisId(e.target.value)}>
                {crises.data.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
            <div>
              <label htmlFor="status">Status</label>
              <select id="status" value={status} onChange={(e) => setStatus(e.target.value)}>
                <option value="">All</option>
                {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>
          </div>

          <ErrorMessage error={allocations.error} onRetry={allocations.reload} />
          {allocations.loading && !allocations.data && <Spinner />}
          {allocations.data && list.length === 0 && <EmptyState title="No allocations match." />}

          {list.length > 0 && (
            <div className="card table-wrap">
              <table>
                <thead>
                  <tr><th>Request</th><th>Item</th><th>Qty</th><th>Status</th><th>Why</th><th>Approved</th><th><span className="sr-only">Actions</span></th></tr>
                </thead>
                <tbody>
                  {list.map((a) => (
                    <tr key={a.id}>
                      <td>{canSeeRequests ? <Link to={`/staff/requests/${a.requestId}`}><ShortId id={a.requestId} /></Link> : <ShortId id={a.requestId} />}</td>
                      <td>{names.get(a.resourceItemId) ?? <ShortId id={a.resourceItemId} />}</td>
                      <td>{a.quantity}</td>
                      <td><Badge kind="allocation" value={a.status} />{a.cancelReason && <div className="muted">{a.cancelReason}</div>}</td>
                      <td className="wrap muted">{a.rationale}</td>
                      <td>{formatDateTime(a.approvedAtUtc)}<div className="muted">{a.approvedByEmail ?? ''}</div></td>
                      <td><AllocationActions allocation={a} canManage={canManage} onDone={refresh} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {crisisId && <ShortagePlan crisisId={crisisId} onApplied={refresh} />}
        </>
      )}
    </>
  );
}
