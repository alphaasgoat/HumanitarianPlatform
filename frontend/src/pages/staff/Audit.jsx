import { useState } from 'react';
import { useApi } from '../../hooks/useApi.js';
import { auditApi } from '../../api/endpoints.js';
import { AUDIT_ACTIONS, AUDIT_ENTITIES, auditTone } from '../../lib/audit.js';
import { formatDateTime } from '../../lib/dates.js';
import { humanize } from '../../lib/validation.js';
import { EmptyState, ErrorMessage, Pagination, ShortId, Spinner } from '../../components/ui.jsx';

const PAGE_SIZE = 50;

export default function Audit() {
  const [filters, setFilters] = useState({ action: '', entityType: '' });
  const [page, setPage] = useState(1);
  const { data, error, loading, reload } = useApi(
    (signal) => auditApi.query({ ...filters, page, pageSize: PAGE_SIZE }, signal),
    [filters.action, filters.entityType, page]
  );

  function setFilter(e) {
    setFilters((f) => ({ ...f, [e.target.name]: e.target.value }));
    setPage(1);
  }

  return (
    <>
      <div className="page-head"><h1>Audit log</h1></div>
      <p className="muted">Every sensitive action is recorded with who did it, when and from where. Newest first. The log is read-only.</p>

      <div className="toolbar">
        <div>
          <label htmlFor="action">Action</label>
          <select id="action" name="action" value={filters.action} onChange={setFilter}>
            <option value="">Any action</option>
            {AUDIT_ACTIONS.map((a) => <option key={a} value={a}>{humanize(a)}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="entityType">Record type</label>
          <select id="entityType" name="entityType" value={filters.entityType} onChange={setFilter}>
            <option value="">Any type</option>
            {AUDIT_ENTITIES.map((a) => <option key={a} value={a}>{humanize(a)}</option>)}
          </select>
        </div>
        <button type="button" className="btn" onClick={reload}>Refresh</button>
      </div>

      <ErrorMessage error={error} onRetry={reload} />
      {loading && !data && <Spinner />}
      {data && data.items.length === 0 && <EmptyState title="No events match these filters." />}

      {data && data.items.length > 0 && (
        <div className="card table-wrap">
          <table>
            <thead><tr><th>When</th><th>Who</th><th>Action</th><th>Record</th><th>Details</th><th>From</th></tr></thead>
            <tbody>
              {data.items.map((a) => (
                <tr key={a.id}>
                  <td>{formatDateTime(a.timestampUtc)}</td>
                  <td>{a.actorEmail ?? <span className="muted">system / anonymous</span>}</td>
                  <td><span className={`badge audit-${auditTone(a.action)}`}>{humanize(a.action)}</span></td>
                  <td>{humanize(a.entityType)}{a.entityId && <> <ShortId id={a.entityId} /></>}</td>
                  <td className="wrap muted">{a.details ?? ''}</td>
                  <td>{a.ipAddress ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onPage={setPage} />}
      {data && <p className="muted center">{data.total} events</p>}
    </>
  );
}
