import test from 'node:test';
import assert from 'node:assert/strict';
import { parseUtc, formatDateTime } from './dates.js';

test('adds Z when the API sends a timestamp without a timezone', () => {
  const d = parseUtc('2026-11-20T09:15:00');
  assert.equal(d.toISOString(), '2026-11-20T09:15:00.000Z');
});

test('keeps an explicit Z or offset', () => {
  assert.equal(parseUtc('2026-11-20T09:15:00Z').toISOString(), '2026-11-20T09:15:00.000Z');
  assert.equal(parseUtc('2026-11-20T09:15:00+05:30').toISOString(), '2026-11-20T03:45:00.000Z');
});

test('handles fractional seconds and bad input', () => {
  assert.equal(parseUtc('2026-11-20T09:15:00.1234567').toISOString(), '2026-11-20T09:15:00.123Z');
  assert.equal(parseUtc('not a date'), null);
  assert.equal(parseUtc(null), null);
  assert.equal(formatDateTime(undefined), '—');
});
