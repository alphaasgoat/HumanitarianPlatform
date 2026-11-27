import { useState } from 'react';
import { Link, Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';
import { postLoginPath } from '../auth/roles.js';
import { validateLogin } from '../lib/validation.js';
import { ErrorMessage, Notice, TextField } from '../components/ui.jsx';

// Development-only shortcuts for the demo accounts created by DataSeeder. Not included in production builds.
const DEMO_ACCOUNTS = import.meta.env.DEV
  ? [
      ['Citizen', 'citizen@demo.org', 'Citizen#12345'],
      ['Coordinator', 'coordinator@demo.org', 'Coord#12345'],
      ['Case manager', 'casemanager@demo.org', 'Case#123456'],
      ['Logistics', 'logistics@demo.org', 'Logi#123456'],
      ['Shelter admin', 'shelter@demo.org', 'Shel#123456'],
      ['Volunteer coord.', 'volunteers@demo.org', 'Volu#123456'],
      ['System admin', 'admin@demo.org', 'Admin#12345']
    ]
  : [];

export default function Login() {
  const { isAuthenticated, user, login, notice, clearNotice } = useAuth();
  const location = useLocation();
  const [values, setValues] = useState({ email: '', password: '' });
  const [errors, setErrors] = useState({});
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  // Already signed in (or just signed in): go to where they were heading, or their home page.
  if (isAuthenticated) return <Navigate to={postLoginPath(user.role, location.state?.from)} replace />;

  function change(e) {
    const { name, value } = e.target;
    setValues((v) => ({ ...v, [name]: value }));
    setErrors((er) => ({ ...er, [name]: undefined }));
  }

  async function onSubmit(e) {
    e.preventDefault();
    const found = validateLogin(values);
    setErrors(found);
    if (Object.keys(found).length > 0) return;
    setSubmitting(true);
    setError(null);
    clearNotice();
    try {
      await login(values.email, values.password); // success re-renders this page into the <Navigate> above
    } catch (err) {
      setError(err);
      setSubmitting(false);
    }
  }

  return (
    <div className="auth-page">
      <div className="card auth-card">
        <h1>Sign in</h1>
        <p className="muted">Humanitarian Crisis Response Platform</p>
        <Notice kind="warning">{notice}</Notice>
        <ErrorMessage error={error} />
        <form onSubmit={onSubmit} noValidate>
          <TextField id="email" label="Email" type="email" autoComplete="username" value={values.email}
            onChange={change} error={errors.email} />
          <TextField id="password" label="Password" type="password" autoComplete="current-password"
            value={values.password} onChange={change} error={errors.password} />
          <button type="submit" className="btn btn-primary btn-block" disabled={submitting}>
            {submitting ? 'Signing in…' : 'Sign in'}
          </button>
        </form>
        <p className="muted center">
          Affected by the crisis and need an account? <Link to="/register">Create a citizen account</Link>
        </p>

        {DEMO_ACCOUNTS.length > 0 && (
          <details className="demo">
            <summary>Demo accounts (development only)</summary>
            <ul>
              {DEMO_ACCOUNTS.map(([label, email, password]) => (
                <li key={email}>
                  <button type="button" className="link-button"
                    onClick={() => { setValues({ email, password }); setErrors({}); }}>
                    {label}
                  </button>
                  <span className="muted"> {email}</span>
                </li>
              ))}
            </ul>
          </details>
        )}
      </div>
    </div>
  );
}
