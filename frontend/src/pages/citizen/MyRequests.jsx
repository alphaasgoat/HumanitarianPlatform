import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useApi } from '../../hooks/useApi.js';
import { requestApi } from '../../api/endpoints.js';
import { formatDateTime } from '../../lib/dates.js';
import { Badge, EmptyState, ErrorMessage, Pagination, Spinner } from '../../components/ui.jsx';

const PAGE_SIZE = 10;

export default function MyRequests() {
  const [page, setPage] = useState(1);
  const { data, error, loading, reload } = useApi(
    (signal) => requestApi.mine({ page, pageSize: PAGE_SIZE }, signal),
    [page]
  );

  return (
    <>
      <div className="page-head">
        <h1>My requests</h1>
        <Link className="btn btn-primary" to="/citizen/requests/new">Ask for help</Link>
      </div>

      <ErrorMessage error={error} onRetry={reload} />
      {loading && !data && <Spinner />}

      {data && data.items.length === 0 && (
        <EmptyState title="You have not asked for help yet.">
          When you submit a request it will appear here so you can follow what happens to it.
        </EmptyState>
      )}

      {data && data.items.length > 0 && (
        <div className="card table-wrap">
          <table>
            <thead>
              <tr><th>Submitted</th><th>Need</th><th>Urgency</th><th>People</th><th>Status</th><th><span className="sr-only">Open</span></th></tr>
            </thead>
            <tbody>
              {data.items.map((r) => (
                <tr key={r.id}>
                  <td>{formatDateTime(r.createdAtUtc)}</td>
                  <td>{r.category}</td>
                  <td><Badge kind="urgency" value={r.urgency} /></td>
                  <td>{r.peopleAffected}</td>
                  <td><Badge kind="status" value={r.status} /></td>
                  <td><Link to={`/citizen/requests/${r.id}`}>View</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onPage={setPage} />}
    </>
  );
}
