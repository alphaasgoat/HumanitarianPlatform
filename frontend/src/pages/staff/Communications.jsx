import { useEffect, useState } from 'react';
import { useApi } from '../../hooks/useApi.js';
import { useAction } from '../../hooks/useAction.js';
import { focusFirstError, useForm } from '../../hooks/useForm.js';
import { crisisApi, notificationApi } from '../../api/endpoints.js';
import { formatDateTime } from '../../lib/dates.js';
import { Badge, EmptyState, ErrorMessage, Notice, Spinner, StatTile, TextField } from '../../components/ui.jsx';

const STATUSES = ['Pending', 'Processing', 'Sent', 'Failed'];

function AnnouncementForm() {
  const crises = useApi((signal) => crisisApi.list(undefined, signal), []);
  const { values, errors, setErrors, change, set } = useForm({ crisisEventId: '', title: '', message: '' });
  const { busy, error, run } = useAction();
  const [done, setDone] = useState('');

  async function onSubmit(e) {
    e.preventDefault();
    setDone('');
    const found = {};
    if (values.title.trim().length < 3 || values.title.trim().length > 150) found.title = 'The title must be 3-150 characters.';
    if (values.message.trim().length < 3 || values.message.trim().length > 1000) found.message = 'The message must be 3-1000 characters.';
    setErrors(found);
    if (focusFirstError(found, ['title', 'message'])) return;

    const result = await run(() => notificationApi.announce({
      crisisEventId: values.crisisEventId || null, title: values.title.trim(), message: values.message.trim()
    }));
    if (result.ok) {
      setDone(`Announcement queued for ${result.value.queued} of ${result.value.recipients} people. It is sent by the background worker within a few seconds.`);
      set({ title: '', message: '' });
    } else if (result.error?.fieldErrors) setErrors(result.error.fieldErrors);
  }

  return (
    <section className="card">
      <h2>Emergency announcement</h2>
      <p className="muted">
        Reaches the people who asked for help in one crisis, or every active citizen. Each person gets it in their inbox, by e-mail,
        and by SMS where a phone number is on file. It is recorded in the audit log.
      </p>
      <Notice kind="success">{done}</Notice>
      <ErrorMessage error={error} />
      <form className="form-grid" onSubmit={onSubmit} noValidate>
        <div className="span-2">
          <TextField id="crisisEventId" label="Who should receive it?" as="select" value={values.crisisEventId} onChange={change}>
            <option value="">Every active citizen</option>
            {(crises.data ?? []).map((c) => <option key={c.id} value={c.id}>People who asked for help: {c.name}</option>)}
          </TextField>
        </div>
        <div className="span-2">
          <TextField id="title" label="Title" value={values.title} onChange={change} error={errors.title} maxLength={150} />
        </div>
        <div className="span-2">
          <TextField id="message" label="Message" as="textarea" rows={4} maxLength={1000} value={values.message} onChange={change}
            error={errors.message} hint={`${values.message.trim().length}/1000 characters. Write short, clear sentences; the same text goes to everyone.`} />
        </div>
        <div className="span-2 actions">
          <button type="submit" className="btn btn-primary" disabled={busy}>{busy ? 'Sending…' : 'Send announcement'}</button>
        </div>
      </form>
    </section>
  );
}

function MessageQueue() {
  const [status, setStatus] = useState('');
  const { data, error, loading, reload } = useApi((signal) => notificationApi.deliveries({ status, take: 100 }, signal), [status]);
  const action = useAction();

  // keep the queue fresh while the page is open
  useEffect(() => {
    const timer = setInterval(reload, 15000);
    return () => clearInterval(timer);
  }, [reload]);

  const list = data ?? [];
  const count = (s) => list.filter((d) => d.status === s).length;

  async function retry(id) {
    const result = await action.run(() => notificationApi.retry(id));
    if (result.ok) reload();
  }

  return (
    <section className="card table-wrap">
      <div className="card-pad head-row">
        <h2>Message queue</h2>
        <div className="actions">
          <label htmlFor="status" className="sr-only">Status</label>
          <select id="status" value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">All messages</option>
            {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <button type="button" className="btn btn-small" onClick={reload}>Refresh</button>
        </div>
      </div>
      <p className="card-pad muted">
        E-mail and SMS are sent in the background. A failed message is tried again automatically (30 s, 1 min, 2 min ... up to 5 attempts);
        after that it waits here for a person to retry it. Provider "log" means no mail server is configured, so the message was written
        to the API log only; "sms-simulated" means the SMS channel is a simulation.
      </p>
      {status === '' && list.length > 0 && (
        <div className="stat-grid card-pad">
          <StatTile label="Waiting" value={count('Pending') + count('Processing')} />
          <StatTile label="Sent" value={count('Sent')} />
          <StatTile label="Failed" value={count('Failed')} hint={count('Failed') ? 'needs attention' : undefined} />
        </div>
      )}
      <ErrorMessage error={error} onRetry={reload} />
      <ErrorMessage error={action.error} />
      {loading && !data && <Spinner />}
      {data && list.length === 0 && <EmptyState title="No messages yet." />}
      {list.length > 0 && (
        <table>
          <thead>
            <tr><th>Created</th><th>Channel</th><th>To</th><th>Status</th><th>Attempts</th><th>Next try</th><th>Provider</th><th>Error</th><th><span className="sr-only">Actions</span></th></tr>
          </thead>
          <tbody>
            {list.map((d) => (
              <tr key={d.id}>
                <td>{formatDateTime(d.createdAtUtc)}</td>
                <td>{d.channel}</td>
                <td>{d.destination}</td>
                <td><Badge kind="delivery" value={d.status} /></td>
                <td>{d.attempts}</td>
                <td>{d.status === 'Pending' ? formatDateTime(d.nextAttemptUtc) : '—'}</td>
                <td>{d.provider ?? '—'}</td>
                <td className="wrap muted">{d.lastError ?? ''}</td>
                <td>
                  {d.status === 'Failed' && (
                    <button type="button" className="btn btn-small btn-primary" disabled={action.busy} onClick={() => retry(d.id)}>Retry</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

export default function Communications() {
  return (
    <>
      <div className="page-head"><h1>Communications</h1></div>
      <AnnouncementForm />
      <MessageQueue />
    </>
  );
}
