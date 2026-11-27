// Client-side validation mirrors the API's DataAnnotations so people get instant feedback.
// The API still validates everything again: this is for usability, never for security.

export const CATEGORIES = ['Food', 'Medicine', 'Water', 'Shelter', 'Transportation', 'Accessibility', 'Rescue', 'Other'];
export const URGENCIES = ['Low', 'Medium', 'High', 'Critical'];
export const REQUEST_STATUSES = ['Submitted', 'UnderReview', 'Triaged', 'Escalated', 'Merged', 'Closed'];
export const CRISIS_TYPES = ['Flood', 'Landslide', 'Earthquake', 'Storm', 'DiseaseOutbreak', 'Displacement', 'Other'];

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PASSWORD = /^(?=.*[A-Za-z])(?=.*\d).{10,100}$/;
const PHONE = /^[+()\-.\s\d]{5,30}$/;

export const PASSWORD_HINT = '10-100 characters, with at least one letter and one digit.';

const blank = (v) => v === '' || v === null || v === undefined;

export function validateLogin({ email, password }) {
  const e = {};
  if (blank(email) || !EMAIL.test(email.trim())) e.email = 'Enter a valid email address.';
  if (blank(password)) e.password = 'Enter your password.';
  return e;
}

export function validateRegister(v) {
  const e = {};
  if (blank(v.email) || !EMAIL.test(v.email.trim()) || v.email.trim().length > 256) e.email = 'Enter a valid email address.';
  if (blank(v.fullName) || v.fullName.trim().length < 2 || v.fullName.trim().length > 150)
    e.fullName = 'Full name must be 2-150 characters.';
  if (blank(v.password) || !PASSWORD.test(v.password)) e.password = PASSWORD_HINT;
  if (v.confirmPassword !== v.password) e.confirmPassword = 'Passwords do not match.';
  if (!blank(v.phone) && !PHONE.test(v.phone.trim())) e.phone = 'Enter a valid phone number (digits, +, -, spaces).';
  return e;
}

export function validateRequest(v) {
  const e = {};
  if (blank(v.crisisEventId)) e.crisisEventId = 'Choose the crisis this request is about.';
  if (!CATEGORIES.includes(v.category)) e.category = 'Choose a category.';
  if (!URGENCIES.includes(v.declaredUrgency)) e.declaredUrgency = 'Choose how urgent it is.';

  const desc = (v.description ?? '').trim();
  if (desc.length < 10) e.description = 'Please describe the need in at least 10 characters.';
  else if (desc.length > 2000) e.description = 'Description is too long (maximum 2000 characters).';

  const people = Number(v.peopleAffected);
  if (blank(v.peopleAffected) || !Number.isInteger(people) || people < 1 || people > 10000)
    e.peopleAffected = 'Enter a whole number between 1 and 10000.';

  if ((v.addressText ?? '').trim().length > 300) e.addressText = 'Address is too long (maximum 300 characters).';

  const hasLat = !blank(v.latitude);
  const hasLng = !blank(v.longitude);
  if (hasLat !== hasLng) {
    e.latitude = e.longitude = 'Give both latitude and longitude, or leave both empty.';
  } else if (hasLat) {
    const lat = Number(v.latitude);
    const lng = Number(v.longitude);
    if (!Number.isFinite(lat) || lat < -90 || lat > 90) e.latitude = 'Latitude must be between -90 and 90.';
    if (!Number.isFinite(lng) || lng < -180 || lng > 180) e.longitude = 'Longitude must be between -180 and 180.';
  }
  return e;
}

/** Shape expected by POST /api/requests (SubmitRequestDto). Enums are sent as strings. */
export function buildSubmitPayload(v, idempotencyKey) {
  const hasLocation = !blank(v.latitude) && !blank(v.longitude);
  return {
    crisisEventId: v.crisisEventId,
    category: v.category,
    description: v.description.trim(),
    peopleAffected: Number(v.peopleAffected),
    declaredUrgency: v.declaredUrgency,
    latitude: hasLocation ? Number(v.latitude) : null,
    longitude: hasLocation ? Number(v.longitude) : null,
    addressText: blank(v.addressText) || !v.addressText.trim() ? null : v.addressText.trim(),
    idempotencyKey
  };
}

export function newIdempotencyKey() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
}

/** "UnderReview" -> "Under review" */
export function humanize(value) {
  if (!value) return '';
  const spaced = String(value).replace(/([a-z])([A-Z])/g, '$1 $2');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

// ---------------------------------------------------------------------------------------------------------------
// Shelters, volunteers and tasks
// ---------------------------------------------------------------------------------------------------------------
export const SKILLS = ['General', 'FirstAid', 'Medical', 'Driving', 'Logistics', 'Translation', 'Cooking', 'Rescue', 'Counselling', 'Construction'];
export const SUPPLY_CATEGORIES = ['Food', 'Medicine', 'Water', 'Vehicle', 'Equipment', 'Other'];
export const REVIEW_ACTIONS = ['Approve', 'Modify', 'Reject', 'Defer'];

/** "2026-11-24T09:30" (a <input type="datetime-local"> value, local time) -> ISO string in UTC, or null if invalid. */
export function toUtcIso(localValue) {
  if (blank(localValue)) return null;
  const d = new Date(localValue);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

/** Date -> value for <input type="datetime-local"> (local time, minutes precision). */
export function toLocalInput(date = new Date()) {
  const pad = (n) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function coordinateErrors(v) {
  const e = {};
  const hasLat = !blank(v.latitude);
  const hasLng = !blank(v.longitude);
  if (hasLat !== hasLng) {
    e.latitude = e.longitude = 'Give both latitude and longitude, or leave both empty.';
  } else if (hasLat) {
    const lat = Number(v.latitude);
    const lng = Number(v.longitude);
    if (!Number.isFinite(lat) || lat < -90 || lat > 90) e.latitude = 'Latitude must be between -90 and 90.';
    if (!Number.isFinite(lng) || lng < -180 || lng > 180) e.longitude = 'Longitude must be between -180 and 180.';
  }
  return e;
}

const optionalNumber = (v) => (blank(v) ? null : Number(v));
const optionalText = (v) => (blank(v) || !String(v).trim() ? null : String(v).trim());

export function validateShelter(v, { creating = true } = {}) {
  const e = {};
  if (creating) {
    const name = (v.name ?? '').trim();
    if (name.length < 2 || name.length > 150) e.name = 'Name must be 2-150 characters.';
    const address = (v.address ?? '').trim();
    if (!address) e.address = 'Enter the shelter address.';
    else if (address.length > 300) e.address = 'Address is too long (maximum 300 characters).';
    Object.assign(e, coordinateErrors(v));
  }
  const capacity = Number(v.capacity);
  if (blank(v.capacity) || !Number.isInteger(capacity) || capacity < 1 || capacity > 100000)
    e.capacity = 'Capacity must be a whole number between 1 and 100000.';
  if ((v.notes ?? '').length > 500) e.notes = 'Notes are too long (maximum 500 characters).';
  return e;
}

/** CreateShelterDto (creating) or UpdateShelterDto (editing: name, address, location and crisis cannot change). */
export function buildShelterPayload(v, { creating = true } = {}) {
  const common = {
    capacity: Number(v.capacity),
    hasMedicalSupport: Boolean(v.hasMedicalSupport),
    wheelchairAccessible: Boolean(v.wheelchairAccessible),
    providesFood: Boolean(v.providesFood),
    providesWater: Boolean(v.providesWater),
    isPublic: Boolean(v.isPublic),
    notes: optionalText(v.notes)
  };
  if (!creating) return common;
  const hasLocation = !blank(v.latitude) && !blank(v.longitude);
  return {
    name: v.name.trim(),
    address: v.address.trim(),
    latitude: hasLocation ? Number(v.latitude) : null,
    longitude: hasLocation ? Number(v.longitude) : null,
    crisisEventId: blank(v.crisisEventId) ? null : v.crisisEventId,
    ...common
  };
}

export function validateVolunteer(v) {
  const e = {};
  const name = (v.fullName ?? '').trim();
  if (name.length < 2 || name.length > 150) e.fullName = 'Full name must be 2-150 characters.';
  if (!blank(v.phone) && !PHONE.test(v.phone.trim())) e.phone = 'Enter a valid phone number (digits, +, -, spaces).';
  if (!Array.isArray(v.skills) || v.skills.length === 0) e.skills = 'Choose at least one skill.';
  Object.assign(e, coordinateErrors(v));
  return e;
}

export function buildVolunteerPayload(v) {
  const hasLocation = !blank(v.latitude) && !blank(v.longitude);
  return {
    fullName: v.fullName.trim(),
    phone: optionalText(v.phone),
    skills: v.skills,
    latitude: hasLocation ? Number(v.latitude) : null,
    longitude: hasLocation ? Number(v.longitude) : null
  };
}

export function validateAvailability(v) {
  const e = {};
  const start = toUtcIso(v.start);
  const end = toUtcIso(v.end);
  if (!start) e.start = 'Choose when you start.';
  if (!end) e.end = 'Choose when you finish.';
  if (start && end && new Date(end) <= new Date(start)) e.end = 'The end must be after the start.';
  return e;
}

export function validateTask(v) {
  const e = {};
  if (blank(v.crisisEventId)) e.crisisEventId = 'Choose the crisis this task belongs to.';
  const title = (v.title ?? '').trim();
  if (title.length < 3 || title.length > 150) e.title = 'Title must be 3-150 characters.';
  if ((v.description ?? '').length > 1000) e.description = 'Description is too long (maximum 1000 characters).';
  if (!SKILLS.includes(v.requiredSkill)) e.requiredSkill = 'Choose the skill needed.';
  const n = Number(v.volunteersNeeded);
  if (blank(v.volunteersNeeded) || !Number.isInteger(n) || n < 1 || n > 500)
    e.volunteersNeeded = 'Enter a whole number between 1 and 500.';
  Object.assign(e, coordinateErrors(v));
  Object.assign(e, validateAvailability(v));
  return e;
}

export function buildTaskPayload(v) {
  const hasLocation = !blank(v.latitude) && !blank(v.longitude);
  return {
    crisisEventId: v.crisisEventId,
    requestId: blank(v.requestId) ? null : v.requestId,
    title: v.title.trim(),
    description: optionalText(v.description),
    requiredSkill: v.requiredSkill,
    volunteersNeeded: Number(v.volunteersNeeded),
    latitude: hasLocation ? Number(v.latitude) : null,
    longitude: hasLocation ? Number(v.longitude) : null,
    startUtc: toUtcIso(v.start),
    endUtc: toUtcIso(v.end)
  };
}

export function validateSupplyRequest(v) {
  const e = {};
  if (!SUPPLY_CATEGORIES.includes(v.category)) e.category = 'Choose a category.';
  const q = Number(v.quantity);
  if (blank(v.quantity) || !Number.isInteger(q) || q < 1 || q > 100000) e.quantity = 'Enter a whole number between 1 and 100000.';
  if ((v.note ?? '').length > 300) e.note = 'Note is too long (maximum 300 characters).';
  return e;
}

export { optionalNumber, optionalText };

// ---------------------------------------------------------------------------------------------------------------
// Resources, allocations, users, audit
// ---------------------------------------------------------------------------------------------------------------
export const STAFF_ROLES = [
  'EmergencyCoordinator', 'CaseManager', 'LogisticsManager', 'ShelterAdministrator',
  'VolunteerCoordinator', 'Donor', 'PartnerOfficer', 'SystemAdministrator'
];

const wholeNumber = (v, min, max) => !blank(v) && Number.isInteger(Number(v)) && Number(v) >= min && Number(v) <= max;

export function validateResource(v) {
  const e = {};
  const name = (v.name ?? '').trim();
  if (name.length < 2 || name.length > 150) e.name = 'Name must be 2-150 characters.';
  if (!SUPPLY_CATEGORIES.includes(v.category)) e.category = 'Choose a category.';
  const unit = (v.unit ?? '').trim();
  if (!unit) e.unit = 'Enter the unit (for example packs, litres, boxes).';
  else if (unit.length > 30) e.unit = 'Unit is too long (maximum 30 characters).';
  if (!wholeNumber(v.quantityTotal, 0, 1000000)) e.quantityTotal = 'Enter a whole number between 0 and 1,000,000.';
  if (!wholeNumber(v.reorderLevel, 0, 1000000)) e.reorderLevel = 'Enter a whole number between 0 and 1,000,000.';
  if ((v.locationName ?? '').length > 200) e.locationName = 'Location name is too long (maximum 200 characters).';
  Object.assign(e, coordinateErrors(v));
  return e;
}

export function buildResourcePayload(v) {
  const hasLocation = !blank(v.latitude) && !blank(v.longitude);
  return {
    name: v.name.trim(),
    category: v.category,
    unit: v.unit.trim(),
    quantityTotal: Number(v.quantityTotal),
    reorderLevel: Number(v.reorderLevel),
    locationName: optionalText(v.locationName),
    latitude: hasLocation ? Number(v.latitude) : null,
    longitude: hasLocation ? Number(v.longitude) : null,
    organisationId: null
  };
}

export function validateQuantity(value, max = 1000000) {
  return wholeNumber(value, 1, max) ? '' : `Enter a whole number between 1 and ${max.toLocaleString('en-US')}.`;
}

export function validateStaff(v) {
  const e = {};
  if (blank(v.email) || !EMAIL.test(v.email.trim()) || v.email.trim().length > 256) e.email = 'Enter a valid email address.';
  const name = (v.fullName ?? '').trim();
  if (name.length < 2 || name.length > 150) e.fullName = 'Full name must be 2-150 characters.';
  if (!STAFF_ROLES.includes(v.role)) e.role = 'Choose a role.';
  if (blank(v.password) || !PASSWORD.test(v.password)) e.password = PASSWORD_HINT;
  return e;
}
