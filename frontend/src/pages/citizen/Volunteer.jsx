import { useState } from 'react';
import { useAuth } from '../../auth/AuthContext.jsx';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { focusFirstError, useForm } from '../../hooks/useForm.js';
import { taskApi, volunteerApi } from '../../api/endpoints.js';
import { formatDateTime } from '../../lib/dates.js';
import {
  SKILLS, buildVolunteerPayload, humanize, toLocalInput, toUtcIso, validateAvailability, validateVolunteer
} from '../../lib/validation.js';
import PromptButton from '../../components/PromptButton.jsx';
import { Badge, Chips, EmptyState, ErrorMessage, Notice, Spinner, TextField } from '../../components/ui.jsx';

function ProfileForm({ initial, isNew, onSaved, onCancel }) {
  const { values, errors, setErrors, change, set } = useForm(initial);
  const { busy, error, run } = useAction();
  const [geo, setGeo] = useState('');
  const skillsChanged = !isNew && [...values.skills].sort().join() !== [...initial.skills].sort().join();

  function toggleSkill(skill) {
    const has = values.skills.includes(skill);
    set({ skills: has ? values.skills.filter((s) => s !== skill) : [...values.skills, skill] });
    setErrors((er) => ({ ...er, skills: undefined }));
  }

  function fillFromDevice() {
    if (!navigator.geolocation) { setGeo('Location is not available in this browser.'); return; }
    setGeo('Finding your location…');
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        set({ latitude: pos.coords.latitude.toFixed(6), longitude: pos.coords.longitude.toFixed(6) });
        setGeo('Location added.');
      },
      () => setGeo('Could not get your location. You can leave it empty.'),
      { enableHighAccuracy: true, timeout: 10000 }
    );
  }

  async function onSubmit(e) {
    e.preventDefault();
    const found = validateVolunteer(values);
    setErrors(found);
    if (found.skills) { document.getElementById('skill-General')?.focus(); return; }
    if (focusFirstError(found, ['fullName', 'phone', 'latitude', 'longitude'])) return;
    const payload = buildVolunteerPayload(values);
    const result = await run(() => (isNew ? volunteerApi.register(payload) : volunteerApi.updateMe(payload)));
    if (result.ok) onSaved(result.value);
    else if (result.error?.fieldErrors) setErrors(result.error.fieldErrors);
  }

  return (
    <form className="card form-grid" onSubmit={onSubmit} noValidate>
      <h2 className="span-2">{isNew ? 'Become a volunteer' : 'Edit my volunteer profile'}</h2>
      <ErrorMessage error={error} />
      <TextField id="fullName" label="Full name" value={values.fullName} onChange={change} error={errors.fullName} />
      <TextField id="phone" label="Phone (optional)" type="tel" value={values.phone} onChange={change} error={errors.phone} />

      <fieldset className="span-2 location">
        <legend>Your skills</legend>
        <div className="checks skill-grid">
          {SKILLS.map((s) => (
            <div className="check-field" key={s}>
              <input id={`skill-${s}`} type="checkbox" checked={values.skills.includes(s)} onChange={() => toggleSkill(s)} />
              <label htmlFor={`skill-${s}`}>{humanize(s)}</label>
            </div>
          ))}
        </div>
        {errors.skills && <small className="error-text" role="alert">{errors.skills}</small>}
        {skillsChanged && (
          <div className="alert alert-warning" role="note">
            Changing your skills means a coordinator has to verify you again before you can get tasks.
          </div>
        )}
      </fieldset>

      <fieldset className="span-2 location">
        <legend>Where do you live? (optional)</legend>
        <p className="muted">Used only to suggest tasks close to you.</p>
        <div className="location-row">
          <TextField id="latitude" label="Latitude" inputMode="decimal" value={values.latitude} onChange={change} error={errors.latitude} />
          <TextField id="longitude" label="Longitude" inputMode="decimal" value={values.longitude} onChange={change} error={errors.longitude} />
          <button type="button" className="btn" onClick={fillFromDevice}>Use my current location</button>
        </div>
        {geo && <p className="muted" role="status">{geo}</p>}
      </fieldset>

      <div className="span-2 actions">
        <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Saving…' : isNew ? 'Register as volunteer' : 'Save changes'}</button>
        {onCancel && <button type="button" className="btn" onClick={onCancel}>Cancel</button>}
      </div>
    </form>
  );
}

function Availability() {
  const slots = useApi((signal) => volunteerApi.myAvailability(signal), []);
  const now = new Date();
  const { values, errors, setErrors, change } = useForm({
    start: toLocalInput(new Date(now.getTime() + 60 * 60 * 1000)),
    end: toLocalInput(new Date(now.getTime() + 5 * 60 * 60 * 1000))
  });
  const { busy, error, run } = useAction();

  async function onSubmit(e) {
    e.preventDefault();
    const found = validateAvailability(values);
    setErrors(found);
    if (focusFirstError(found, ['start', 'end'])) return;
    const result = await run(() => volunteerApi.addAvailability({ startUtc: toUtcIso(values.start), endUtc: toUtcIso(values.end) }));
    if (result.ok) slots.reload();
    else if (result.error?.fieldErrors) setErrors(result.error.fieldErrors);
  }

  return (
    <section className="card">
      <h2>When I am free</h2>
      <p className="muted">A coordinator can only give you a task if the whole shift falls inside a time you added here.</p>
      <ErrorMessage error={slots.error} onRetry={slots.reload} />
      {slots.loading && !slots.data && <Spinner />}
      {slots.data && slots.data.length === 0 && <p className="muted">You have not added any free time yet.</p>}
      {slots.data && slots.data.length > 0 && (
        <ul className="plain slot-list">
          {slots.data.map((s) => (
            <li key={s.id}><span className="muted">From</span> {formatDateTime(s.startUtc)} <span className="muted">to</span> {formatDateTime(s.endUtc)}</li>
          ))}
        </ul>
      )}
      <ErrorMessage error={error} />
      <form className="form-grid" onSubmit={onSubmit} noValidate>
        <TextField id="start" label="From" type="datetime-local" value={values.start} onChange={change} error={errors.start} />
        <TextField id="end" label="To" type="datetime-local" value={values.end} onChange={change} error={errors.end} />
        <div className="span-2 actions">
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Adding…' : 'Add free time'}</button>
        </div>
      </form>
    </section>
  );
}

function MyAssignments() {
  const { data, error, loading, reload } = useApi((signal) => taskApi.myAssignments(signal), []);
  const action = useAction();

  async function accept(id) {
    const result = await action.run(() => taskApi.accept(id));
    if (result.ok) reload();
  }

  return (
    <section className="card table-wrap">
      <div className="card-pad"><h2>My assignments</h2></div>
      <ErrorMessage error={error} onRetry={reload} />
      <ErrorMessage error={action.error} />
      {loading && !data && <Spinner />}
      {data && data.length === 0 && <EmptyState title="No assignments yet.">When a coordinator assigns you a task it will appear here.</EmptyState>}
      {data && data.length > 0 && (
        <table>
          <thead><tr><th>Task</th><th>When</th><th>Status</th><th><span className="sr-only">Actions</span></th></tr></thead>
          <tbody>
            {data.map((a) => (
              <tr key={a.id}>
                <td><strong>{a.taskTitle ?? 'Task'}</strong></td>
                <td>{formatDateTime(a.taskStartUtc)}<div className="muted">to {formatDateTime(a.taskEndUtc)}</div></td>
                <td><Badge kind="assign" value={a.status} />{a.hoursWorked != null && <div className="muted">{a.hoursWorked} h</div>}</td>
                <td>
                  {a.status === 'Assigned' && (
                    <div className="actions">
                      <button type="button" className="btn btn-small btn-primary" disabled={action.busy} onClick={() => accept(a.id)}>Accept</button>
                      <PromptButton label="Decline" confirmLabel="Decline" placeholder="Reason (optional)" inputProps={{ maxLength: 300 }}
                        onSubmit={async (reason) => { await taskApi.decline(a.id, reason); reload(); }} />
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

export default function Volunteer() {
  const { user } = useAuth();
  const me = useApi((signal) => volunteerApi.me(signal), []);
  const [editing, setEditing] = useState(false);

  const notRegistered = me.error?.status === 404;
  const v = me.data;

  return (
    <>
      <div className="page-head"><h1>Volunteer</h1></div>

      {me.loading && !v && !me.error && <Spinner />}
      {me.error && !notRegistered && <ErrorMessage error={me.error} onRetry={me.reload} />}

      {notRegistered && (
        <>
          <p className="muted">Want to help others? Tell us your skills. A coordinator will verify your profile before giving you tasks.</p>
          <ProfileForm isNew onSaved={me.setData}
            initial={{ fullName: user.fullName ?? '', phone: '', skills: [], latitude: '', longitude: '' }} />
        </>
      )}

      {v && (
        <>
          <section className="card">
            <div className="card-head">
              <h2>{v.fullName}</h2>
              <div className="badges">
                <Badge kind="verify" value={v.isVerified ? 'Verified' : 'Pending'} />
                <Badge kind="active" value={v.isActive ? 'Active' : 'Inactive'} />
              </div>
            </div>
            {!v.isVerified && (
              <Notice kind="info">Waiting for a coordinator to verify your profile. You can add your free time meanwhile.</Notice>
            )}
            <Chips items={v.skills} />
            <p className="muted">{v.completedTasks} tasks completed · {v.totalHours} hours given</p>
            {!editing && <button type="button" className="btn" onClick={() => setEditing(true)}>Edit profile</button>}
          </section>

          {editing && (
            <ProfileForm onCancel={() => setEditing(false)} onSaved={(updated) => { me.setData(updated); setEditing(false); }}
              initial={{
                fullName: v.fullName, phone: v.phone ?? '', skills: v.skills,
                latitude: v.latitude ?? '', longitude: v.longitude ?? ''
              }} />
          )}

          <Availability />
          <MyAssignments />
        </>
      )}
    </>
  );
}
