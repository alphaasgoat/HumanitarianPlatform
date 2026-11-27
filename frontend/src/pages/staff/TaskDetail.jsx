import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { taskApi, volunteerApi } from '../../api/endpoints.js';
import { formatDateTime } from '../../lib/dates.js';
import { humanize } from '../../lib/validation.js';
import PromptButton from '../../components/PromptButton.jsx';
import { Badge, Chips, EmptyState, ErrorMessage, Notice, Spinner } from '../../components/ui.jsx';

export default function TaskDetail() {
  const { id } = useParams();
  const { hasRole } = useAuth();
  const canCoordinate = hasRole(GROUPS.volunteerCoordinate);
  const task = useApi((signal) => taskApi.get(id, signal), [id]);
  const assignments = useApi((signal) => taskApi.assignments(id, signal), [id]);
  const volunteers = useApi((signal) => volunteerApi.list({}, signal), []);
  const suggest = useAction();
  const assign = useAction();
  const [matches, setMatches] = useState(null);
  const [message, setMessage] = useState('');

  const names = useMemo(() => {
    const map = new Map();
    (volunteers.data ?? []).forEach((v) => map.set(v.id, v.fullName));
    return map;
  }, [volunteers.data]);
  const nameOf = (volunteerId) => names.get(volunteerId) ?? String(volunteerId).slice(0, 8);

  const t = task.data;
  const closed = t && (t.status === 'Cancelled' || t.status === 'Completed');

  function refresh() {
    task.reload();
    assignments.reload();
  }

  async function findVolunteers() {
    setMessage('');
    const result = await suggest.run(() => taskApi.recommend(id));
    if (result.ok) setMatches(result.value);
  }

  async function assignVolunteer(m) {
    const result = await assign.run(() => taskApi.assign(id, m.volunteerId, null));
    if (result.ok) {
      setMessage(`${m.fullName} was assigned. They will see it under their assignments and can accept or decline.`);
      setMatches((list) => (list ?? []).filter((x) => x.volunteerId !== m.volunteerId));
      refresh();
    }
  }

  return (
    <>
      <p><Link to="/staff/tasks">← Back to tasks</Link></p>
      <ErrorMessage error={task.error} onRetry={task.reload} />
      {task.loading && !t && <Spinner />}

      {t && (
        <>
          <div className="page-head">
            <h1>{t.title}</h1>
            <div className="badges"><Badge kind="task" value={t.status} /></div>
          </div>

          <section className="card">
            {t.description && <p>{t.description}</p>}
            <dl className="details">
              <div className="row"><dt>Skill needed</dt><dd>{humanize(t.requiredSkill)}</dd></div>
              <div className="row"><dt>Volunteers</dt><dd>{t.activeAssignments} of {t.volunteersNeeded} assigned</dd></div>
              <div className="row"><dt>When</dt><dd>{formatDateTime(t.startUtc)} to {formatDateTime(t.endUtc)}</dd></div>
              {t.latitude != null && t.longitude != null && <div className="row"><dt>Location</dt><dd>{t.latitude}, {t.longitude}</dd></div>}
            </dl>
            {canCoordinate && !closed && (
              <div className="actions">
                <PromptButton label="Cancel task" confirmLabel="Cancel task" placeholder="Reason (optional)" tone="btn-danger"
                  inputProps={{ maxLength: 300 }} onSubmit={async (reason) => { await taskApi.cancel(id, reason); refresh(); }} />
              </div>
            )}
          </section>

          <section className="card table-wrap">
            <div className="card-pad"><h2>Assignments</h2></div>
            <ErrorMessage error={assignments.error} onRetry={assignments.reload} />
            {assignments.loading && !assignments.data && <Spinner />}
            {assignments.data && assignments.data.length === 0 && (
              <EmptyState title="Nobody is assigned yet.">Use "Suggest volunteers" below.</EmptyState>
            )}
            {assignments.data && assignments.data.length > 0 && (
              <table>
                <thead><tr><th>Volunteer</th><th>Status</th><th>Match</th><th>Assigned</th><th>Hours</th><th><span className="sr-only">Actions</span></th></tr></thead>
                <tbody>
                  {assignments.data.map((a) => (
                    <tr key={a.id}>
                      <td><strong>{nameOf(a.volunteerId)}</strong></td>
                      <td><Badge kind="assign" value={a.status} /></td>
                      <td>{Math.round(a.matchScore)}</td>
                      <td>{formatDateTime(a.assignedAtUtc)}</td>
                      <td>{a.hoursWorked ?? '—'}</td>
                      <td>
                        {canCoordinate && (a.status === 'Assigned' || a.status === 'Accepted') && (
                          <div className="actions">
                            {a.status === 'Accepted' && (
                              <PromptButton label="Complete" confirmLabel="Save" placeholder="Hours worked (0.25 - 24)" type="number"
                                required inputProps={{ min: 0.25, max: 24, step: 0.25 }}
                                onSubmit={async (hours) => { await taskApi.complete(a.id, hours, null); refresh(); }} />
                            )}
                            <PromptButton label="Remove" confirmLabel="Remove" placeholder="Reason (optional)" tone="btn-danger"
                              inputProps={{ maxLength: 300 }} onSubmit={async (reason) => { await taskApi.cancelAssignment(a.id, reason); refresh(); }} />
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          {canCoordinate && !closed && (
            <section className="card">
              <div className="head-row">
                <h2>Suggest volunteers</h2>
                <button type="button" className="btn btn-primary" onClick={findVolunteers} disabled={suggest.busy}>
                  {suggest.busy ? 'Looking…' : matches ? 'Refresh suggestions' : 'Suggest volunteers'}
                </button>
              </div>
              <p className="muted">
                Only verified, active volunteers with the right skill who are free for the whole shift (no clash with another task)
                are listed. Volunteers with fewer hours so far rank higher, so the work is shared fairly. You decide who to assign.
              </p>
              <Notice kind="success">{message}</Notice>
              <ErrorMessage error={suggest.error} />
              <ErrorMessage error={assign.error} />
              {matches && matches.length === 0 && (
                <EmptyState title="No volunteer qualifies right now.">
                  Check that volunteers are verified and have added an availability slot that covers the whole shift.
                </EmptyState>
              )}
              {matches && matches.length > 0 && (
                <ul className="match-list">
                  {matches.map((m, i) => (
                    <li key={m.volunteerId} className="match">
                      <div className="match-rank">{i + 1}</div>
                      <div className="match-body">
                        <strong>{m.fullName}</strong> <span className="muted">· score {Math.round(m.score)}{m.distanceKm != null ? ` · ${m.distanceKm.toFixed(1)} km away` : ''}</span>
                        <Chips items={m.skills} />
                        <div className="muted">{m.rationale}</div>
                      </div>
                      <button type="button" className="btn btn-small btn-primary" disabled={assign.busy} onClick={() => assignVolunteer(m)}>Assign</button>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          )}
        </>
      )}
    </>
  );
}
