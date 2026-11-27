import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { crisisApi, taskApi } from '../../api/endpoints.js';
import { formatDateTime } from '../../lib/dates.js';
import { humanize } from '../../lib/validation.js';
import { Badge, EmptyState, ErrorMessage, Spinner } from '../../components/ui.jsx';

const STATUSES = ['Open', 'Filled', 'Completed', 'Cancelled'];

export default function Tasks() {
  const { hasRole } = useAuth();
  const canCoordinate = hasRole(GROUPS.volunteerCoordinate);
  const [filters, setFilters] = useState({ crisisEventId: '', status: '' });
  const crises = useApi((signal) => crisisApi.list(undefined, signal), []);
  const { data, error, loading, reload } = useApi(
    (signal) => taskApi.list(filters, signal), [filters.crisisEventId, filters.status]
  );

  function setFilter(e) {
    setFilters((f) => ({ ...f, [e.target.name]: e.target.value }));
  }

  return (
    <>
      <div className="page-head">
        <h1>Volunteer tasks</h1>
        {canCoordinate && <Link className="btn btn-primary" to="/staff/tasks/new">New task</Link>}
      </div>

      <div className="toolbar">
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
            {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>
      </div>

      <ErrorMessage error={error} onRetry={reload} />
      {loading && !data && <Spinner />}
      {data && data.length === 0 && (
        <EmptyState title="No tasks yet.">{canCoordinate ? 'Create a task, then assign verified volunteers to it.' : ''}</EmptyState>
      )}

      {data && data.length > 0 && (
        <div className="card table-wrap">
          <table>
            <thead>
              <tr><th>Task</th><th>Skill</th><th>People</th><th>When</th><th>Status</th><th><span className="sr-only">Open</span></th></tr>
            </thead>
            <tbody>
              {data.map((t) => (
                <tr key={t.id}>
                  <td><strong>{t.title}</strong></td>
                  <td>{humanize(t.requiredSkill)}</td>
                  <td>{t.activeAssignments} / {t.volunteersNeeded}</td>
                  <td>{formatDateTime(t.startUtc)}<div className="muted">to {formatDateTime(t.endUtc)}</div></td>
                  <td><Badge kind="task" value={t.status} /></td>
                  <td><Link to={`/staff/tasks/${t.id}`}>Open</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
