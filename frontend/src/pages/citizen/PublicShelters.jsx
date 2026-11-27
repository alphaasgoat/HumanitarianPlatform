import { useMemo, useState } from 'react';
import { useApi } from '../../hooks/useApi.js';
import { crisisApi, shelterApi } from '../../api/endpoints.js';
import { AVAILABILITY_COLORS } from '../../lib/format.js';
import MapView from '../../components/MapView.jsx';
import { Badge, EmptyState, ErrorMessage, Spinner } from '../../components/ui.jsx';

function Feature({ on, children }) {
  return <li className={on ? 'feature on' : 'feature off'}>{on ? '✓' : '✗'} {children}</li>;
}

export default function PublicShelters() {
  const [crisisId, setCrisisId] = useState('');
  const crises = useApi((signal) => crisisApi.list('Active', signal), []);
  const shelters = useApi((signal) => shelterApi.listPublic(crisisId, signal), [crisisId]);

  const points = useMemo(() => (shelters.data ?? [])
    .filter((s) => s.latitude != null && s.longitude != null)
    .map((s) => ({
      id: s.id, lat: s.latitude, lng: s.longitude, color: AVAILABILITY_COLORS[s.availability] ?? '#1a5fd0', radius: 12,
      lines: [s.name, `Space: ${s.availability}`, s.address]
    })), [shelters.data]);

  return (
    <>
      <div className="page-head"><h1>Shelters</h1></div>
      <p className="muted">
        Open, verified shelters. For privacy and safety we only show how much room is left (available, limited or full),
        never exact numbers of people inside.
      </p>

      {(crises.data?.length ?? 0) > 1 && (
        <div className="filters">
          <label htmlFor="crisis">Crisis</label>
          <select id="crisis" value={crisisId} onChange={(e) => setCrisisId(e.target.value)}>
            <option value="">All crises</option>
            {crises.data.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </div>
      )}

      <ErrorMessage error={shelters.error} onRetry={shelters.reload} />
      {shelters.loading && !shelters.data && <Spinner />}

      {shelters.data && shelters.data.length === 0 && (
        <EmptyState title="No open shelters are listed yet.">Check again soon, or ask for help and staff will find a place for you.</EmptyState>
      )}

      {points.length > 0 && (
        <div className="card">
          <MapView points={points} height={340} label="Map of open shelters" />
          <p className="muted">Green: space available. Amber: limited space. Red: full.</p>
        </div>
      )}

      <div className="card-grid">
        {(shelters.data ?? []).map((s) => (
          <article className="card" key={s.id}>
            <div className="card-head">
              <h2>{s.name}</h2>
              <Badge kind="avail" value={s.availability} />
            </div>
            <p>{s.address}</p>
            <ul className="features">
              <Feature on={s.wheelchairAccessible}>Wheelchair accessible</Feature>
              <Feature on={s.hasMedicalSupport}>Medical support</Feature>
              <Feature on={s.providesFood}>Food</Feature>
              <Feature on={s.providesWater}>Water</Feature>
            </ul>
            {s.notes && <p className="muted">{s.notes}</p>}
            {s.latitude != null && s.longitude != null && (
              <a href={`https://www.openstreetmap.org/?mlat=${s.latitude}&mlon=${s.longitude}#map=16/${s.latitude}/${s.longitude}`}
                target="_blank" rel="noopener noreferrer">View on map</a>
            )}
          </article>
        ))}
      </div>
    </>
  );
}
