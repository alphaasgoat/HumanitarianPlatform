import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { notificationApi } from '../api/endpoints.js';
import Icon from './Icon.jsx';

/** Window event other screens fire after they read or mark notifications, so the bell updates immediately. */
export const NOTIFICATIONS_CHANGED = 'humanitarian:notifications-changed';

export default function NotificationBell() {
  const [count, setCount] = useState(0);

  useEffect(() => {
    let active = true;
    const load = () => {
      notificationApi.unreadCount().then((r) => active && setCount(Number(r?.count ?? 0))).catch(() => { /* the bell is optional */ });
    };
    load();
    const timer = setInterval(load, 30000);
    window.addEventListener(NOTIFICATIONS_CHANGED, load);
    return () => {
      active = false;
      clearInterval(timer);
      window.removeEventListener(NOTIFICATIONS_CHANGED, load);
    };
  }, []);

  const label = count > 0 ? `Notifications, ${count} unread` : 'Notifications';
  return (
    <Link to="/notifications" className="bell" aria-label={label} title={label}>
      <Icon name="bell" size={20} />
      {count > 0 && <span className="bell-badge" aria-hidden="true">{count > 99 ? '99+' : count}</span>}
    </Link>
  );
}
