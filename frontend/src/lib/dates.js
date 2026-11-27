// The API sends UTC timestamps. EF Core returns DateTime values with an "unspecified" kind, which .NET
// serialises WITHOUT a trailing "Z" (e.g. "2026-11-20T09:15:00"). JavaScript would then treat that as LOCAL time
// and show the wrong hour, so we add the "Z" ourselves when no timezone designator is present.
export function parseUtc(value) {
  if (!value || typeof value !== 'string') return null;
  const hasZone = /(Z|[+-]\d{2}:?\d{2})$/i.test(value);
  const d = new Date(hasZone ? value : `${value}Z`);
  return Number.isNaN(d.getTime()) ? null : d;
}

export function formatDateTime(value) {
  const d = parseUtc(value);
  return d ? d.toLocaleString() : '—';
}

export function formatDate(value) {
  const d = parseUtc(value);
  return d ? d.toLocaleDateString() : '—';
}
