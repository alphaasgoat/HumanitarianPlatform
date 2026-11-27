import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { crisisApi, metricsApi } from '../../api/endpoints.js';
import { AVAILABILITY_COLORS, URGENCY_COLORS, countOf, formatHours, formatMinutes, formatPercent } from '../../lib/format.js';
import { CATEGORIES, REQUEST_STATUSES, URGENCIES, humanize } from '../../lib/validation.js';
import { BarList, StackedBar } from '../../components/charts.jsx';
import { EmptyState, ErrorMessage, Meter, Spinner, StatTile } from '../../components/ui.jsx';

const STATUS_COLORS = {
  Submitted: '#6b8fd6', UnderReview: '#1a5fd0', Triaged: '#2e9e5b', Escalated: '#b3261e', Merged: '#8a93a0', Closed: '#5d6673'
};

function Card({ title, children, hint }) {
  return (
    <section className="card">
      <h2>{title}</h2>
      {hint && <p className="muted">{hint}</p>}
      {children}
    </section>
  );
}

export default function OperationsDashboard() {
  const { hasRole } = useAuth();
  const canOpenCommunications = hasRole(GROUPS.communications);
  const [crisisId, setCrisisId] = useState('');
  const crises = useApi((signal) => crisisApi.list(undefined, signal), []);
  const { data: m, error, loading, reload } = useApi((signal) => metricsApi.operational(crisisId, signal), [crisisId]);

  // keep the numbers fresh while the page is open
  useEffect(() => {
    const timer = setInterval(reload, 60000);
    return () => clearInterval(timer);
  }, [reload]);

  const updated = m ? new Date(m.generatedAtUtc.endsWith('Z') ? m.generatedAtUtc : `${m.generatedAtUtc}Z`).toLocaleTimeString() : '';

  return (
    <>
      <div className="page-head">
        <h1>Operations dashboard</h1>
        <div className="actions">
          <label htmlFor="crisis" className="sr-only">Crisis</label>
          <select id="crisis" value={crisisId} onChange={(e) => setCrisisId(e.target.value)}>
            <option value="">All crises</option>
            {(crises.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          <button type="button" className="btn" onClick={reload}>Refresh</button>
        </div>
      </div>
      {m && <p className="muted">Updated {updated}. Refreshes every minute.</p>}

      <ErrorMessage error={error} onRetry={reload} />
      {loading && !m && <Spinner />}
      {m && m.requests.total === 0 && <EmptyState title="No requests yet for this selection." />}

      {m && (
        <>
          <div className="stat-grid">
            <StatTile label="Open requests" value={m.requests.open} hint={`${m.requests.total} in total`} />
            <StatTile label="Unresolved" value={m.requests.unresolved} tone={m.requests.unresolved > 0 ? 'warn' : ''}
              hint="open, and nobody is helping yet" />
            <StatTile label="Overdue" value={m.requests.overdue} tone={m.requests.overdue > 0 ? 'alert' : ''}
              hint="past the target: Critical 2 h, High 6 h, Medium 24 h, Low 72 h" />
            <StatTile label="Escalated" value={m.requests.escalated} />
          </div>
          <div className="stat-grid">
            <StatTile label="Average time to first decision" value={formatMinutes(m.responseTimes.averageFirstDecisionMinutes)}
              hint={`${m.responseTimes.requestsWithDecision} requests decided`} />
            <StatTile label="Average time to resolve" value={formatHours(m.responseTimes.averageResolutionHours)} hint="closed requests" />
            <StatTile label="AI override rate" value={formatPercent(m.ai.overrideRatePercent)} hint={`${m.ai.decided} decisions on AI advice`} />
            <StatTile label="Shelter occupancy" value={formatPercent(m.shelters.occupancyPercent)} hint={`${m.shelters.occupancy} of ${m.shelters.capacity} places`} />
          </div>

          <div className="two-col">
            <Card title="Requests by status">
              <BarList items={REQUEST_STATUSES.map((s) => ({ label: humanize(s), value: countOf(m.requests.byStatus, s), color: STATUS_COLORS[s] }))} />
            </Card>
            <Card title="Open requests by urgency" hint="Staff-assessed urgency of requests that are not closed or merged.">
              <BarList items={[...URGENCIES].reverse().map((u) => ({ label: u, value: countOf(m.requests.openByUrgency, u), color: URGENCY_COLORS[u] }))} />
            </Card>
          </div>

          <div className="two-col">
            <Card title="Open requests by need">
              <BarList empty="No open requests."
                items={CATEGORIES.map((c) => ({ label: c, value: countOf(m.requests.openByCategory, c) })).filter((i) => i.value > 0)} />
            </Card>
            <Card title="Resource utilisation" hint="How much of each stock category is reserved or sent out. Stock is shared by every crisis.">
              <BarList max={100} empty="No stock items yet."
                items={m.resources.map((r) => ({
                  label: r.category, value: r.utilisationPercent, display: `${r.quantityAvailable}/${r.quantityTotal} left`,
                  note: r.lowStockItems > 0 ? `${r.lowStockItems} item(s) low on stock` : undefined,
                  color: r.utilisationPercent >= 80 ? '#b3261e' : r.utilisationPercent >= 50 ? '#d29a00' : '#2e9e5b'
                }))} />
            </Card>
          </div>

          <div className="two-col">
            <Card title="AI recommendations and human decisions" hint="The AI only advises. Modified and rejected recommendations are overrides.">
              <StackedBar segments={[
                { label: 'Approved', value: m.ai.approved, color: '#2e9e5b' },
                { label: 'Modified', value: m.ai.modified, color: '#d29a00' },
                { label: 'Rejected', value: m.ai.rejected, color: '#b3261e' },
                { label: 'Deferred', value: m.ai.deferred, color: '#8a93a0' }
              ]} />
              <dl className="details compact">
                <div className="row"><dt>Recommendations made</dt><dd>{m.ai.recommendations}</dd></div>
                <div className="row"><dt>Average confidence</dt><dd>{Math.round(m.ai.averageConfidence * 100)}%</dd></div>
                <div className="row"><dt>Analysis service unavailable</dt><dd>{formatPercent(m.ai.fallbackRatePercent)} of recommendations (rule-based fallback)</dd></div>
              </dl>
            </Card>
            <Card title="Shelters">
              <Meter percent={m.shelters.occupancyPercent} label={`${m.shelters.occupancyPercent}% of open shelter places used`} />
              <dl className="details compact">
                <div className="row"><dt>Open shelters</dt><dd>{m.shelters.open} of {m.shelters.shelters}</dd></div>
                <div className="row"><dt>Places used</dt><dd>{m.shelters.occupancy} of {m.shelters.capacity}</dd></div>
                <div className="row"><dt>Full shelters</dt><dd>{m.shelters.full}</dd></div>
              </dl>
            </Card>
          </div>

          <div className="two-col">
            <Card title="Volunteers and tasks">
              <dl className="details compact">
                <div className="row"><dt>Volunteers</dt><dd>{m.volunteers.total} ({m.volunteers.verified} verified, {m.volunteers.active} active)</dd></div>
                <div className="row"><dt>Tasks</dt><dd>{m.volunteers.openTasks} open, {m.volunteers.filledTasks} filled, {m.volunteers.completedTasks} completed</dd></div>
                <div className="row"><dt>Assignments accepted</dt><dd>{formatPercent(m.volunteers.acceptanceRatePercent)}</dd></div>
                <div className="row"><dt>Hours given</dt><dd>{m.volunteers.hoursGiven} h</dd></div>
              </dl>
            </Card>
            <Card title="Service coverage" hint={`Open requests with a location that are within ${m.coverage.radiusKm} km of an open shelter.`}>
              <Meter percent={m.coverage.coveragePercent} label={`${m.coverage.coveragePercent}% covered`} />
              <dl className="details compact">
                <div className="row"><dt>Covered</dt><dd>{m.coverage.withinRadius} of {m.coverage.openRequestsWithLocation} ({formatPercent(m.coverage.coveragePercent)})</dd></div>
                <div className="row"><dt>Average distance to nearest shelter</dt><dd>{m.coverage.averageDistanceKm == null ? '—' : `${m.coverage.averageDistanceKm} km`}</dd></div>
              </dl>
            </Card>
          </div>

          <div className="two-col">
            <Card title="Message delivery" hint="E-mail and SMS queue health.">
              <dl className="details compact">
                <div className="row"><dt>Waiting</dt><dd>{m.deliveries.pending}</dd></div>
                <div className="row"><dt>Sent</dt><dd>{m.deliveries.sent}</dd></div>
                <div className="row"><dt>Failed</dt><dd>{m.deliveries.failed}</dd></div>
              </dl>
              {m.deliveries.failed > 0 && canOpenCommunications && <Link className="btn btn-small" to="/staff/communications">Review failed messages</Link>}
            </Card>
            <Card title="Staff decisions" hint="Decisions on AI recommendations per person.">
              {m.workload.length === 0 ? <p className="muted">No decisions yet.</p> : (
                <table>
                  <thead><tr><th>Person</th><th>Decisions</th><th>Overrides</th></tr></thead>
                  <tbody>{m.workload.map((w) => <tr key={w.email}><td>{w.email}</td><td>{w.decisions}</td><td>{w.overrides}</td></tr>)}</tbody>
                </table>
              )}
            </Card>
          </div>
        </>
      )}
    </>
  );
}
