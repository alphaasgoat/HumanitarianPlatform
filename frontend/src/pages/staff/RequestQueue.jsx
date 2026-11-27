import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useApi } from '../../hooks/useApi.js';
import { crisisApi, requestApi } from '../../api/endpoints.js';
import { formatDateTime } from '../../lib/dates.js';
import { CATEGORIES, REQUEST_STATUSES, URGENCIES, humanize } from '../../lib/validation.js';
import { Badge, EmptyState, ErrorMessage, Pagination, Spinner } from '../../components/ui.jsx';

const PAGE_SIZE = 20;

export default function RequestQueue() {
  const [filters, setFilters] = useState({ crisisEventId: '', status: '', urgency: '', category: '' });
  const [page, setPage] = useState(1);
  const crises = useApi((signal) => crisisApi.list(undefined, signal), []);
  const { data, error, loading, reload } = useApi(
    (signal) => requestApi.list({ ...filters, page, pageSize: PAGE_SIZE }, signal),
    [filters.crisisEventId, filters.status, filters.urgency, filters.category, page]
  );

  function setFilter(e) {
    const { name, value } = e.target;
    setFilters((f) => ({ ...f, [name]: value }));
    setPage(1);
  }

  return (
    <>
      <div className="page-head"><h1>Request queue</h1></div>

      <div className="filters card">
        <div>
          <label htmlFor="crisisEventId">Crisis</label>
          <select id="crisisEventId" name="crisisEventId" value={filters.crisisEventId} onChange={setFilter}>
            <option value="">All</option>
            {(crises.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="status">Status</label>
          <select id="status" name="status" value={filters.status} onChange={setFilter}>
            <option value="">All</option>
            {REQUEST_STATUSES.map((s) => <option key={s} value={s}>{humanize(s)}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="urgency">Urgency</label>
          <select id="urgency" name="urgency" value={filters.urgency} onChange={setFilter}>
            <option value="">All</option>
            {URGENCIES.map((u) => <option key={u} value={u}>{u}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="category">Category</label>
          <select id="category" name="category" value={filters.category} onChange={setFilter}>
            <option value="">All</option>
            {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>
      </div>

      <ErrorMessage error={error} onRetry={reload} />
      {loading && !data && <Spinner />}
      {data && data.items.length === 0 && <EmptyState title="No requests match these filters." />}

      {data && data.items.length > 0 && (
        <div className="card table-wrap">
          <table>
            <thead>
              <tr><th>Priority</th><th>Urgency</th><th>Need</th><th>People</th><th>Status</th><th>Submitted</th><th><span className="sr-only">Open</span></th></tr>
            </thead>
            <tbody>
              {data.items.map((r) => (
                <tr key={r.id}>
                  <td><strong>{r.priorityScore}</strong></td>
                  <td><Badge kind="urgency" value={r.urgency} /></td>
                  <td>{r.category}</td>
                  <td>{r.peopleAffected}</td>
                  <td><Badge kind="status" value={r.status} /></td>
                  <td>{formatDateTime(r.createdAtUtc)}</td>
                  <td><Link to={`/staff/requests/${r.id}`}>Open</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onPage={setPage} />}
    </>
  );
}
