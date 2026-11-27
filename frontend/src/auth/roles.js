// Role names MUST equal the API's UserRole enum / Roles constants (they come back in the JWT "role" claim).
// IMPORTANT: hiding a link or blocking a route here is only a convenience. The API enforces permissions on every
// call with [Authorize(Roles = ...)], so a user who edits the frontend still gets HTTP 403.
export const ROLES = Object.freeze({
  Citizen: 'Citizen',
  EmergencyCoordinator: 'EmergencyCoordinator',
  CaseManager: 'CaseManager',
  LogisticsManager: 'LogisticsManager',
  ShelterAdministrator: 'ShelterAdministrator',
  VolunteerCoordinator: 'VolunteerCoordinator',
  Donor: 'Donor',
  PartnerOfficer: 'PartnerOfficer',
  SystemAdministrator: 'SystemAdministrator'
});

const R = ROLES;

// Each group copies one of the API's role constants (RequestRoles, ShelterRoles, VolunteerRoles ...).
export const GROUPS = Object.freeze({
  requestStaff: [R.CaseManager, R.EmergencyCoordinator],
  crisisManage: [R.EmergencyCoordinator, R.SystemAdministrator],
  shelterManage: [R.ShelterAdministrator, R.EmergencyCoordinator],
  shelterView: [R.ShelterAdministrator, R.EmergencyCoordinator, R.CaseManager],
  supplies: [R.ShelterAdministrator, R.EmergencyCoordinator, R.LogisticsManager],
  volunteerCoordinate: [R.VolunteerCoordinator, R.EmergencyCoordinator],
  volunteerView: [R.VolunteerCoordinator, R.EmergencyCoordinator, R.CaseManager],
  resourceManage: [R.LogisticsManager, R.EmergencyCoordinator],
  resourceView: [R.LogisticsManager, R.EmergencyCoordinator, R.CaseManager],
  planApply: [R.EmergencyCoordinator],
  admin: [R.SystemAdministrator],
  metrics: [R.CaseManager, R.EmergencyCoordinator, R.SystemAdministrator],
  mapView: [R.ShelterAdministrator, R.EmergencyCoordinator, R.CaseManager],
  communications: [R.EmergencyCoordinator, R.SystemAdministrator],
  allStaff: Object.values(R).filter((r) => r !== R.Citizen)
});

export function homePathFor(role) {
  return role === R.Citizen ? '/citizen' : '/staff';
}

export const NAV_ITEMS = [
  { to: '/citizen', label: 'My requests', roles: [R.Citizen], end: true },
  { to: '/citizen/requests/new', label: 'Ask for help', roles: [R.Citizen] },
  { to: '/citizen/shelters', label: 'Shelters', roles: [R.Citizen] },
  { to: '/citizen/volunteer', label: 'Volunteer', roles: [R.Citizen] },
  { to: '/staff', label: 'Dashboard', roles: GROUPS.allStaff, end: true },
  { to: '/staff/requests', label: 'Requests', roles: GROUPS.requestStaff },
  { to: '/staff/crises', label: 'Crises', roles: GROUPS.crisisManage },
  { to: '/staff/shelters', label: 'Shelters', roles: GROUPS.shelterView },
  { to: '/staff/supply-requests', label: 'Supply requests', roles: GROUPS.supplies },
  { to: '/staff/volunteers', label: 'Volunteers', roles: GROUPS.volunteerView },
  { to: '/staff/tasks', label: 'Tasks', roles: GROUPS.volunteerView },
  { to: '/staff/resources', label: 'Resources', roles: GROUPS.resourceView },
  { to: '/staff/allocations', label: 'Allocations', roles: GROUPS.resourceView },
  { to: '/staff/operations', label: 'Operations', roles: GROUPS.metrics },
  { to: '/staff/map', label: 'Map', roles: GROUPS.mapView },
  { to: '/staff/communications', label: 'Communications', roles: GROUPS.communications },
  { to: '/staff/users', label: 'Users', roles: GROUPS.admin },
  { to: '/staff/audit', label: 'Audit log', roles: GROUPS.admin }
];

export function navFor(role) {
  return NAV_ITEMS.filter((i) => i.roles.includes(role));
}

/** Where to send someone after sign-in: back to the page they wanted if their role may use that area, else home. */
export function postLoginPath(role, from) {
  const home = homePathFor(role);
  const path = from?.pathname;
  if (typeof path === 'string' && (path === home || path.startsWith(`${home}/`))) return `${path}${from.search ?? ''}`;
  return home;
}
