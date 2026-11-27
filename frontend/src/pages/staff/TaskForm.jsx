import { Link, useNavigate } from 'react-router-dom';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { focusFirstError, useForm } from '../../hooks/useForm.js';
import { crisisApi, taskApi } from '../../api/endpoints.js';
import { SKILLS, buildTaskPayload, humanize, toLocalInput, validateTask } from '../../lib/validation.js';
import { ErrorMessage, TextField } from '../../components/ui.jsx';

const ORDER = ['crisisEventId', 'title', 'description', 'requiredSkill', 'volunteersNeeded', 'start', 'end', 'latitude', 'longitude'];

export default function TaskForm() {
  const navigate = useNavigate();
  const crises = useApi((signal) => crisisApi.list('Active', signal), []);
  const now = new Date();
  const { values, errors, setErrors, change } = useForm({
    crisisEventId: '', title: '', description: '', requiredSkill: 'General', volunteersNeeded: '2',
    latitude: '', longitude: '',
    start: toLocalInput(new Date(now.getTime() + 60 * 60 * 1000)),
    end: toLocalInput(new Date(now.getTime() + 5 * 60 * 60 * 1000))
  });
  const { busy, error, run } = useAction();
  const activeCrises = crises.data ?? [];

  async function onSubmit(e) {
    e.preventDefault();
    const found = validateTask(values);
    setErrors(found);
    if (focusFirstError(found, ORDER)) return;
    const result = await run(() => taskApi.create(buildTaskPayload(values)));
    if (result.ok) navigate(`/staff/tasks/${result.value.id}`);
    else if (result.error?.fieldErrors) setErrors(result.error.fieldErrors);
  }

  return (
    <>
      <p><Link to="/staff/tasks">← Back to tasks</Link></p>
      <div className="page-head"><h1>New volunteer task</h1></div>
      <form className="card form-grid" onSubmit={onSubmit} noValidate>
        <ErrorMessage error={error} />
        <ErrorMessage error={crises.error} onRetry={crises.reload} />
        <div className="span-2">
          <TextField id="crisisEventId" label="Crisis" as="select" value={values.crisisEventId} onChange={change} error={errors.crisisEventId}>
            <option value="">Choose…</option>
            {activeCrises.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </TextField>
        </div>
        <div className="span-2">
          <TextField id="title" label="Title" value={values.title} onChange={change} error={errors.title} maxLength={150}
            placeholder="e.g. Deliver food packs to the Palatota shelter" />
        </div>
        <div className="span-2">
          <TextField id="description" label="Description (optional)" as="textarea" rows={3} maxLength={1000}
            value={values.description} onChange={change} error={errors.description} />
        </div>
        <TextField id="requiredSkill" label="Skill needed" as="select" value={values.requiredSkill} onChange={change} error={errors.requiredSkill}>
          {SKILLS.map((s) => <option key={s} value={s}>{humanize(s)}</option>)}
        </TextField>
        <TextField id="volunteersNeeded" label="Volunteers needed" type="number" min="1" max="500" inputMode="numeric"
          value={values.volunteersNeeded} onChange={change} error={errors.volunteersNeeded} />
        <TextField id="start" label="Starts" type="datetime-local" value={values.start} onChange={change} error={errors.start} />
        <TextField id="end" label="Ends" type="datetime-local" value={values.end} onChange={change} error={errors.end} />
        <TextField id="latitude" label="Latitude (optional)" inputMode="decimal" value={values.latitude} onChange={change}
          error={errors.latitude} hint="Lets the matcher prefer volunteers who live nearby." />
        <TextField id="longitude" label="Longitude (optional)" inputMode="decimal" value={values.longitude} onChange={change} error={errors.longitude} />
        <div className="span-2 actions">
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Creating…' : 'Create task'}</button>
          <Link className="btn" to="/staff/tasks">Cancel</Link>
        </div>
      </form>
    </>
  );
}
