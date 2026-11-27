import { useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';
import { homePathFor } from '../auth/roles.js';
import { PASSWORD_HINT, validateRegister } from '../lib/validation.js';
import { ErrorMessage, TextField } from '../components/ui.jsx';

const EMPTY = { fullName: '', email: '', phone: '', preferredLanguage: 'en', password: '', confirmPassword: '' };

export default function Register() {
  const { isAuthenticated, user, register } = useAuth();
  const [values, setValues] = useState(EMPTY);
  const [errors, setErrors] = useState({});
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  if (isAuthenticated) return <Navigate to={homePathFor(user.role)} replace />;

  function change(e) {
    const { name, value } = e.target;
    setValues((v) => ({ ...v, [name]: value }));
    setErrors((er) => ({ ...er, [name]: undefined }));
  }

  async function onSubmit(e) {
    e.preventDefault();
    const found = validateRegister(values);
    setErrors(found);
    if (Object.keys(found).length > 0) return;
    setSubmitting(true);
    setError(null);
    try {
      await register({
        email: values.email.trim(),
        password: values.password,
        fullName: values.fullName.trim(),
        phone: values.phone.trim() || null,
        preferredLanguage: values.preferredLanguage || null
      });
      // signed in automatically -> the guard above redirects to the citizen home
    } catch (err) {
      setError(err);
      setErrors((er) => ({ ...er, ...err.fieldErrors }));
      setSubmitting(false);
    }
  }

  return (
    <div className="auth-page">
      <div className="card auth-card">
        <h1>Create a citizen account</h1>
        <p className="muted">Staff accounts are created by a system administrator.</p>
        <ErrorMessage error={error} />
        <form onSubmit={onSubmit} noValidate>
          <TextField id="fullName" label="Full name" autoComplete="name" value={values.fullName}
            onChange={change} error={errors.fullName} />
          <TextField id="email" label="Email" type="email" autoComplete="email" value={values.email}
            onChange={change} error={errors.email} />
          <TextField id="phone" label="Phone (optional)" type="tel" autoComplete="tel" value={values.phone}
            onChange={change} error={errors.phone} />
          <TextField id="preferredLanguage" label="Preferred language" as="select" value={values.preferredLanguage}
            onChange={change} error={errors.preferredLanguage}>
            <option value="en">English</option>
            <option value="si">සිංහල</option>
            <option value="ta">தமிழ்</option>
          </TextField>
          <TextField id="password" label="Password" type="password" autoComplete="new-password"
            value={values.password} onChange={change} error={errors.password}
            hint={PASSWORD_HINT} />
          <TextField id="confirmPassword" label="Confirm password" type="password" autoComplete="new-password"
            value={values.confirmPassword} onChange={change} error={errors.confirmPassword} />
          <button type="submit" className="btn btn-primary btn-block" disabled={submitting}>
            {submitting ? 'Creating account…' : 'Create account'}
          </button>
        </form>
        <p className="muted center">Already registered? <Link to="/login">Sign in</Link></p>
      </div>
    </div>
  );
}
