// Small display helpers for the dashboards. Pure functions, covered by unit tests.

/** 50 -> "50 min", 125 -> "2 h 5 min", null -> an em dash */
export function formatMinutes(minutes) {
  if (minutes === null || minutes === undefined || Number.isNaN(Number(minutes))) return '—';
  const m = Math.round(Number(minutes));
  if (m < 60) return `${m} min`;
  const h = Math.floor(m / 60);
  const rest = m % 60;
  return rest === 0 ? `${h} h` : `${h} h ${rest} min`;
}

export function formatHours(hours) {
  if (hours === null || hours === undefined || Number.isNaN(Number(hours))) return '—';
  return `${Math.round(Number(hours) * 10) / 10} h`;
}

export function formatPercent(value) {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return '—';
  return `${Math.round(Number(value) * 10) / 10}%`;
}

/** Width (0-100) of a bar for `value` when the longest bar is `max`. */
export function barPercent(value, max) {
  const v = Number(value);
  const m = Number(max);
  if (!(m > 0) || !(v > 0)) return 0;
  return Math.min(100, (v / m) * 100);
}

/**
 * Reads a count from a dictionary the API sent. Dictionary keys keep the server's casing ("Closed"); this also accepts
 * "closed" so a change of JSON settings on the server cannot silently zero a chart.
 */
export function countOf(dictionary, key) {
  if (!dictionary) return 0;
  const lower = key.charAt(0).toLowerCase() + key.slice(1);
  return Number(dictionary[key] ?? dictionary[lower] ?? 0);
}

export const URGENCY_COLORS = { Critical: '#b3261e', High: '#e65100', Medium: '#d29a00', Low: '#2e9e5b' };
export const AVAILABILITY_COLORS = { Available: '#2e9e5b', Limited: '#d29a00', Full: '#b3261e' };

/** Relative time for the notification list: "just now", "5 min ago", "3 h ago", otherwise the date. */
export function timeAgo(date, now = new Date()) {
  if (!date) return '';
  const seconds = Math.round((now.getTime() - date.getTime()) / 1000);
  if (seconds < 45) return 'just now';
  if (seconds < 3600) return `${Math.max(1, Math.round(seconds / 60))} min ago`;
  if (seconds < 86400) return `${Math.round(seconds / 3600)} h ago`;
  return date.toLocaleDateString();
}
