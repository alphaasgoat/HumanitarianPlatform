import test from 'node:test';
import assert from 'node:assert/strict';
import {
  buildSubmitPayload, humanize, validateLogin, validateRegister, validateRequest
} from './validation.js';

const good = {
  crisisEventId: '11111111-1111-1111-1111-111111111111',
  category: 'Water',
  declaredUrgency: 'High',
  description: 'We have no clean drinking water since yesterday.',
  peopleAffected: '4',
  addressText: '  12 Lake Road  ',
  latitude: '',
  longitude: ''
};

test('a complete request has no errors', () => {
  assert.deepEqual(validateRequest(good), {});
});

test('description must be 10-2000 characters after trimming', () => {
  assert.ok(validateRequest({ ...good, description: '   short   ' }).description);
  assert.ok(validateRequest({ ...good, description: 'x'.repeat(2001) }).description);
  assert.equal(validateRequest({ ...good, description: 'x'.repeat(2000) }).description, undefined);
});

test('people affected must be a whole number 1-10000', () => {
  for (const bad of ['', '0', '10001', '2.5', 'abc']) {
    assert.ok(validateRequest({ ...good, peopleAffected: bad }).peopleAffected, `should reject "${bad}"`);
  }
  assert.equal(validateRequest({ ...good, peopleAffected: '10000' }).peopleAffected, undefined);
});

test('coordinates: both or neither, and within range', () => {
  assert.ok(validateRequest({ ...good, latitude: '6.9' }).longitude);
  assert.ok(validateRequest({ ...good, latitude: '91', longitude: '80' }).latitude);
  assert.ok(validateRequest({ ...good, latitude: '6.9', longitude: '181' }).longitude);
  assert.deepEqual(validateRequest({ ...good, latitude: '6.9271', longitude: '79.8612' }), {});
});

test('category, urgency and crisis are required', () => {
  const e = validateRequest({ ...good, category: '', declaredUrgency: '', crisisEventId: '' });
  assert.ok(e.category && e.declaredUrgency && e.crisisEventId);
});

test('payload matches SubmitRequestDto', () => {
  const p = buildSubmitPayload({ ...good, latitude: '6.9271', longitude: '79.8612' }, 'key-1');
  assert.deepEqual(p, {
    crisisEventId: good.crisisEventId,
    category: 'Water',
    description: good.description,
    peopleAffected: 4,
    declaredUrgency: 'High',
    latitude: 6.9271,
    longitude: 79.8612,
    addressText: '12 Lake Road',
    idempotencyKey: 'key-1'
  });
});

test('payload sends null (not 0 or "") for missing optional values', () => {
  const p = buildSubmitPayload({ ...good, addressText: '   ' }, 'k');
  assert.equal(p.latitude, null);
  assert.equal(p.longitude, null);
  assert.equal(p.addressText, null);
});

test('login validation', () => {
  assert.ok(validateLogin({ email: 'nope', password: 'x' }).email);
  assert.ok(validateLogin({ email: 'a@b.co', password: '' }).password);
  assert.deepEqual(validateLogin({ email: 'a@b.co', password: 'x' }), {});
});

test('register validation mirrors the API password rule', () => {
  const base = { email: 'a@b.co', fullName: 'Amal Perera', phone: '', password: 'abcdefghij1', confirmPassword: 'abcdefghij1' };
  assert.deepEqual(validateRegister(base), {});
  assert.ok(validateRegister({ ...base, password: 'short1', confirmPassword: 'short1' }).password);
  assert.ok(validateRegister({ ...base, password: 'onlyletterspassword', confirmPassword: 'onlyletterspassword' }).password);
  assert.ok(validateRegister({ ...base, password: '1234567890123', confirmPassword: '1234567890123' }).password);
  assert.ok(validateRegister({ ...base, confirmPassword: 'different11' }).confirmPassword);
  assert.ok(validateRegister({ ...base, phone: 'call me' }).phone);
  assert.equal(validateRegister({ ...base, phone: '+94 77 123 4567' }).phone, undefined);
});

test('humanize splits PascalCase', () => {
  assert.equal(humanize('UnderReview'), 'Under review');
  assert.equal(humanize('EmergencyCoordinator'), 'Emergency coordinator');
});
