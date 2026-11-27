import test from 'node:test';
import assert from 'node:assert/strict';
import {
  buildShelterPayload, buildTaskPayload, buildVolunteerPayload, toLocalInput, toUtcIso,
  validateAvailability, validateShelter, validateSupplyRequest, validateTask, validateVolunteer
} from './validation.js';

const shelter = {
  name: 'Palatota Hall', address: '12 Lake Road', latitude: '', longitude: '', capacity: '120', crisisEventId: '',
  hasMedicalSupport: true, wheelchairAccessible: false, providesFood: true, providesWater: true, isPublic: true, notes: ''
};

test('a valid shelter has no errors and builds the CreateShelterDto', () => {
  assert.deepEqual(validateShelter(shelter), {});
  const p = buildShelterPayload(shelter);
  assert.equal(p.capacity, 120);
  assert.equal(p.latitude, null);
  assert.equal(p.crisisEventId, null);
  assert.equal(p.notes, null);
  assert.equal(p.hasMedicalSupport, true);
});

test('shelter capacity must be 1-100000', () => {
  for (const bad of ['', '0', '100001', '1.5', 'x']) assert.ok(validateShelter({ ...shelter, capacity: bad }).capacity, bad);
});

test('editing a shelter only sends the UpdateShelterDto fields', () => {
  const p = buildShelterPayload(shelter, { creating: false });
  assert.deepEqual(Object.keys(p).sort(), ['capacity', 'hasMedicalSupport', 'isPublic', 'notes', 'providesFood', 'providesWater', 'wheelchairAccessible']);
  assert.deepEqual(validateShelter({ capacity: '10', notes: '' }, { creating: false }), {});
});

test('volunteer needs a name and at least one skill', () => {
  const v = { fullName: 'Nimali Silva', phone: '', skills: ['Driving'], latitude: '', longitude: '' };
  assert.deepEqual(validateVolunteer(v), {});
  assert.ok(validateVolunteer({ ...v, skills: [] }).skills);
  assert.ok(validateVolunteer({ ...v, fullName: 'N' }).fullName);
  assert.ok(validateVolunteer({ ...v, latitude: '6.9' }).longitude);
  assert.deepEqual(buildVolunteerPayload({ ...v, phone: ' 0771234567 ' }), {
    fullName: 'Nimali Silva', phone: '0771234567', skills: ['Driving'], latitude: null, longitude: null
  });
});

test('availability end must be after start', () => {
  assert.ok(validateAvailability({ start: '2026-11-24T10:00', end: '2026-11-24T09:00' }).end);
  assert.ok(validateAvailability({ start: '', end: '' }).start);
  assert.deepEqual(validateAvailability({ start: '2026-11-24T09:00', end: '2026-11-24T12:00' }), {});
});

test('toUtcIso converts a local datetime-local value to an ISO UTC string', () => {
  const iso = toUtcIso('2026-11-24T09:30');
  assert.match(iso, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:00\.000Z$/);
  assert.equal(new Date(iso).getTime(), new Date('2026-11-24T09:30').getTime());
  assert.equal(toUtcIso(''), null);
  assert.equal(toUtcIso('garbage'), null);
});

test('toLocalInput round-trips through toUtcIso', () => {
  const d = new Date(2026, 10, 24, 9, 5);
  assert.equal(toLocalInput(d), '2026-11-24T09:05');
  assert.equal(new Date(toUtcIso(toLocalInput(d))).getTime(), d.getTime());
});

test('task validation and payload', () => {
  const t = {
    crisisEventId: '11111111-1111-1111-1111-111111111111', title: 'Deliver food packs', description: '',
    requiredSkill: 'Driving', volunteersNeeded: '3', latitude: '', longitude: '',
    start: '2026-11-24T09:00', end: '2026-11-24T13:00'
  };
  assert.deepEqual(validateTask(t), {});
  assert.ok(validateTask({ ...t, title: 'ab' }).title);
  assert.ok(validateTask({ ...t, volunteersNeeded: '0' }).volunteersNeeded);
  assert.ok(validateTask({ ...t, requiredSkill: 'Flying' }).requiredSkill);
  assert.ok(validateTask({ ...t, end: '2026-11-24T08:00' }).end);
  const p = buildTaskPayload(t);
  assert.equal(p.volunteersNeeded, 3);
  assert.equal(p.requestId, null);
  assert.equal(p.description, null);
  assert.match(p.startUtc, /Z$/);
});

test('supply request validation', () => {
  assert.deepEqual(validateSupplyRequest({ category: 'Food', quantity: '25', note: '' }), {});
  assert.ok(validateSupplyRequest({ category: 'Pizza', quantity: '25' }).category);
  assert.ok(validateSupplyRequest({ category: 'Food', quantity: '0' }).quantity);
});

import { buildResourcePayload, validateQuantity, validateResource, validateStaff } from './validation.js';
import { AUDIT_ACTIONS, auditTone } from './audit.js';

const item = { name: 'Dry ration packs', category: 'Food', unit: 'packs', quantityTotal: '100', reorderLevel: '20', locationName: '', latitude: '', longitude: '' };

test('resource validation and payload', () => {
  assert.deepEqual(validateResource(item), {});
  assert.ok(validateResource({ ...item, name: 'x' }).name);
  assert.ok(validateResource({ ...item, category: 'Pizza' }).category);
  assert.ok(validateResource({ ...item, unit: '' }).unit);
  assert.ok(validateResource({ ...item, quantityTotal: '-1' }).quantityTotal);
  assert.ok(validateResource({ ...item, reorderLevel: '1.5' }).reorderLevel);
  assert.equal(validateResource({ ...item, quantityTotal: '0' }).quantityTotal, undefined);
  const p = buildResourcePayload(item);
  assert.equal(p.quantityTotal, 100);
  assert.equal(p.locationName, null);
  assert.equal(p.organisationId, null);
});

test('quantity validation', () => {
  assert.equal(validateQuantity('5'), '');
  assert.ok(validateQuantity('0'));
  assert.ok(validateQuantity('2.5'));
  assert.ok(validateQuantity(''));
});

test('staff account validation', () => {
  const v = { email: 'new.staff@demo.org', fullName: 'New Staff', role: 'CaseManager', password: 'Welcome12345' };
  assert.deepEqual(validateStaff(v), {});
  assert.ok(validateStaff({ ...v, role: 'Citizen' }).role, 'staff accounts cannot be created as Citizen');
  assert.ok(validateStaff({ ...v, password: 'short1' }).password);
  assert.ok(validateStaff({ ...v, email: 'nope' }).email);
});

test('audit tones group actions for scanning', () => {
  assert.equal(auditTone('LoginFailed'), 'warn');
  assert.equal(auditTone('ResourceAllocationCancelled'), 'warn');
  assert.equal(auditTone('RequestSubmitted'), 'ok');
  assert.equal(auditTone('ShelterUpdated'), 'info');
  assert.ok(AUDIT_ACTIONS.includes('ShortagePlanApplied'));
});
