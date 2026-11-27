import { Link, useLocation, useParams } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext.jsx';
import { GROUPS } from '../../auth/roles.js';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { requestApi } from '../../api/endpoints.js';
import { formatDateTime } from '../../lib/dates.js';
import { humanize } from '../../lib/validation.js';
import PromptButton from '../../components/PromptButton.jsx';
import ReviewPanel from '../../components/ReviewPanel.jsx';
import ResourceAllocationPanel from '../../components/ResourceAllocationPanel.jsx';
import ShelterPlacement from '../../components/ShelterPlacement.jsx';
import { Badge, ErrorMessage, Notice, Spinner } from '../../components/ui.jsx';

const STATUS_EXPLANATION = {
  Submitted: 'We received your request. It is waiting for a first review.',
  UnderReview: 'A case worker is reviewing your request.',
  Triaged: 'Your request was reviewed and approved. Help is being arranged.',
  Escalated: 'Your request was marked for senior attention.',
  Merged: 'Your request was combined with another request for the same need, so responders do not do the work twice.',
  Closed: 'This request is closed.'
};

function Row({ label, children }) {
  return <div className="row"><dt>{label}</dt><dd>{children}</dd></div>;
}

export default function RequestDetail() {
  const { id } = useParams();
  const location = useLocation();
  const { hasRole } = useAuth();
  const isStaff = hasRole(GROUPS.requestStaff);
  const canPlace = hasRole(GROUPS.shelterManage);
  const canReserve = hasRole(GROUPS.resourceManage);
  const { data, error, loading, reload, setData } = useApi((signal) => requestApi.get(id, signal), [id]);
  const dismiss = useAction();

  const backTo = isStaff ? '/staff/requests' : '/citizen';
  const r = data?.request;
  const rec = data?.latestRecommendation;
  const duplicates = data?.possibleDuplicates ?? [];
  const timeline = data?.timeline ?? [];
  const open = r && r.status !== 'Merged' && r.status !== 'Closed';
  const canFindShelter = isStaff && r && (r.status === 'Triaged' || r.status === 'Escalated');
  const showSupplies = isStaff && r && ['Triaged', 'Escalated', 'Closed'].includes(r.status);

  async function dismissDuplicate(link) {
    const result = await dismiss.run(() => requestApi.dismissDuplicate(r.id, link.id));
    if (result.ok) setData(result.value);
  }

  return (
    <>
      <p><Link to={backTo}>← Back</Link></p>
      {location.state?.submitted && (
        <Notice kind="success">
          {location.state.wasExisting
            ? 'You already sent this request, so we did not create a second copy.'
            : 'Your request was submitted. A case worker will review it.'}
        </Notice>
      )}
      <ErrorMessage error={error} onRetry={reload} />
      {loading && !data && <Spinner />}

      {r && (
        <>
          <div className="page-head">
            <h1>{r.category} request</h1>
            <div className="badges"><Badge kind="status" value={r.status} /><Badge kind="urgency" value={r.urgency} /></div>
          </div>

          {!isStaff && <p className="status-explain">{STATUS_EXPLANATION[r.status] ?? ''}</p>}

          <section className="card">
            <h2>Details</h2>
            <dl className="details">
              <Row label="Description">{r.description}</Row>
              <Row label="People affected">{r.peopleAffected}</Row>
              <Row label={isStaff ? 'Declared urgency' : 'Urgency you chose'}>{humanize(r.declaredUrgency)}</Row>
              {r.urgency !== r.declaredUrgency && <Row label="Urgency after review">{humanize(r.urgency)}</Row>}
              {r.addressText && <Row label="Address">{r.addressText}</Row>}
              {r.latitude != null && r.longitude != null && <Row label="Coordinates">{r.latitude}, {r.longitude}</Row>}
              {isStaff && <Row label="Priority score">{r.priorityScore}</Row>}
              <Row label="Submitted">{formatDateTime(r.createdAtUtc)}</Row>
              {r.closedAtUtc && <Row label="Closed">{formatDateTime(r.closedAtUtc)}</Row>}
            </dl>
          </section>

          {isStaff && rec && (
            <section className="card">
              <h2>AI recommendation <small className="muted">(advice only - a person decides)</small></h2>
              {rec.usedFallback && (
                <div className="alert alert-warning" role="note">Automatic analysis was unavailable, so manual review is required.</div>
              )}
              <dl className="details">
                <Row label="Suggested">{humanize(rec.suggestedCategory)} · {humanize(rec.suggestedUrgency)} (score {rec.priorityScore})</Row>
                <Row label="Confidence">{Math.round(rec.confidence * 100)}%</Row>
                <Row label="Why">{rec.rationale}</Row>
                <Row label="Model">{rec.modelName}</Row>
                <Row label="Status">{humanize(rec.status)}</Row>
                {rec.humanAction && (
                  <Row label="Human decision">
                    {humanize(rec.humanAction)}{rec.decidedByEmail ? ` by ${rec.decidedByEmail}` : ''}
                    {rec.decisionNote ? ` - ${rec.decisionNote}` : ''}
                  </Row>
                )}
              </dl>
            </section>
          )}

          {isStaff && open && <ReviewPanel key={rec?.id ?? 'none'} detail={data} onChange={setData} />}

          {isStaff && duplicates.length > 0 && (
            <section className="card">
              <h2>Possible duplicates</h2>
              <ErrorMessage error={dismiss.error} />
              <ul className="plain">
                {duplicates.map((d) => (
                  <li key={d.id} className="dup">
                    <div>
                      <Link to={`/staff/requests/${d.duplicateOfRequestId}`}>Request {d.duplicateOfRequestId.slice(0, 8)}</Link>
                      {' '}· similarity {Math.round(d.similarityScore * 100)}% · {humanize(d.status)}
                      <div className="muted">{d.reason}</div>
                    </div>
                    {open && d.status === 'Suspected' && (
                      <div className="actions">
                        <PromptButton label="Merge into it" confirmLabel="Merge" placeholder="Note (optional)" inputProps={{ maxLength: 500 }}
                          onSubmit={async (note) => setData(await requestApi.merge(r.id, d.duplicateOfRequestId, note))} />
                        <button type="button" className="btn btn-small" disabled={dismiss.busy} onClick={() => dismissDuplicate(d)}>Not a duplicate</button>
                      </div>
                    )}
                  </li>
                ))}
              </ul>
            </section>
          )}

          {canFindShelter && <ShelterPlacement request={r} canPlace={canPlace} />}
          {showSupplies && <ResourceAllocationPanel request={r} canManage={canReserve} />}

          {timeline.length > 0 && (
            <section className="card">
              <h2>Timeline</h2>
              <ol className="timeline">
                {timeline.map((t, i) => {
                  const showArrow = t.toStatus && humanize(t.toStatus) !== humanize(t.action);
                  return (
                    <li key={`${t.timestampUtc}-${i}`}>
                      <time>{formatDateTime(t.timestampUtc)}</time>
                      <div>
                        <strong>{humanize(t.action)}</strong>
                        {showArrow && <> → {humanize(t.toStatus)}</>}
                        {isStaff && t.actorEmail && <span className="muted"> · {t.actorEmail}</span>}
                        {t.note && <div className="muted">{t.note}</div>}
                      </div>
                    </li>
                  );
                })}
              </ol>
            </section>
          )}
        </>
      )}
    </>
  );
}
