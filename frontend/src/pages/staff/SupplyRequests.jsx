import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { shelterApi } from '../../api/endpoints.js';
import { formatDateTime } from '../../lib/dates.js';
import PromptButton from '../../components/PromptButton.jsx';
import { Badge, EmptyState, ErrorMessage, ShortId, Spinner } from '../../components/ui.jsx';

const STATUSES = ['Open', 'Fulfilled', 'Cancelled'];

export default function SupplyRequests() {
  const { hasRole } = useAuth();
  const canOpenShelter = hasRole(GROUPS.shelterView);
  const [status, setStatus] = useState('Open');
  const { data, error, loading, reload } = useApi((signal) => shelterApi.listSupplyRequests(status, signal), [status]);

  return (
    <>
      <div className="page-head"><h1>Supply requests</h1></div>
      <p className="muted">Shelters ask for food, medicine and other supplies here. Logistics answers them and marks them done.</p>

      <div className="toolbar">
        <label htmlFor="status">Status</label>
        <select id="status" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">All</option>
          {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      <ErrorMessage error={error} onRetry={reload} />
      {loading && !data && <Spinner />}
      {data && data.length === 0 && <EmptyState title="No supply requests match." />}

      {data && data.length > 0 && (
        <div className="card table-wrap">
          <table>
            <thead>
              <tr><th>Asked</th><th>Shelter</th><th>Category</th><th>Quantity</th><th>Status</th><th>Asked by</th><th>Note</th><th><span className="sr-only">Actions</span></th></tr>
            </thead>
            <tbody>
              {data.map((r) => (
                <tr key={r.id}>
                  <td>{formatDateTime(r.createdAtUtc)}</td>
                  <td>{canOpenShelter ? <Link to={`/staff/shelters/${r.shelterId}`}><ShortId id={r.shelterId} /></Link> : <ShortId id={r.shelterId} />}</td>
                  <td>{r.category}</td>
                  <td>{r.quantity}</td>
                  <td><Badge kind="supply" value={r.status} /></td>
                  <td>{r.requestedByEmail ?? '—'}</td>
                  <td className="wrap">{r.resolutionNote ?? r.note ?? ''}</td>
                  <td>
                    {r.status === 'Open' && (
                      <PromptButton label="Mark done" confirmLabel="Mark done" placeholder="Note (optional)"
                        inputProps={{ maxLength: 300 }} onSubmit={async (note) => { await shelterApi.resolveSupplyRequest(r.id, note); reload(); }} />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
