import test from 'node:test';
import assert from 'node:assert/strict';
import { barPercent, countOf, formatHours, formatMinutes, formatPercent, timeAgo } from './format.js';

test('formatMinutes', () => {
  assert.equal(formatMinutes(50), '50 min');
  assert.equal(formatMinutes(60), '1 h');
  assert.equal(formatMinutes(125), '2 h 5 min');
  assert.equal(formatMinutes(0), '0 min');
  assert.equal(formatMinutes(null), '—');
  assert.equal(formatMinutes(undefined), '—');
});

test('formatHours and formatPercent', () => {
  assert.equal(formatHours(4), '4 h');
  assert.equal(formatHours(2.26), '2.3 h');
  assert.equal(formatHours(null), '—');
  assert.equal(formatPercent(66.66), '66.7%');
  assert.equal(formatPercent(0), '0%');
  assert.equal(formatPercent(null), '—');
});

test('barPercent never leaves 0-100 and handles empty data', () => {
  assert.equal(barPercent(5, 10), 50);
  assert.equal(barPercent(20, 10), 100);
  assert.equal(barPercent(0, 10), 0);
  assert.equal(barPercent(5, 0), 0);
  assert.equal(barPercent(-3, 10), 0);
});

test('countOf accepts the server casing and camelCase', () => {
  assert.equal(countOf({ Closed: 3 }, 'Closed'), 3);
  assert.equal(countOf({ closed: 2 }, 'Closed'), 2);
  assert.equal(countOf({}, 'Closed'), 0);
  assert.equal(countOf(null, 'Closed'), 0);
});

test('timeAgo', () => {
  const now = new Date('2026-11-25T12:00:00Z');
  assert.equal(timeAgo(new Date('2026-11-25T11:59:50Z'), now), 'just now');
  assert.equal(timeAgo(new Date('2026-11-25T11:55:00Z'), now), '5 min ago');
  assert.equal(timeAgo(new Date('2026-11-25T09:00:00Z'), now), '3 h ago');
  assert.equal(timeAgo(null, now), '');
});
