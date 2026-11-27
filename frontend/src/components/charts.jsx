import { barPercent } from '../lib/format.js';

/** Horizontal bars with the number always written next to the bar (colour is never the only signal). */
export function BarList({ items, max, empty = 'Nothing to show yet.' }) {
  const visible = items ?? [];
  if (visible.length === 0) return <p className="muted">{empty}</p>;
  const top = max ?? Math.max(...visible.map((i) => Number(i.value) || 0), 0);
  return (
    <ul className="bar-list">
      {visible.map((i) => (
        <li key={i.label}>
          <span className="bar-label">{i.label}</span>
          <span className="bar-track" role="img" aria-label={`${i.label}: ${i.value}`}>
            <span className="bar-fill" style={{ width: `${barPercent(i.value, top)}%`, background: i.color ?? '#1a5fd0' }} />
          </span>
          <span className="bar-value">{i.display ?? i.value}</span>
          {i.note && <span className="bar-note">{i.note}</span>}
        </li>
      ))}
    </ul>
  );
}

/** One bar split into labelled segments (for example approved / modified / rejected / deferred). */
export function StackedBar({ segments }) {
  const total = (segments ?? []).reduce((sum, s) => sum + (Number(s.value) || 0), 0);
  if (total === 0) return <p className="muted">No decisions recorded yet.</p>;
  return (
    <>
      <div className="stacked" role="img" aria-label={segments.map((s) => `${s.label} ${s.value}`).join(', ')}>
        {segments.filter((s) => s.value > 0).map((s) => (
          <span key={s.label} style={{ width: `${(s.value / total) * 100}%`, background: s.color }} title={`${s.label}: ${s.value}`} />
        ))}
      </div>
      <ul className="legend">
        {segments.map((s) => (
          <li key={s.label}><span className="swatch" style={{ background: s.color }} />{s.label} <strong>{s.value}</strong></li>
        ))}
      </ul>
    </>
  );
}
