import { useMemo, useState } from 'react';
import { useApi } from '../hooks/useApi.js';
import { useAction } from '../hooks/useAction.js';
import { allocationApi, resourceApi } from '../api/endpoints.js';
import { formatDateTime } from '../lib/dates.js';
import { validateQuantity } from '../lib/validation.js';
import AllocationActions from './AllocationActions.jsx';
import { Badge, ErrorMessage, Notice } from './ui.jsx';

/**
 * Supplies for one request: ranked stock suggestions (preferred item first, then alternatives), the allocations that
 * already exist and their next step. Reserving stock is a person's decision; the server re-checks the stock and
 * requires an Emergency Coordinator for Critical requests.
 */
export default function ResourceAllocationPanel({ request, canManage }) {
  const canAsk = request.status === 'Triaged' || request.status === 'Escalated';
  const allocations = useApi((signal) => allocationApi.byRequest(request.id, signal), [request.id]);
  const resources = useApi((signal) => resourceApi.list({}, signal), []);
  const find = useAction();
  const alloc = useAction();
  const [result, setResult] = useState(null);
  const [quantity, setQuantity] = useState('');
  const [problem, setProblem] = useState('');
  const [done, setDone] = useState('');

  const names = useMemo(() => new Map((resources.data ?? []).map((r) => [r.id, r.name])), [resources.data]);

  async function load() {
    const res = await find.run(() => allocationApi.recommend(request.id));
    if (res.ok) {
      setResult(res.value);
      setQuantity(String(res.value.quantityNeeded));
    }
  }

  async function allocate(candidate) {
    setDone('');
    const bad = validateQuantity(quantity);
    setProblem(bad);
    if (bad) return;
    const res = await alloc.run(() => allocationApi.allocate({
      requestId: request.id, resourceItemId: candidate.resourceItemId, quantity: Number(quantity), note: null
    }));
    if (res.ok) {
      setDone(`${res.value.quantity} x ${candidate.name} reserved for this request.`);
      allocations.reload();
      resources.reload();
      load();
    }
  }

  const list = allocations.data ?? [];

  return (
    <section className="card">
      <div className="head-row">
        <h2>Supplies</h2>
        {canAsk && (
          <button type="button" className="btn btn-primary" onClick={load} disabled={find.busy}>
            {find.busy ? 'Looking…' : result ? 'Refresh' : 'Suggest supplies'}
          </button>
        )}
      </div>

      {list.length > 0 && (
        <div className="table-wrap inner">
          <table>
            <thead><tr><th>Item</th><th>Qty</th><th>Status</th><th>Approved</th><th><span className="sr-only">Actions</span></th></tr></thead>
            <tbody>
              {list.map((a) => (
                <tr key={a.id}>
                  <td>{names.get(a.resourceItemId) ?? String(a.resourceItemId).slice(0, 8)}</td>
                  <td>{a.quantity}</td>
                  <td><Badge kind="allocation" value={a.status} />{a.cancelReason && <div className="muted">{a.cancelReason}</div>}</td>
                  <td>{formatDateTime(a.approvedAtUtc)}<div className="muted">{a.approvedByEmail ?? ''}</div></td>
                  <td>
                    <AllocationActions allocation={a} canManage={canManage}
                      onDone={() => { allocations.reload(); resources.reload(); }} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {allocations.data && list.length === 0 && <p className="muted">Nothing has been allocated to this request yet.</p>}
      <ErrorMessage error={allocations.error} onRetry={allocations.reload} />

      <ErrorMessage error={find.error} />
      <ErrorMessage error={alloc.error} />
      <Notice kind="success">{done}</Notice>

      {result && (
        <>
          <p>{result.message}</p>
          {canManage && result.candidates.length > 0 && (
            <div className="toolbar">
              <div>
                <label htmlFor="alloc-qty">Quantity to reserve ({result.resourceCategory})</label>
                <input id="alloc-qty" type="number" min="1" value={quantity} aria-invalid={problem ? 'true' : 'false'}
                  onChange={(e) => { setQuantity(e.target.value); setProblem(''); }} />
                {problem && <small className="error-text">{problem}</small>}
              </div>
            </div>
          )}
          <ul className="match-list">
            {result.candidates.map((c, i) => (
              <li className="match" key={c.resourceItemId}>
                <div className="match-rank">{i + 1}</div>
                <div className="match-body">
                  <strong>{c.name}</strong>{' '}
                  <span className="muted">· {c.quantityAvailable} available · score {Math.round(c.score)}{c.distanceKm != null ? ` · ${c.distanceKm.toFixed(1)} km` : ''}</span>
                  {' '}<span className={c.canFullyCover ? 'badge allocation-Delivered' : 'badge allocation-Approved'}>
                    {c.canFullyCover ? 'Covers the whole need' : 'Partial'}
                  </span>
                  <div className="muted">{c.rationale}</div>
                </div>
                {canManage && (
                  <button type="button" className="btn btn-small btn-primary" disabled={alloc.busy} onClick={() => allocate(c)}>Reserve</button>
                )}
              </li>
            ))}
          </ul>
          {!canManage && result.candidates.length > 0 && <p className="muted">Only logistics or a coordinator can reserve stock.</p>}
        </>
      )}
    </section>
  );
}
