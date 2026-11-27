import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useApi } from '../../hooks/useApi.js';
import { crisisApi, requestApi } from '../../api/endpoints.js';
import {
  CATEGORIES, URGENCIES, buildSubmitPayload, humanize, newIdempotencyKey, validateRequest
} from '../../lib/validation.js';
import LocateButton from '../../components/LocateButton.jsx';
import { EmptyState, ErrorMessage, Spinner, TextField } from '../../components/ui.jsx';

// Change this one line if the platform is deployed in another country.
const EMERGENCY_NOTE = 'If someone is in immediate danger, call the emergency services first: 119 (police) or 1990 (ambulance).';

const FIELD_ORDER = ['crisisEventId', 'category', 'declaredUrgency', 'description', 'peopleAffected', 'addressText', 'latitude', 'longitude'];

const URGENCY_HINTS = {
  Low: 'Can wait a few days',
  Medium: 'Needed soon',
  High: 'Needed within hours',
  Critical: 'Danger to life right now'
};

export default function NewRequest() {
  const navigate = useNavigate();
  const crises = useApi((signal) => crisisApi.list('Active', signal), []);
  const [values, setValues] = useState({
    crisisEventId: '', category: '', declaredUrgency: 'Medium', description: '',
    peopleAffected: '1', addressText: '', latitude: '', longitude: ''
  });
  const [errors, setErrors] = useState({});
  const [submitError, setSubmitError] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [geoMessage, setGeoMessage] = useState('');

  // One key per "intent to submit". If the network drops and the person taps Submit again, the SAME key is sent,
  // so the API returns the request it already created instead of making a duplicate (idempotent submission).
  const idempotencyKey = useRef(newIdempotencyKey());

  const activeCrises = crises.data ?? [];

  // Only one active crisis? Select it for them.
  useEffect(() => {
    if (activeCrises.length === 1) {
      setValues((v) => (v.crisisEventId ? v : { ...v, crisisEventId: activeCrises[0].id }));
    }
  }, [activeCrises]);

  function change(e) {
    const { name, value } = e.target;
    setValues((v) => ({ ...v, [name]: value }));
    setErrors((er) => ({ ...er, [name]: undefined }));
  }

  function fillFromDevice() {
    if (!navigator.geolocation) {
      setGeoMessage('Location is not available in this browser. Please type the address instead.');
      return;
    }
    setGeoMessage('Finding your location…');
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setValues((v) => ({
          ...v,
          latitude: pos.coords.latitude.toFixed(6),
          longitude: pos.coords.longitude.toFixed(6)
        }));
        setErrors((er) => ({ ...er, latitude: undefined, longitude: undefined }));
        setGeoMessage('Location added.');
      },
      () => setGeoMessage('Could not get your location. You can type the address instead.'),
      { enableHighAccuracy: true, timeout: 10000 }
    );
  }

  async function onSubmit(e) {
    e.preventDefault();
    if (submitting) return;

    const found = validateRequest(values);
    setErrors(found);
    const firstBad = FIELD_ORDER.find((f) => found[f]);
    if (firstBad) {
      document.getElementById(firstBad)?.focus();
      return;
    }

    setSubmitting(true);
    setSubmitError(null);
    try {
      const { detail, wasExisting } = await requestApi.submit(buildSubmitPayload(values, idempotencyKey.current));
      idempotencyKey.current = newIdempotencyKey(); // the next request is a new intent
      navigate(`/citizen/requests/${detail.request.id}`, { state: { submitted: true, wasExisting } });
    } catch (err) {
      setSubmitError(err);
      if (err.fieldErrors) setErrors((er) => ({ ...er, ...err.fieldErrors }));
      setSubmitting(false);
    }
  }

  const descLength = values.description.trim().length;

  return (
    <>
      <div className="page-head"><h1>Ask for help</h1></div>
      <div className="alert alert-warning" role="note">{EMERGENCY_NOTE}</div>

      {crises.loading && <Spinner label="Loading crisis events…" />}
      <ErrorMessage error={crises.error} onRetry={crises.reload} />

      {!crises.loading && !crises.error && activeCrises.length === 0 && (
        <EmptyState title="There is no active crisis event right now.">
          A coordinator has to open a crisis event before requests can be submitted. Please check again later.
        </EmptyState>
      )}

      {activeCrises.length > 0 && (
        <form className="card form-grid" onSubmit={onSubmit} noValidate>
          <ErrorMessage error={submitError} />

          <TextField id="crisisEventId" label="Which crisis is this about?" as="select" value={values.crisisEventId}
            onChange={change} error={errors.crisisEventId} required>
            <option value="">Choose…</option>
            {activeCrises.map((c) => (
              <option key={c.id} value={c.id}>{c.name} ({humanize(c.type)}, {c.country})</option>
            ))}
          </TextField>

          <TextField id="category" label="What do you need?" as="select" value={values.category}
            onChange={change} error={errors.category} required>
            <option value="">Choose…</option>
            {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
          </TextField>

          <TextField id="declaredUrgency" label="How urgent is it?" as="select" value={values.declaredUrgency}
            onChange={change} error={errors.declaredUrgency}
            hint="Staff will confirm the final urgency after review." required>
            {URGENCIES.map((u) => <option key={u} value={u}>{u} - {URGENCY_HINTS[u]}</option>)}
          </TextField>

          <TextField id="peopleAffected" label="How many people need help?" type="number" min="1" max="10000"
            inputMode="numeric" value={values.peopleAffected} onChange={change} error={errors.peopleAffected} required />

          <div className="span-2">
            <TextField id="description" label="Describe the situation" as="textarea" rows={5} maxLength={2000}
              value={values.description} onChange={change} error={errors.description}
              hint={`${descLength}/2000 characters (minimum 10). Do not include passwords or bank details.`} required />
          </div>

          <div className="span-2">
            <TextField id="addressText" label="Where are you? (address or landmark)" maxLength={300}
              value={values.addressText} onChange={change} error={errors.addressText}
              autoComplete="street-address" />
            <LocateButton address={values.addressText} label="Find my location from this address"
              onFound={(p) => { setValues((v) => ({ ...v, ...p })); setErrors((er) => ({ ...er, latitude: undefined, longitude: undefined })); }} />
          </div>

          <fieldset className="span-2 location">
            <legend>Exact location (optional)</legend>
            <p className="muted">
              Sharing your coordinates helps responders find you faster and spot duplicate requests.
              They are only used for this request.
            </p>
            <div className="location-row">
              <TextField id="latitude" label="Latitude" inputMode="decimal" value={values.latitude}
                onChange={change} error={errors.latitude} />
              <TextField id="longitude" label="Longitude" inputMode="decimal" value={values.longitude}
                onChange={change} error={errors.longitude} />
              <button type="button" className="btn" onClick={fillFromDevice}>Use my current location</button>
            </div>
            {geoMessage && <p className="muted" role="status">{geoMessage}</p>}
          </fieldset>

          <div className="span-2 actions">
            <button type="submit" className="btn btn-primary" disabled={submitting}>
              {submitting ? 'Sending…' : 'Submit request'}
            </button>
            <Link className="btn" to="/citizen">Cancel</Link>
          </div>
        </form>
      )}
    </>
  );
}
