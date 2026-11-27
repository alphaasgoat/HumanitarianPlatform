import { humanize } from '../lib/validation.js';

export function Spinner({ label = 'Loading…' }) {
  return (
    <div className="spinner" role="status" aria-live="polite">
      <span className="spinner-dot" aria-hidden="true" /> {label}
    </div>
  );
}

export function ErrorMessage({ error, onRetry }) {
  if (!error) return null;
  const message = typeof error === 'string' ? error : error.detail || error.message;
  return (
    <div className="alert alert-error" role="alert">
      <span>{message}</span>
      {onRetry && (
        <button type="button" className="btn btn-small" onClick={onRetry}>Try again</button>
      )}
    </div>
  );
}

export function Notice({ children, kind = 'info' }) {
  if (!children) return null;
  return <div className={`alert alert-${kind}`} role="status">{children}</div>;
}

export function EmptyState({ title, children }) {
  return (
    <div className="empty">
      <strong>{title}</strong>
      {children && <p>{children}</p>}
    </div>
  );
}

export function Badge({ kind, value }) {
  if (!value) return null;
  return <span className={`badge ${kind}-${value}`}>{humanize(value)}</span>;
}

/** Label + control + hint/error, wired for screen readers (aria-invalid / aria-describedby). */
export function TextField({ id, label, error, hint, as: Tag = 'input', ...props }) {
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;
  return (
    <div className={`field${error ? ' has-error' : ''}`}>
      <label htmlFor={id}>{label}</label>
      <Tag id={id} name={id} aria-invalid={error ? 'true' : 'false'} aria-describedby={describedBy} {...props} />
      {error ? (
        <small className="error-text" id={`${id}-error`}>{error}</small>
      ) : hint ? (
        <small className="hint" id={`${id}-hint`}>{hint}</small>
      ) : null}
    </div>
  );
}

export function Pagination({ page, pageSize, total, onPage }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (pages <= 1) return null;
  return (
    <nav className="pagination" aria-label="Pagination">
      <button type="button" className="btn btn-small" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</button>
      <span>Page {page} of {pages}</span>
      <button type="button" className="btn btn-small" disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</button>
    </nav>
  );
}

/** Horizontal fill bar, e.g. shelter occupancy. Turns amber at 80% and red when full. */
export function Meter({ percent, label }) {
  const p = Math.max(0, Math.min(100, Math.round(Number(percent) || 0)));
  const tone = p >= 100 ? 'full' : p >= 80 ? 'high' : 'ok';
  return (
    <div className="meter" role="img" aria-label={label ?? `${p}% full`}>
      <div className={`meter-fill meter-${tone}`} style={{ width: `${p}%` }} />
    </div>
  );
}

export function StatTile({ label, value, hint, tone }) {
  return (
    <div className={`stat-tile${tone ? ` ${tone}` : ''}`}>
      <div className="stat-value">{value}</div>
      <div className="stat-label">{label}</div>
      {hint && <div className="stat-hint">{hint}</div>}
    </div>
  );
}

export function CheckField({ id, label, hint, ...props }) {
  return (
    <div className="check-field">
      <input id={id} name={id} type="checkbox" {...props} />
      <label htmlFor={id}>{label}{hint && <small className="hint"> {hint}</small>}</label>
    </div>
  );
}

export function Chips({ items }) {
  if (!items || items.length === 0) return null;
  return (
    <ul className="chips">
      {items.map((i) => <li className="chip" key={i}>{humanize(i)}</li>)}
    </ul>
  );
}

/** Shows a short id (first 8 characters) of a GUID so that rows without names stay recognisable. */
export function ShortId({ id }) {
  return <code className="short-id" title={id}>{String(id).slice(0, 8)}</code>;
}
