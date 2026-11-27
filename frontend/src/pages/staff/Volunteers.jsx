import { useState } from 'react';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { volunteerApi } from '../../api/endpoints.js';
import { SKILLS, humanize } from '../../lib/validation.js';
import { Badge, Chips, EmptyState, ErrorMessage, Spinner, StatTile } from '../../components/ui.jsx';

export default function Volunteers() {
  const { hasRole } = useAuth();
  const canCoordinate = hasRole(GROUPS.volunteerCoordinate);
  const [skill, setSkill] = useState('');
  const [verified, setVerified] = useState('');
  const { data, error, loading, reload } = useApi(
    (signal) => volunteerApi.list({ skill, verified: verified === '' ? undefined : verified === 'yes' }, signal),
    [skill, verified]
  );
  const action = useAction();

  const list = data ?? [];
  const verifiedCount = list.filter((v) => v.isVerified).length;
  const hours = list.reduce((sum, v) => sum + v.totalHours, 0);

  async function act(fn) {
    const result = await action.run(fn);
    if (result.ok) reload();
  }

  return (
    <>
      <div className="page-head"><h1>Volunteers</h1></div>
      <p className="muted">
        Citizens register themselves as volunteers. Only verified, active volunteers can be assigned to tasks.
      </p>

      <div className="toolbar">
        <div>
          <label htmlFor="skill">Skill</label>
          <select id="skill" value={skill} onChange={(e) => setSkill(e.target.value)}>
            <option value="">Any</option>
            {SKILLS.map((s) => <option key={s} value={s}>{humanize(s)}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="verified">Verification</label>
          <select id="verified" value={verified} onChange={(e) => setVerified(e.target.value)}>
            <option value="">All</option>
            <option value="yes">Verified</option>
            <option value="no">Waiting for verification</option>
          </select>
        </div>
      </div>

      <ErrorMessage error={error} onRetry={reload} />
      <ErrorMessage error={action.error} />
      {loading && !data && <Spinner />}

      {data && list.length > 0 && (
        <div className="stat-grid">
          <StatTile label="Volunteers" value={list.length} />
          <StatTile label="Verified" value={verifiedCount} hint={`${list.length - verifiedCount} waiting`} />
          <StatTile label="Hours given" value={Math.round(hours * 10) / 10} />
        </div>
      )}
      {data && list.length === 0 && <EmptyState title="No volunteers match." />}

      {data && list.length > 0 && (
        <div className="card table-wrap">
          <table>
            <thead>
              <tr><th>Name</th><th>Skills</th><th>Verification</th><th>Account</th><th>Hours</th><th>Tasks done</th><th><span className="sr-only">Actions</span></th></tr>
            </thead>
            <tbody>
              {list.map((v) => (
                <tr key={v.id}>
                  <td><strong>{v.fullName}</strong>{v.phone && <div className="muted">{v.phone}</div>}</td>
                  <td className="wrap"><Chips items={v.skills} /></td>
                  <td><Badge kind="verify" value={v.isVerified ? 'Verified' : 'Pending'} /></td>
                  <td><Badge kind="active" value={v.isActive ? 'Active' : 'Inactive'} /></td>
                  <td>{v.totalHours}</td>
                  <td>{v.completedTasks}</td>
                  <td>
                    {canCoordinate && (
                      <div className="actions">
                        {!v.isVerified && (
                          <button type="button" className="btn btn-small btn-primary" disabled={action.busy}
                            onClick={() => act(() => volunteerApi.verify(v.id))}>Verify</button>
                        )}
                        <button type="button" className="btn btn-small" disabled={action.busy}
                          onClick={() => act(() => volunteerApi.setActive(v.id, !v.isActive))}>
                          {v.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      </div>
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
