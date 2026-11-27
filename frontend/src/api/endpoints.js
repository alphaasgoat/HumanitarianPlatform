import { api, apiRaw } from './client.js';

// One small object per API controller. Components never build URLs themselves.
const e = encodeURIComponent;
const post = (path, body) => api(path, { method: 'POST', body });

export const authApi = {
  login: (email, password) => api('/api/auth/login', { method: 'POST', body: { email, password }, auth: false }),
  register: (payload) => api('/api/auth/register', { method: 'POST', body: payload, auth: false }),
  me: (signal) => api('/api/auth/me', { signal })
};

export const crisisApi = {
  list: (status, signal) => api('/api/crisis-events', { query: { status }, signal }),
  create: (body) => api('/api/crisis-events', { method: 'POST', body })
};

export const requestApi = {
  /** Resolves to { detail, wasExisting }. HTTP 200 means the same idempotencyKey was already used (no duplicate). */
  async submit(dto) {
    const { status, data } = await apiRaw('/api/requests', { method: 'POST', body: dto });
    return { detail: data, wasExisting: status === 200 };
  },
  mine: ({ page = 1, pageSize = 10 } = {}, signal) => api('/api/requests/mine', { query: { page, pageSize }, signal }),
  get: (id, signal) => api(`/api/requests/${e(id)}`, { signal }),
  list: ({ crisisEventId, status, urgency, category, page = 1, pageSize = 20 } = {}, signal) =>
    api('/api/requests', { query: { crisisEventId, status, urgency, category, page, pageSize }, signal }),
  // staff actions: each returns the updated RequestDetailDto
  analyze: (id) => post(`/api/requests/${e(id)}/analyze`),
  review: (id, body) => post(`/api/requests/${e(id)}/review`, body),
  escalate: (id, note) => post(`/api/requests/${e(id)}/escalate`, { note: note || null }),
  merge: (id, duplicateOfRequestId, note) => post(`/api/requests/${e(id)}/merge`, { duplicateOfRequestId, note: note || null }),
  dismissDuplicate: (id, linkId) => post(`/api/requests/${e(id)}/duplicates/${e(linkId)}/dismiss`),
  close: (id, note) => post(`/api/requests/${e(id)}/close`, { note: note || null })
};

export const shelterApi = {
  listPublic: (crisisEventId, signal) => api('/api/shelters/public', { query: { crisisEventId }, signal }),
  list: (crisisEventId, signal) => api('/api/shelters', { query: { crisisEventId }, signal }),
  get: (id, signal) => api(`/api/shelters/${e(id)}`, { signal }),
  create: (body) => post('/api/shelters', body),
  update: (id, body) => api(`/api/shelters/${e(id)}`, { method: 'PUT', body }),
  setStatus: (id, status) => api(`/api/shelters/${e(id)}/status/${e(status)}`, { method: 'PUT' }),
  recommend: (requestId, signal) => api(`/api/shelters/recommend/${e(requestId)}`, { signal }),
  allocate: (body) => post('/api/shelters/allocate', body),
  allocations: (id, activeOnly, signal) => api(`/api/shelters/${e(id)}/allocations`, { query: { activeOnly }, signal }),
  release: (allocationId, reason) => post(`/api/shelters/allocations/${e(allocationId)}/release`, { reason }),
  requestSupplies: (id, body) => post(`/api/shelters/${e(id)}/supply-requests`, body),
  listSupplyRequests: (status, signal) => api('/api/shelters/supply-requests', { query: { status }, signal }),
  resolveSupplyRequest: (id, note) => post(`/api/shelters/supply-requests/${e(id)}/resolve`, { note: note || null })
};

export const volunteerApi = {
  // the signed-in citizen's own profile
  me: (signal) => api('/api/volunteers/me', { signal }),
  register: (body) => post('/api/volunteers/me', body),
  updateMe: (body) => api('/api/volunteers/me', { method: 'PUT', body }),
  myAvailability: (signal) => api('/api/volunteers/me/availability', { signal }),
  addAvailability: (body) => post('/api/volunteers/me/availability', body),
  // coordinators
  list: ({ skill, verified } = {}, signal) => api('/api/volunteers', { query: { skill, verified }, signal }),
  verify: (id) => post(`/api/volunteers/${e(id)}/verify`),
  setActive: (id, active) => api(`/api/volunteers/${e(id)}/active/${active ? 'true' : 'false'}`, { method: 'PUT' })
};

export const taskApi = {
  list: ({ crisisEventId, status } = {}, signal) => api('/api/volunteer-tasks', { query: { crisisEventId, status }, signal }),
  get: (id, signal) => api(`/api/volunteer-tasks/${e(id)}`, { signal }),
  create: (body) => post('/api/volunteer-tasks', body),
  recommend: (id) => api(`/api/volunteer-tasks/${e(id)}/recommend`),
  assign: (id, volunteerId, note) => post(`/api/volunteer-tasks/${e(id)}/assign`, { volunteerId, note: note || null }),
  assignments: (id, signal) => api(`/api/volunteer-tasks/${e(id)}/assignments`, { signal }),
  cancel: (id, reason) => post(`/api/volunteer-tasks/${e(id)}/cancel`, { reason: reason || null }),
  complete: (assignmentId, hoursWorked, note) =>
    post(`/api/volunteer-tasks/assignments/${e(assignmentId)}/complete`, { hoursWorked, note: note || null }),
  cancelAssignment: (assignmentId, reason) =>
    post(`/api/volunteer-tasks/assignments/${e(assignmentId)}/cancel`, { reason: reason || null }),
  // the volunteer (a citizen account) answering their own assignments
  myAssignments: (signal) => api('/api/volunteer-tasks/my-assignments', { signal }),
  accept: (assignmentId) => post(`/api/volunteer-tasks/assignments/${e(assignmentId)}/accept`),
  decline: (assignmentId, reason) => post(`/api/volunteer-tasks/assignments/${e(assignmentId)}/decline`, { reason: reason || null })
};

export const resourceApi = {
  list: ({ category, lowStockOnly } = {}, signal) =>
    api('/api/resources', { query: { category, lowStockOnly: lowStockOnly ? true : undefined }, signal }),
  create: (body) => post('/api/resources', body),
  restock: (id, quantity, note) => post(`/api/resources/${e(id)}/restock`, { quantity, note: note || null }),
  setActive: (id, active) => api(`/api/resources/${e(id)}/active/${active ? 'true' : 'false'}`, { method: 'PUT' })
};

export const allocationApi = {
  recommend: (requestId) => api(`/api/allocations/recommend/${e(requestId)}`),
  allocate: (body) => post('/api/allocations', body),
  byRequest: (requestId, signal) => api(`/api/allocations/request/${e(requestId)}`, { signal }),
  byCrisis: (crisisEventId, status, signal) => api(`/api/allocations/crisis/${e(crisisEventId)}`, { query: { status }, signal }),
  plan: (crisisEventId, category) => api('/api/allocations/plan', { query: { crisisEventId, category } }),
  applyPlan: (crisisEventId, category) =>
    api(`/api/allocations/plan/apply?crisisEventId=${e(crisisEventId)}&category=${e(category)}`, { method: 'POST' }),
  dispatch: (id) => post(`/api/allocations/${e(id)}/dispatch`),
  deliver: (id) => post(`/api/allocations/${e(id)}/deliver`),
  cancel: (id, reason) => post(`/api/allocations/${e(id)}/cancel`, { reason })
};

export const userApi = {
  list: (signal) => api('/api/users', { signal }),
  createStaff: (body) => post('/api/users/staff', body),
  setActive: (id, active) => api(`/api/users/${e(id)}/active/${active ? 'true' : 'false'}`, { method: 'PUT' })
};

export const auditApi = {
  query: ({ action, entityType, page = 1, pageSize = 50 } = {}, signal) =>
    api('/api/audit', { query: { action, entityType, page, pageSize }, signal })
};

export const notificationApi = {
  mine: ({ unreadOnly, page = 1, pageSize = 20 } = {}, signal) =>
    api('/api/notifications/mine', { query: { unreadOnly: unreadOnly ? true : undefined, page, pageSize }, signal }),
  unreadCount: (signal) => api('/api/notifications/unread-count', { signal }),
  markRead: (id) => post(`/api/notifications/${e(id)}/read`),
  markAllRead: () => post('/api/notifications/read-all'),
  announce: (body) => post('/api/notifications/announcements', body),
  deliveries: ({ status, take = 50 } = {}, signal) => api('/api/notifications/deliveries', { query: { status, take }, signal }),
  retry: (id) => post(`/api/notifications/deliveries/${e(id)}/retry`)
};

export const mapsApi = {
  geocode: (address) => api('/api/maps/geocode', { query: { address } }),
  reverse: (lat, lng) => api('/api/maps/reverse', { query: { lat, lng } }),
  route: (fromLat, fromLng, toLat, toLng) => api('/api/maps/route', { query: { fromLat, fromLng, toLat, toLng } })
};

export const metricsApi = {
  operational: (crisisEventId, signal) => api('/api/metrics/operational', { query: { crisisEventId }, signal })
};
