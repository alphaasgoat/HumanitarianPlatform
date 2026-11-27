import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';
import { navFor } from '../auth/roles.js';
import { humanize } from '../lib/validation.js';
import NotificationBell from './NotificationBell.jsx';

export default function Layout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const items = navFor(user.role);

  function handleLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  return (
    <div className="app">
      <a className="skip-link" href="#main">Skip to content</a>
      <header className="topbar">
        <div className="topbar-inner">
          <span className="brand"><span className="brand-mark" aria-hidden="true">+</span> Humanitarian Platform</span>
          <nav aria-label="Main">
            <ul className="nav">
              {items.map((i) => (
                <li key={i.to}>
                  <NavLink to={i.to} end={i.end} className={({ isActive }) => (isActive ? 'active' : undefined)}>
                    {i.label}
                  </NavLink>
                </li>
              ))}
            </ul>
          </nav>
          <div className="whoami">
            <NotificationBell />
            <span className="whoami-name">{user.fullName}</span>
            <span className="role-pill">{humanize(user.role)}</span>
            <button type="button" className="btn btn-small" onClick={handleLogout}>Sign out</button>
          </div>
        </div>
      </header>
      <main id="main" className="container">
        <Outlet />
      </main>
      <footer className="footer">Humanitarian Crisis Response Platform · prototype</footer>
    </div>
  );
}
