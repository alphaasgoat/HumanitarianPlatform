import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useApi } from '../hooks/useApi.js';
import { useAction } from '../hooks/useAction.js';
import { mapsApi, shelterApi } from '../api/endpoints.js';
import MapView from './MapView.jsx';
import { ErrorMessage, Notice } from './ui.jsx';

/**
 * Ranked shelter suggestions for a request, with their warnings, a map and (when the request has a location) the road
 * distance to each shelter from the external routing service. Placing is done by a person; the server re-checks
 * capacity at that moment (HTTP 409 if the shelter filled up meanwhile).
 */
export default function ShelterPlacement({ request, canPlace }) {
  const find = useAction();
  const place = useAction();
  const route = useAction();
  const [result, setResult] = useState(null);
  const [people, setPeople] = useState(String(request.peopleAffected));
  const [done, setDone] = useState('');
  const [routes, setRoutes] = useState({});
  const shelterList = useApi((signal) => shelterApi.list(undefined, signal), []);

  const hasLocation = request.latitude != null && request.longitude != null;
  const coordsById = useMemo(
    () => new Map((shelterList.data ?? []).filter((s) => s.latitude != null && s.longitude != null).map((s) => [s.id, s])), [shelterList.data]
  );

  const points = useMemo(() => {
    const list = [];
    if (hasLocation) {
      list.push({ id: 'request', lat: request.latitude, lng: request.longitude, color: '#b3261e', radius: 9, lines: ['This request', request.addressText] });
    }
    (result?.candidates ?? []).forEach((c, i) => {
      const s = coordsById.get(c.shelterId);
      if (s) list.push({ id: c.shelterId, lat: s.latitude, lng: s.longitude, color: '#1a5fd0', radius: 12, lines: [`${i + 1}. ${c.name}`, `${c.availableSpaces} places left`, s.address] });
    });
    return list;
  }, [result, coordsById, hasLocation, request.latitude, request.longitude, request.addressText]);

  async function load() {
    const res = await find.run(() => shelterApi.recommend(request.id));
    if (res.ok) {
      setResult(res.value);
      setRoutes({});
    }
  }

  async function showRoute(candidate) {
    const s = coordsById.get(candidate.shelterId);
    if (!s || !hasLocation) return;
    const res = await route.run(() => mapsApi.route(request.latitude, request.longitude, s.latitude, s.longitude));
    if (res.ok) {
      const r = res.value;
      setRoutes((prev) => ({
        ...prev,
        [candidate.shelterId]: `${r.distanceKm} km, about ${r.durationMinutes} min${r.isEstimate ? ' (estimate: the routing service is unavailable)' : ' by road'}`
      }));
    }
  }

  async function placeHere(candidate) {
    setDone('');
    const count = Number(people);
    const res = await place.run(() => shelterApi.allocate({
      requestId: request.id,
      shelterId: candidate.shelterId,
      peopleCount: Number.isInteger(count) && count > 0 ? count : null,
      note: null
    }));
    if (res.ok) {
      const warnings = res.value.warnings?.length ? ` Warnings: ${res.value.warnings.join('; ')}` : '';
      setDone(`${res.value.peopleCount} people placed in ${candidate.name}. The person has been notified.${warnings}`);
      load(); // spaces changed, refresh the ranking
    }
  }

  return (
    <section className="card">
      <div className="head-row">
        <h2>Find a shelter</h2>
        <button type="button" className="btn btn-primary" onClick={load} disabled={find.busy}>
          {find.busy ? 'Looking…' : result ? 'Refresh' : 'Suggest shelters'}
        </button>
      </div>
      <p className="muted">
        Shelters that are closed, full or not wheelchair accessible (when that is needed) are never suggested.
        Nearer shelters and shelters with a free-space margin rank higher.
        {!hasLocation && ' This request has no location, so distances and routes are not available.'}
      </p>
      <ErrorMessage error={find.error} />
      <ErrorMessage error={place.error} />
      <ErrorMessage error={route.error} />
      <Notice kind="success">{done}</Notice>

      {result && (
        <>
          <p>{result.message}</p>
          {points.length > 0 && <MapView points={points} height={260} label="Request and suggested shelters" />}
          {canPlace && result.candidates.length > 0 && (
            <div className="toolbar">
              <label htmlFor="people">People to place</label>
              <input id="people" type="number" min="1" max="10000" value={people} onChange={(e) => setPeople(e.target.value)} />
            </div>
          )}
          <ul className="match-list">
            {result.candidates.map((c, i) => (
              <li className="match" key={c.shelterId}>
                <div className="match-rank">{i + 1}</div>
                <div className="match-body">
                  <strong><Link to={`/staff/shelters/${c.shelterId}`}>{c.name}</Link></strong>{' '}
                  <span className="muted">· {c.availableSpaces} places left · score {Math.round(c.score)}{c.distanceKm != null ? ` · ${c.distanceKm.toFixed(1)} km straight line` : ''}</span>
                  <div className="muted">{c.rationale}</div>
                  {c.warnings?.map((w) => <div className="warn-line" key={w}>⚠ {w}</div>)}
                  {routes[c.shelterId] && <div className="route-line">Road: {routes[c.shelterId]}</div>}
                </div>
                <div className="actions">
                  {hasLocation && coordsById.has(c.shelterId) && !routes[c.shelterId] && (
                    <button type="button" className="btn btn-small" disabled={route.busy} onClick={() => showRoute(c)}>Road distance</button>
                  )}
                  {canPlace && (
                    <button type="button" className="btn btn-small btn-primary" disabled={place.busy} onClick={() => placeHere(c)}>Place here</button>
                  )}
                </div>
              </li>
            ))}
          </ul>
          {!canPlace && result.candidates.length > 0 && <p className="muted">Only a coordinator or shelter administrator can place a household.</p>}
        </>
      )}
    </section>
  );
}
