import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';
import { homePathFor } from '../auth/roles.js';

export default function NotFound() {
  const { user } = useAuth();
  return (
    <div className="card narrow">
      <h1>Page not found</h1>
      <p>There is nothing at this address.</p>
      <Link className="btn btn-primary" to={homePathFor(user.role)}>Back to my home page</Link>
    </div>
  );
}
