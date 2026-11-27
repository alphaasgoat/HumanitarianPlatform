import { useAction } from '../hooks/useAction.js';
import { allocationApi } from '../api/endpoints.js';
import PromptButton from './PromptButton.jsx';
import { ErrorMessage } from './ui.jsx';

/** Dispatch -> deliver, or cancel (the stock goes back). Only logistics / coordinators see the buttons. */
export default function AllocationActions({ allocation: a, canManage, onDone }) {
  const { busy, error, run } = useAction();
  if (!canManage) return null;

  async function act(fn) {
    const result = await run(fn);
    if (result.ok) onDone();
  }

  return (
    <div className="actions">
      {a.status === 'Approved' && (
        <button type="button" className="btn btn-small btn-primary" disabled={busy} onClick={() => act(() => allocationApi.dispatch(a.id))}>Dispatch</button>
      )}
      {a.status === 'Dispatched' && (
        <button type="button" className="btn btn-small btn-primary" disabled={busy} onClick={() => act(() => allocationApi.deliver(a.id))}>Mark delivered</button>
      )}
      {(a.status === 'Approved' || a.status === 'Dispatched') && (
        <PromptButton label="Cancel" confirmLabel="Cancel allocation" placeholder="Reason (min. 3 characters)" required minLength={3}
          tone="btn-danger" inputProps={{ maxLength: 300 }}
          onSubmit={async (reason) => { await allocationApi.cancel(a.id, reason); onDone(); }} />
      )}
      <ErrorMessage error={error} />
    </div>
  );
}
