import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { crisisApi, requestApi, shelterApi } from '../../api/endpoints.js';
import { AVAILABILITY_COLORS, URGENCY_COLORS } from '../../lib/format.js';
import MapView from '../../components/MapView.jsx';
import { ErrorMessage, Spinner } from '../../components/ui.jsx';

const CLOSED = ['Closed', 'Merged'];

function shelterColor(s) {
  if (s.status !== 'Open') return '#8a93a0';
  if (s.isFull) return AVAILABILITY_COLORS.Full;
  return s.occupancyPercent >= 80 ? AVAILABILITY_COLORS.Limited : AVAILABILITY_COLORS.Available;
}

export default function MapPage() {
  const { hasRole } = useAuth();
  const canRequests = hasRole(GROUPS.requestStaff);
  const canShelters = hasRole(GROUPS.shelterView);
  const [crisisId, setCrisisId] = useState('');
  const [showRequests, setShowRequests] = useState(true);
  const [showShelters, setShowShelters] = useState(true);
  const crises = useApi((signal) => crisisApi.list(undefined, signal), []);
  const requests = useApi((signal) => (canRequests ? requestApi.list({ crisisEventId: crisisId, pageSize: 100 }, signal) : Promise.resolve(null)), [crisisId]);
  const shelters = useApi((signal) => (canShelters ? shelterApi.list(crisisId, signal) : Promise.resolve([])), [crisisId]);

  const openRequests = useMemo(() => (requests.data?.items ?? []).filter((r) => !CLOSED.includes(r.status)), [requests.data]);
  const located = openRequests.filter((r) => r.latitude != null && r.longitude != null);
  const shelterList = shelters.data ?? [];
  const shelterLocated = shelterList.filter((s) => s.latitude != null && s.longitude != null);

  const points = useMemo(() => {
    const list = [];
    if (showRequests) {
      located.forEach((r) => list.push({
        id: `r-${r.id}`, lat: r.latitude, lng: r.longitude, color: URGENCY_COLORS[r.urgency] ?? '#b3261e', radius: 8,
        lines: [`${r.category} request (${r.urgency})`, `${r.peopleAffected} people · ${r.status}`, r.addressText]
      }));
    }
    if (showShelters) {
      shelterLocated.forEach((s) => list.push({
        id: `s-${s.id}`, lat: s.latitude, lng: s.longitude, color: shelterColor(s), radius: 12,
        lines: [`Shelter: ${s.name}`, `${s.occupancy} of ${s.capacity} places used`, s.address]
      }));
    }
    return list;
  }, [located, shelterLocated, showRequests, showShelters]);

  return (
    <>
      <div className="page-head"><h1>Map</h1></div>
      <p className="muted">
        Circles are open requests (colour = urgency); larger circles are shelters (green has room, amber is nearly full, red is full, grey is closed).
        A request appears when it has a location: either given by the person or worked out from the address.
      </p>

      <div className="toolbar">
        <div>
          <label htmlFor="crisis">Crisis</label>
          <select id="crisis" value={crisisId} onChange={(e) => setCrisisId(e.target.value)}>
            <option value="">All crises</option>
            {(crises.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </div>
        {canRequests && (
          <label className="inline-check"><input type="checkbox" checked={showRequests} onChange={(e) => setShowRequests(e.target.checked)} /> Requests ({located.length})</label>
        )}
        {canShelters && (
          <label className="inline-check"><input type="checkbox" checked={showShelters} onChange={(e) => setShowShelters(e.target.checked)} /> Shelters ({shelterLocated.length})</label>
        )}
      </div>

      <ErrorMessage error={requests.error} onRetry={requests.reload} />
      <ErrorMessage error={shelters.error} onRetry={shelters.reload} />
      {(requests.loading || shelters.loading) && !requests.data && !shelters.data && <Spinner />}

      <div className="card">
        <MapView points={points} height={520} label="Map of open requests and shelters" />
        <ul className="legend">
          {Object.entries(URGENCY_COLORS).reverse().map(([label, color]) => (
            <li key={label}><span className="swatch" style={{ background: color }} />{label} request</li>
          ))}
        </ul>
      </div>
      {canRequests && openRequests.length > located.length && (
        <p className="muted">
          {openRequests.length - located.length} open request(s) have no location and are not on the map. Open the request to add one or contact the person.{' '}
          <Link to="/staff/requests">Go to requests</Link>
        </p>
      )}
      {requests.data && requests.data.total > 100 && <p className="muted">Showing the 100 most recent requests of {requests.data.total}.</p>}
    </>
  );
}
