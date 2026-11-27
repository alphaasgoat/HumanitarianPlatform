import { useMemo, useState } from 'react';
import { useAuth } from '../../auth/AuthContext.jsx';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { focusFirstError, useForm } from '../../hooks/useForm.js';
import { userApi } from '../../api/endpoints.js';
import { PASSWORD_HINT, STAFF_ROLES, humanize, validateStaff } from '../../lib/validation.js';
import { Badge, EmptyState, ErrorMessage, Notice, Spinner, StatTile, TextField } from '../../components/ui.jsx';

function CreateStaff({ onCreated, onCancel }) {
  const { values, errors, setErrors, change } = useForm({ email: '', fullName: '', role: 'CaseManager', password: '' });
  const { busy, error, run } = useAction();

  async function onSubmit(e) {
    e.preventDefault();
    const found = validateStaff(values);
    setErrors(found);
    if (focusFirstError(found, ['email', 'fullName', 'role', 'password'])) return;
    const result = await run(() => userApi.createStaff({
      email: values.email.trim(), fullName: values.fullName.trim(), role: values.role, password: values.password, organisationId: null
    }));
    if (result.ok) onCreated(result.value);
    else if (result.error?.fieldErrors) setErrors(result.error.fieldErrors);
  }

  return (
    <form className="card form-grid" onSubmit={onSubmit} noValidate>
      <h2 className="span-2">Create a staff account</h2>
      <ErrorMessage error={error} />
      <TextField id="fullName" label="Full name" value={values.fullName} onChange={change} error={errors.fullName} />
      <TextField id="email" label="Email" type="email" autoComplete="off" value={values.email} onChange={change} error={errors.email} />
      <TextField id="role" label="Role" as="select" value={values.role} onChange={change} error={errors.role}>
        {STAFF_ROLES.map((r) => <option key={r} value={r}>{humanize(r)}</option>)}
      </TextField>
      <TextField id="password" label="Temporary password" type="text" autoComplete="off" value={values.password}
        onChange={change} error={errors.password} hint={PASSWORD_HINT} />
      <div className="span-2 actions">
        <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Creating…' : 'Create account'}</button>
        <button type="button" className="btn" onClick={onCancel}>Cancel</button>
      </div>
    </form>
  );
}

export default function Users() {
  const { user: me } = useAuth();
  const { data, error, loading, reload } = useApi((signal) => userApi.list(signal), []);
  const action = useAction();
  const [creating, setCreating] = useState(false);
  const [created, setCreated] = useState('');
  const [role, setRole] = useState('');
  const [search, setSearch] = useState('');

  const all = data ?? [];
  const shown = useMemo(() => {
    const q = search.trim().toLowerCase();
    return all.filter((u) => (!role || u.role === role) && (!q || u.email.toLowerCase().includes(q) || u.fullName.toLowerCase().includes(q)));
  }, [all, role, search]);
  const roles = [...new Set(all.map((u) => u.role))].sort();

  async function toggle(u) {
    const result = await action.run(() => userApi.setActive(u.id, !u.isActive));
    if (result.ok) reload();
  }

  return (
    <>
      <div className="page-head">
        <h1>Users</h1>
        {!creating && <button type="button" className="btn btn-primary" onClick={() => { setCreating(true); setCreated(''); }}>Create staff account</button>}
      </div>
      <Notice kind="success">{created}</Notice>
      {creating && (
        <CreateStaff onCancel={() => setCreating(false)}
          onCreated={(u) => { setCreating(false); setCreated(`Account created for ${u.fullName} (${humanize(u.role)}). Give them the temporary password in person.`); reload(); }} />
      )}

      <ErrorMessage error={error} onRetry={reload} />
      <ErrorMessage error={action.error} />
      {loading && !data && <Spinner />}

      {data && all.length > 0 && (
        <div className="stat-grid">
          <StatTile label="Accounts" value={all.length} />
          <StatTile label="Staff" value={all.filter((u) => u.role !== 'Citizen').length} />
          <StatTile label="Deactivated" value={all.filter((u) => !u.isActive).length} />
        </div>
      )}

      <div className="toolbar">
        <div>
          <label htmlFor="search">Search</label>
          <input id="search" type="search" placeholder="Name or email" value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <div>
          <label htmlFor="role-filter">Role</label>
          <select id="role-filter" value={role} onChange={(e) => setRole(e.target.value)}>
            <option value="">All</option>
            {roles.map((r) => <option key={r} value={r}>{humanize(r)}</option>)}
          </select>
        </div>
      </div>

      {data && shown.length === 0 && <EmptyState title="No users match." />}
      {shown.length > 0 && (
        <div className="card table-wrap">
          <table>
            <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Status</th><th><span className="sr-only">Actions</span></th></tr></thead>
            <tbody>
              {shown.map((u) => (
                <tr key={u.id}>
                  <td><strong>{u.fullName}</strong>{u.id === me.id && <span className="muted"> (you)</span>}</td>
                  <td>{u.email}</td>
                  <td>{humanize(u.role)}</td>
                  <td><Badge kind="active" value={u.isActive ? 'Active' : 'Inactive'} /></td>
                  <td>
                    {u.id !== me.id && (
                      <button type="button" className="btn btn-small" disabled={action.busy} onClick={() => toggle(u)}>
                        {u.isActive ? 'Deactivate' : 'Activate'}
                      </button>
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
