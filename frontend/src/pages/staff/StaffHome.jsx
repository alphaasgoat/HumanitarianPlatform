import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { auditApi, crisisApi, metricsApi, notificationApi, requestApi, resourceApi, shelterApi, taskApi, userApi, volunteerApi } from '../../api/endpoints.js';
import Icon from '../../components/Icon.jsx';
import { humanize } from '../../lib/validation.js';

const sum = (list, pick) => list.reduce((total, item) => total + pick(item), 0);

// "to" = a screen that exists in this build. "stat" is a small live number shown on the card (fails quietly).
const MODULES = [
  {
    title: 'Operations dashboard', icon: 'chart', to: '/staff/operations', roles: GROUPS.metrics,
    text: 'Unresolved and overdue cases, response times, AI override rate, resources and shelters at a glance.',
    stat: async (signal) => {
      const m = await metricsApi.operational(undefined, signal);
      return `${m.requests.unresolved} unresolved · ${m.requests.overdue} overdue`;
    }
  },
  {
    title: 'Requests', icon: 'requests', to: '/staff/requests', roles: GROUPS.requestStaff,
    text: 'Citizen requests by priority, with the AI recommendation and your decision.',
    stat: async (signal) => `${(await requestApi.list({ pageSize: 1 }, signal)).total} requests in total`
  },
  {
    title: 'Crisis events', icon: 'crisis', to: '/staff/crises', roles: GROUPS.crisisManage,
    text: 'Open and review the crisis events that requests belong to.',
    stat: async (signal) => `${(await crisisApi.list('Active', signal)).length} active`
  },
  {
    title: 'Shelters', icon: 'shelter', to: '/staff/shelters', roles: GROUPS.shelterView,
    text: 'Capacity, placement of households and check-out.',
    stat: async (signal) => {
      const open = (await shelterApi.list(undefined, signal)).filter((s) => s.status === 'Open');
      return `${open.length} open · ${sum(open, (s) => s.occupancy)} of ${sum(open, (s) => s.capacity)} places used`;
    }
  },
  {
    title: 'Supply requests', icon: 'supply', to: '/staff/supply-requests', roles: GROUPS.supplies,
    text: 'What shelters are asking logistics for.',
    stat: async (signal) => `${(await shelterApi.listSupplyRequests('Open', signal)).length} open`
  },
  {
    title: 'Volunteers', icon: 'volunteer', to: '/staff/volunteers', roles: GROUPS.volunteerView,
    text: 'Verify volunteers and manage who can be assigned.',
    stat: async (signal) => {
      const all = await volunteerApi.list({}, signal);
      return `${all.filter((v) => v.isVerified).length} verified · ${all.filter((v) => !v.isVerified).length} waiting`;
    }
  },
  {
    title: 'Volunteer tasks', icon: 'tasks', to: '/staff/tasks', roles: GROUPS.volunteerView,
    text: 'Create tasks and assign the best-matching volunteers.',
    stat: async (signal) => `${(await taskApi.list({ status: 'Open' }, signal)).length} open`
  },
  {
    title: 'Resources', icon: 'resources', to: '/staff/resources', roles: GROUPS.resourceView,
    text: 'Inventory of relief supplies, restocking and low-stock warnings.',
    stat: async (signal) => {
      const items = await resourceApi.list({}, signal);
      const low = items.filter((r) => r.isLowStock).length;
      return `${items.length} items${low ? ` · ${low} low on stock` : ''}`;
    }
  },
  {
    title: 'Allocations', icon: 'allocation', to: '/staff/allocations', roles: GROUPS.resourceView,
    text: 'Dispatch and deliver reserved stock; share limited stock fairly with the shortage plan.'
  },
  {
    title: 'Map', icon: 'map', to: '/staff/map', roles: GROUPS.mapView,
    text: 'Open requests and shelters on a map.'
  },
  {
    title: 'Communications', icon: 'bell', to: '/staff/communications', roles: GROUPS.communications,
    text: 'Send emergency announcements and watch the e-mail / SMS queue.',
    stat: async (signal) => {
      const failed = await notificationApi.deliveries({ status: 'Failed', take: 200 }, signal);
      return failed.length === 0 ? 'No failed messages' : `${failed.length} failed message(s)`;
    }
  },
  {
    title: 'User management', icon: 'users', to: '/staff/users', roles: GROUPS.admin,
    text: 'Create staff accounts and deactivate users.',
    stat: async (signal) => `${(await userApi.list(signal)).length} accounts`
  },
  {
    title: 'Audit log', icon: 'audit', to: '/staff/audit', roles: GROUPS.admin,
    text: 'Who did what, and when.',
    stat: async (signal) => `${(await auditApi.query({ pageSize: 1 }, signal)).total} events recorded`
  }
];

const NO_STAT = async () => null;

function ModuleCard({ module: m }) {
  const { data } = useApi(m.stat ?? NO_STAT, []);
  const body = (
    <>
      <span className="module-icon"><Icon name={m.icon} size={22} /></span>
      <div className="module-text">
        <h2>{m.title}</h2>
        <p className="muted">{m.text}</p>
        {m.to && data && <p className="module-stat">{data}</p>}
      </div>
    </>
  );
  return m.to ? (
    <Link className="card module-card" to={m.to}>{body}</Link>
  ) : (
    <div className="card module-card module-soon" aria-disabled="true">
      {body}
      <span className="soon-tag">Coming soon</span>
    </div>
  );
}

export default function StaffHome() {
  const { user } = useAuth();
  const mine = MODULES.filter((m) => m.roles.includes(user.role));

  return (
    <>
      <div className="hero">
        <div>
          <h1>Welcome, {user.fullName}</h1>
          <p className="muted">Signed in as {humanize(user.role)}. Here is what you can work on.</p>
        </div>
      </div>
      {mine.length === 0 && <p className="muted">There are no screens for your role yet.</p>}
      <div className="module-grid">
        {mine.map((m) => <ModuleCard key={m.title} module={m} />)}
      </div>
    </>
  );
}
