import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { crisisApi, shelterApi } from '../../api/endpoints.js';
import { Badge, EmptyState, ErrorMessage, Meter, Spinner, StatTile } from '../../components/ui.jsx';

export default function Shelters() {
  const { hasRole } = useAuth();
  const canManage = hasRole(GROUPS.shelterManage);
  const [crisisId, setCrisisId] = useState('');
  const crises = useApi((signal) => crisisApi.list(undefined, signal), []);
  const { data, error, loading, reload } = useApi((signal) => shelterApi.list(crisisId, signal), [crisisId]);

  const shelters = data ?? [];
  const open = shelters.filter((s) => s.status === 'Open');
  const capacity = open.reduce((sum, s) => sum + s.capacity, 0);
  const occupied = open.reduce((sum, s) => sum + s.occupancy, 0);

  return (
    <>
      <div className="page-head">
        <h1>Shelters</h1>
        {canManage && <Link className="btn btn-primary" to="/staff/shelters/new">Add shelter</Link>}
      </div>

      {(crises.data?.length ?? 0) > 0 && (
        <div className="toolbar">
          <label htmlFor="crisis">Crisis</label>
          <select id="crisis" value={crisisId} onChange={(e) => setCrisisId(e.target.value)}>
            <option value="">All crises</option>
            {crises.data.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </div>
      )}

      <ErrorMessage error={error} onRetry={reload} />
      {loading && !data && <Spinner />}

      {data && shelters.length > 0 && (
        <div className="stat-grid">
          <StatTile label="Shelters" value={shelters.length} hint={`${open.length} open`} />
          <StatTile label="People sheltered" value={occupied} hint={`of ${capacity} places in open shelters`} />
          <StatTile label="Places left" value={Math.max(0, capacity - occupied)} />
        </div>
      )}

      {data && shelters.length === 0 && (
        <EmptyState title="No shelters yet.">
          {canManage ? 'Add the first shelter so households can be placed.' : 'A shelter administrator has to add shelters.'}
        </EmptyState>
      )}

      <div className="card-grid">
        {shelters.map((s) => (
          <article className="card shelter-card" key={s.id}>
            <div className="card-head">
              <h2><Link to={`/staff/shelters/${s.id}`}>{s.name}</Link></h2>
              <Badge kind="shelter" value={s.status} />
            </div>
            <p className="muted">{s.address}</p>
            <Meter percent={s.occupancyPercent} label={`${s.occupancyPercent}% full`} />
            <p className="meter-caption">
              <strong>{s.occupancy}</strong> of {s.capacity} places used
              {s.isFull ? <span className="full-flag"> · FULL</span> : <> · {s.availableSpaces} left</>}
            </p>
            <ul className="features">
              <li className={s.wheelchairAccessible ? 'feature on' : 'feature off'}>{s.wheelchairAccessible ? '✓' : '✗'} Wheelchair access</li>
              <li className={s.hasMedicalSupport ? 'feature on' : 'feature off'}>{s.hasMedicalSupport ? '✓' : '✗'} Medical</li>
              <li className={s.providesFood ? 'feature on' : 'feature off'}>{s.providesFood ? '✓' : '✗'} Food</li>
              <li className={s.providesWater ? 'feature on' : 'feature off'}>{s.providesWater ? '✓' : '✗'} Water</li>
            </ul>
            {!s.isPublic && <p className="muted">Not shown on the public list.</p>}
          </article>
        ))}
      </div>
    </>
  );
}
