import { Fragment, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { focusFirstError, useForm } from '../../hooks/useForm.js';
import { shelterApi } from '../../api/endpoints.js';
import { formatDateTime } from '../../lib/dates.js';
import {
  SUPPLY_CATEGORIES, buildShelterPayload, validateShelter, validateSupplyRequest
} from '../../lib/validation.js';
import PromptButton from '../../components/PromptButton.jsx';
import ShelterFields from '../../components/ShelterFields.jsx';
import { Badge, EmptyState, ErrorMessage, Meter, Notice, ShortId, Spinner, TextField } from '../../components/ui.jsx';

function EditShelter({ shelter, onSaved, onCancel }) {
  const { values, errors, setErrors, change } = useForm({
    capacity: String(shelter.capacity),
    hasMedicalSupport: shelter.hasMedicalSupport,
    wheelchairAccessible: shelter.wheelchairAccessible,
    providesFood: shelter.providesFood,
    providesWater: shelter.providesWater,
    isPublic: shelter.isPublic,
    notes: shelter.notes ?? ''
  });
  const { busy, error, run } = useAction();

  async function onSubmit(e) {
    e.preventDefault();
    const found = validateShelter(values, { creating: false });
    setErrors(found);
    if (focusFirstError(found, ['capacity', 'notes'])) return;
    const result = await run(() => shelterApi.update(shelter.id, buildShelterPayload(values, { creating: false })));
    if (result.ok) onSaved(result.value);
    else if (result.error?.fieldErrors) setErrors(result.error.fieldErrors);
  }

  return (
    <form className="card form-grid" onSubmit={onSubmit} noValidate>
      <h2 className="span-2">Edit shelter</h2>
      <ErrorMessage error={error} />
      <ShelterFields values={values} errors={errors} change={change} creating={false} />
      <div className="span-2 actions">
        <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : 'Save changes'}</button>
        <button type="button" className="btn" onClick={onCancel}>Cancel</button>
      </div>
    </form>
  );
}

function SupplyRequestForm({ shelterId }) {
  const { values, errors, setErrors, change, set } = useForm({ category: 'Food', quantity: '10', note: '' });
  const { busy, error, run } = useAction();
  const [done, setDone] = useState('');

  async function onSubmit(e) {
    e.preventDefault();
    setDone('');
    const found = validateSupplyRequest(values);
    setErrors(found);
    if (focusFirstError(found, ['category', 'quantity', 'note'])) return;
    const result = await run(() => shelterApi.requestSupplies(shelterId, {
      category: values.category, quantity: Number(values.quantity), note: values.note.trim() || null
    }));
    if (result.ok) {
      setDone('Supply request sent to logistics.');
      set({ quantity: '10', note: '' });
    } else if (result.error?.fieldErrors) setErrors(result.error.fieldErrors);
  }

  return (
    <section className="card">
      <h2>Ask logistics for supplies</h2>
      <Notice kind="success">{done}</Notice>
      <ErrorMessage error={error} />
      <form className="form-grid" onSubmit={onSubmit} noValidate>
        <TextField id="category" label="What is needed?" as="select" value={values.category} onChange={change} error={errors.category}>
          {SUPPLY_CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
        </TextField>
        <TextField id="quantity" label="Quantity" type="number" min="1" inputMode="numeric" value={values.quantity}
          onChange={change} error={errors.quantity} />
        <div className="span-2">
          <TextField id="note" label="Note (optional)" value={values.note} onChange={change} error={errors.note} maxLength={300} />
        </div>
        <div className="span-2 actions">
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Sending…' : 'Send request'}</button>
        </div>
      </form>
    </section>
  );
}

export default function ShelterDetail() {
  const { id } = useParams();
  const { hasRole } = useAuth();
  const canManage = hasRole(GROUPS.shelterManage);
  const canSeeRequests = hasRole(GROUPS.requestStaff);
  const [editing, setEditing] = useState(false);
  const [activeOnly, setActiveOnly] = useState(true);
  const shelter = useApi((signal) => shelterApi.get(id, signal), [id]);
  const allocations = useApi((signal) => shelterApi.allocations(id, activeOnly, signal), [id, activeOnly]);
  const statusAction = useAction();

  const s = shelter.data;

  function refresh() {
    shelter.reload();
    allocations.reload();
  }

  async function toggleStatus() {
    const next = s.status === 'Open' ? 'Closed' : 'Open';
    const result = await statusAction.run(() => shelterApi.setStatus(s.id, next));
    if (result.ok) shelter.setData(result.value);
  }

  return (
    <>
      <p><Link to="/staff/shelters">← Back to shelters</Link></p>
      <ErrorMessage error={shelter.error} onRetry={shelter.reload} />
      {shelter.loading && !s && <Spinner />}

      {s && (
        <>
          <div className="page-head">
            <h1>{s.name}</h1>
            <div className="badges">
              <Badge kind="shelter" value={s.status} />
              {!s.isPublic && <span className="badge">Not public</span>}
            </div>
          </div>

          <section className="card">
            <p className="muted">{s.address}</p>
            <Meter percent={s.occupancyPercent} label={`${s.occupancyPercent}% full`} />
            <p className="meter-caption">
              <strong>{s.occupancy}</strong> of {s.capacity} places used
              {s.isFull ? <span className="full-flag"> · FULL</span> : <> · {s.availableSpaces} left</>}
            </p>
            <ul className="features">
              <li className={s.wheelchairAccessible ? 'feature on' : 'feature off'}>{s.wheelchairAccessible ? '✓' : '✗'} Wheelchair access</li>
              <li className={s.hasMedicalSupport ? 'feature on' : 'feature off'}>{s.hasMedicalSupport ? '✓' : '✗'} Medical support</li>
              <li className={s.providesFood ? 'feature on' : 'feature off'}>{s.providesFood ? '✓' : '✗'} Food</li>
              <li className={s.providesWater ? 'feature on' : 'feature off'}>{s.providesWater ? '✓' : '✗'} Water</li>
            </ul>
            {s.notes && <p>{s.notes}</p>}
            {canManage && (
              <div className="actions">
                <button type="button" className="btn" onClick={() => setEditing((v) => !v)}>{editing ? 'Close editor' : 'Edit details'}</button>
                <button type="button" className="btn" onClick={toggleStatus} disabled={statusAction.busy}>
                  {s.status === 'Open' ? 'Close shelter' : 'Reopen shelter'}
                </button>
              </div>
            )}
            <ErrorMessage error={statusAction.error} />
          </section>

          {canManage && editing && (
            <EditShelter shelter={s} onCancel={() => setEditing(false)}
              onSaved={(updated) => { shelter.setData(updated); setEditing(false); }} />
          )}

          <section className="card table-wrap">
            <div className="card-pad head-row">
              <h2>Households placed here</h2>
              <label className="inline-check">
                <input type="checkbox" checked={activeOnly} onChange={(e) => setActiveOnly(e.target.checked)} /> Only people currently staying
              </label>
            </div>
            <ErrorMessage error={allocations.error} onRetry={allocations.reload} />
            {allocations.loading && !allocations.data && <Spinner />}
            {allocations.data && allocations.data.length === 0 && (
              <EmptyState title={activeOnly ? 'Nobody is placed here right now.' : 'No placements yet.'}>
                Households are placed from a triaged request, using the shelter suggestions on the request page.
              </EmptyState>
            )}
            {allocations.data && allocations.data.length > 0 && (
              <table>
                <thead>
                  <tr><th>Request</th><th>People</th><th>Status</th><th>Placed</th><th>By</th><th>Note</th><th><span className="sr-only">Actions</span></th></tr>
                </thead>
                <tbody>
                  {allocations.data.map((a) => (
                    <Fragment key={a.id}>
                      <tr>
                        <td>{canSeeRequests ? <Link to={`/staff/requests/${a.requestId}`}><ShortId id={a.requestId} /></Link> : <ShortId id={a.requestId} />}</td>
                        <td>{a.peopleCount}</td>
                        <td><Badge kind="alloc" value={a.status} /></td>
                        <td>{formatDateTime(a.allocatedAtUtc)}</td>
                        <td>{a.allocatedByEmail ?? '—'}</td>
                        <td className="wrap">{a.releaseReason ?? a.note ?? ''}</td>
                        <td>
                          {canManage && a.status === 'Active' && (
                            <PromptButton label="Check out" confirmLabel="Check out" placeholder="Reason (min. 3 characters)"
                              required minLength={3} inputProps={{ maxLength: 300 }}
                              onSubmit={async (reason) => { await shelterApi.release(a.id, reason); refresh(); }} />
                          )}
                        </td>
                      </tr>
                      {a.warnings?.length > 0 && (
                        <tr><td colSpan={7} className="warn-row">{a.warnings.join(' · ')}</td></tr>
                      )}
                    </Fragment>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          {canManage && <SupplyRequestForm shelterId={s.id} />}
        </>
      )}
    </>
  );
}
