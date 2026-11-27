import test from 'node:test';
import assert from 'node:assert/strict';
import { normalizeFieldErrors, toApiError } from '../api/errors.js';

test('normalizeFieldErrors lower-cases the first letter and joins messages', () => {
  assert.deepEqual(
    normalizeFieldErrors({ Description: ['too short', 'required'], '$.peopleAffected': ['bad number'] }),
    { description: 'too short required', peopleAffected: 'bad number' }
  );
});

test('toApiError uses ProblemDetails.detail when present', () => {
  const e = toApiError(409, { title: 'Conflict', detail: 'Shelter is full.' });
  assert.equal(e.status, 409);
  assert.equal(e.detail, 'Shelter is full.');
  assert.equal(e.message, 'Shelter is full.');
});

test('toApiError falls back to a friendly message for empty bodies (e.g. 403 from JWT role check)', () => {
  const e = toApiError(403, null);
  assert.match(e.detail, /permission/i);
  assert.deepEqual(e.fieldErrors, {});
});

test('toApiError maps validation errors to fields', () => {
  const e = toApiError(400, { title: 'One or more validation errors occurred.', errors: { Email: ['invalid'] } });
  assert.equal(e.fieldErrors.email, 'invalid');
  assert.match(e.detail, /highlighted/i);
});
