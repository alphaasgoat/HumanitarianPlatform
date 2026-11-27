import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';
import { homePathFor } from '../auth/roles.js';

export default function Forbidden() {
  const { user } = useAuth();
  return (
    <div className="card narrow">
      <h1>Access denied</h1>
      <p>Your role does not have permission to open that page.</p>
      <Link className="btn btn-primary" to={homePathFor(user.role)}>Back to my home page</Link>
    </div>
  );
}
