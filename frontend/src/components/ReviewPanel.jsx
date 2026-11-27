import { useForm } from '../hooks/useForm.js';
import { useAction } from '../hooks/useAction.js';
import { requestApi } from '../api/endpoints.js';
import { CATEGORIES, REVIEW_ACTIONS, URGENCIES } from '../lib/validation.js';
import PromptButton from './PromptButton.jsx';
import { ErrorMessage, TextField } from './ui.jsx';

/**
 * The human decision on a request. The AI only advises: Approve / Modify / Reject / Defer is recorded against the
 * staff member's name. Every action returns the updated request, which replaces what is on the screen.
 */
export default function ReviewPanel({ detail, onChange }) {
  const r = detail.request;
  const rec = detail.latestRecommendation;
  const canDecide = rec && rec.status === 'Pending';
  const { values, errors, setErrors, change } = useForm({
    decision: 'Approve',
    finalCategory: rec?.suggestedCategory ?? r.category,
    finalUrgency: rec?.suggestedUrgency ?? r.urgency,
    note: ''
  });
  const decide = useAction();
  const analyze = useAction();

  const needsNote = values.decision === 'Modify' || values.decision === 'Reject';

  async function onSubmit(e) {
    e.preventDefault();
    const found = {};
    if (needsNote && values.note.trim().length < 3) found.note = 'Please explain your decision (at least 3 characters).';
    setErrors(found);
    if (found.note) { document.getElementById('note')?.focus(); return; }
    const result = await decide.run(() => requestApi.review(r.id, {
      decision: values.decision,
      finalCategory: values.decision === 'Modify' ? values.finalCategory : null,
      finalUrgency: values.decision === 'Modify' ? values.finalUrgency : null,
      note: values.note.trim() || null
    }));
    if (result.ok) onChange(result.value);
  }

  async function reanalyze() {
    const result = await analyze.run(() => requestApi.analyze(r.id));
    if (result.ok) onChange(result.value);
  }

  return (
    <section className="card">
      <div className="head-row">
        <h2>Your decision</h2>
        <button type="button" className="btn btn-small" onClick={reanalyze} disabled={analyze.busy}>
          {analyze.busy ? 'Analysing…' : 'Re-run analysis'}
        </button>
      </div>
      <ErrorMessage error={analyze.error} />

      {!rec && <p className="muted">There is no recommendation yet. Run the analysis to get one.</p>}
      {rec && !canDecide && (
        <p className="muted">The latest recommendation was already decided ({rec.humanAction ?? rec.status}). Re-run the analysis if you want a fresh one.</p>
      )}

      {canDecide && (
        <form className="form-grid" onSubmit={onSubmit} noValidate>
          <ErrorMessage error={decide.error} />
          <TextField id="decision" label="Decision" as="select" value={values.decision} onChange={change}>
            {REVIEW_ACTIONS.map((a) => <option key={a} value={a}>{a}</option>)}
          </TextField>
          <div />
          {values.decision === 'Modify' && (
            <>
              <TextField id="finalCategory" label="Final category" as="select" value={values.finalCategory} onChange={change}>
                {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
              </TextField>
              <TextField id="finalUrgency" label="Final urgency" as="select" value={values.finalUrgency} onChange={change}>
                {URGENCIES.map((u) => <option key={u} value={u}>{u}</option>)}
              </TextField>
            </>
          )}
          <div className="span-2">
            <TextField id="note" label={needsNote ? 'Reason (required)' : 'Note (optional)'} as="textarea" rows={2} maxLength={500}
              value={values.note} onChange={change} error={errors.note} />
          </div>
          <div className="span-2 actions">
            <button type="submit" className="btn btn-primary" disabled={decide.busy}>{decide.busy ? 'Saving…' : 'Record decision'}</button>
          </div>
        </form>
      )}

      <div className="actions panel-foot">
        <PromptButton label="Escalate" confirmLabel="Escalate" placeholder="Why? (optional)" inputProps={{ maxLength: 500 }}
          onSubmit={async (note) => onChange(await requestApi.escalate(r.id, note))} />
        <PromptButton label="Close request" confirmLabel="Close" placeholder="Why? (optional)" tone="btn-danger" inputProps={{ maxLength: 500 }}
          onSubmit={async (note) => onChange(await requestApi.close(r.id, note))} />
      </div>
    </section>
  );
}
