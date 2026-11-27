import { useState } from 'react';
import { useApi } from '../../hooks/useApi.js';
import { crisisApi } from '../../api/endpoints.js';
import { formatDate } from '../../lib/dates.js';
import { CRISIS_TYPES, humanize } from '../../lib/validation.js';
import { Badge, EmptyState, ErrorMessage, Notice, Spinner, TextField } from '../../components/ui.jsx';

const EMPTY = { name: '', type: 'Flood', country: 'Sri Lanka', description: '' };

function validate(v) {
  const e = {};
  const name = v.name.trim();
  if (name.length < 3 || name.length > 200) e.name = 'Name must be 3-200 characters.';
  if (!CRISIS_TYPES.includes(v.type)) e.type = 'Choose a type.';
  if (!v.country.trim() || v.country.trim().length > 100) e.country = 'Country is required (maximum 100 characters).';
  if (v.description.length > 2000) e.description = 'Description is too long (maximum 2000 characters).';
  return e;
}

export default function CrisisEvents() {
  const { data, error, loading, reload } = useApi((signal) => crisisApi.list(undefined, signal), []);
  const [values, setValues] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [submitError, setSubmitError] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [created, setCreated] = useState('');

  function change(e) {
    const { name, value } = e.target;
    setValues((v) => ({ ...v, [name]: value }));
    setErrors((er) => ({ ...er, [name]: undefined }));
  }

  async function onSubmit(e) {
    e.preventDefault();
    const found = validate(values);
    setErrors(found);
    if (Object.keys(found).length > 0) return;
    setSubmitting(true);
    setSubmitError(null);
    setCreated('');
    try {
      const c = await crisisApi.create({
        name: values.name.trim(),
        type: values.type,
        country: values.country.trim(),
        description: values.description.trim() || null
      });
      setCreated(`Crisis event "${c.name}" is now active. Citizens can submit requests for it.`);
      setValues(EMPTY);
      reload();
    } catch (err) {
      setSubmitError(err);
      if (err.fieldErrors) setErrors((er) => ({ ...er, ...err.fieldErrors }));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <div className="page-head"><h1>Crisis events</h1></div>

      <section className="card">
        <h2>Open a new crisis event</h2>
        <Notice kind="success">{created}</Notice>
        <ErrorMessage error={submitError} />
        <form className="form-grid" onSubmit={onSubmit} noValidate>
          <div className="span-2">
            <TextField id="name" label="Name" value={values.name} onChange={change} error={errors.name}
              placeholder="e.g. Kelani River floods, November 2026" maxLength={200} />
          </div>
          <TextField id="type" label="Type" as="select" value={values.type} onChange={change} error={errors.type}>
            {CRISIS_TYPES.map((t) => <option key={t} value={t}>{humanize(t)}</option>)}
          </TextField>
          <TextField id="country" label="Country" value={values.country} onChange={change} error={errors.country} maxLength={100} />
          <div className="span-2">
            <TextField id="description" label="Description (optional)" as="textarea" rows={3} maxLength={2000}
              value={values.description} onChange={change} error={errors.description} />
          </div>
          <div className="span-2 actions">
            <button type="submit" className="btn btn-primary" disabled={submitting}>
              {submitting ? 'Creating…' : 'Create crisis event'}
            </button>
          </div>
        </form>
      </section>

      <ErrorMessage error={error} onRetry={reload} />
      {loading && !data && <Spinner />}
      {data && data.length === 0 && <EmptyState title="No crisis events yet." >Create the first one above.</EmptyState>}

      {data && data.length > 0 && (
        <div className="card table-wrap">
          <table>
            <thead><tr><th>Name</th><th>Type</th><th>Country</th><th>Status</th><th>Started</th><th>Areas</th></tr></thead>
            <tbody>
              {data.map((c) => (
                <tr key={c.id}>
                  <td>{c.name}</td>
                  <td>{humanize(c.type)}</td>
                  <td>{c.country}</td>
                  <td><Badge kind="crisis" value={c.status} /></td>
                  <td>{formatDate(c.startedAtUtc)}</td>
                  <td>{c.areas.length}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
