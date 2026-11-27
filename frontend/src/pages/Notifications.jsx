import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';
import { GROUPS, ROLES } from '../auth/roles.js';
import { useApi } from '../hooks/useApi.js';
import { useAction } from '../hooks/useAction.js';
import { notificationApi } from '../api/endpoints.js';
import { parseUtc } from '../lib/dates.js';
import { timeAgo } from '../lib/format.js';
import { humanize } from '../lib/validation.js';
import { NOTIFICATIONS_CHANGED } from '../components/NotificationBell.jsx';
import { EmptyState, ErrorMessage, Pagination, Spinner } from '../components/ui.jsx';

const PAGE_SIZE = 15;

/** Where "Open" should lead for this person (citizens and staff see different screens). */
function targetFor(n, role, isRequestStaff) {
  if (n.relatedEntityType === 'AssistanceRequest' && n.relatedEntityId) {
    if (role === ROLES.Citizen) return `/citizen/requests/${n.relatedEntityId}`;
    if (isRequestStaff) return `/staff/requests/${n.relatedEntityId}`;
  }
  if (n.relatedEntityType === 'VolunteerTask' && role === ROLES.Citizen) return '/citizen/volunteer';
  return null;
}

export default function Notifications() {
  const { user, hasRole } = useAuth();
  const isRequestStaff = hasRole(GROUPS.requestStaff);
  const [page, setPage] = useState(1);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const { data, error, loading, reload } = useApi(
    (signal) => notificationApi.mine({ unreadOnly, page, pageSize: PAGE_SIZE }, signal), [unreadOnly, page]
  );
  const action = useAction();

  async function run(fn) {
    const result = await action.run(fn);
    if (result.ok) {
      reload();
      window.dispatchEvent(new Event(NOTIFICATIONS_CHANGED));
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>Notifications</h1>
        <div className="actions">
          <label className="inline-check">
            <input type="checkbox" checked={unreadOnly} onChange={(e) => { setUnreadOnly(e.target.checked); setPage(1); }} /> Unread only
          </label>
          <button type="button" className="btn" disabled={action.busy || !data?.unreadCount} onClick={() => run(() => notificationApi.markAllRead())}>
            Mark all as read
          </button>
        </div>
      </div>

      <ErrorMessage error={error} onRetry={reload} />
      <ErrorMessage error={action.error} />
      {loading && !data && <Spinner />}
      {data && data.items.length === 0 && (
        <EmptyState title={unreadOnly ? 'You have read everything.' : 'No notifications yet.'}>
          Updates about your requests, shelter places and volunteer tasks appear here.
        </EmptyState>
      )}

      {data && data.items.length > 0 && (
        <ul className="notification-list">
          {data.items.map((n) => {
            const target = targetFor(n, user.role, isRequestStaff);
            const unread = !n.readAtUtc;
            return (
              <li key={n.id} className={`card notification${unread ? ' unread' : ''}`}>
                <div className="notification-head">
                  <strong>{unread && <span className="dot" aria-label="Unread" />}{n.title}</strong>
                  <span className="muted" title={parseUtc(n.createdAtUtc)?.toLocaleString()}>{timeAgo(parseUtc(n.createdAtUtc))}</span>
                </div>
                <p>{n.body}</p>
                <div className="actions">
                  <span className="chip">{humanize(n.type)}</span>
                  {target && <Link className="btn btn-small" to={target} onClick={() => unread && run(() => notificationApi.markRead(n.id))}>Open</Link>}
                  {unread && (
                    <button type="button" className="btn btn-small" disabled={action.busy} onClick={() => run(() => notificationApi.markRead(n.id))}>
                      Mark as read
                    </button>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}
      {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onPage={setPage} />}
    </>
  );
}
