import { useState } from 'react';
import { useAction } from '../hooks/useAction.js';
import { ErrorMessage } from './ui.jsx';

/**
 * A button that opens a one-field mini form before doing something (release a household, cancel a task, resolve a
 * request ...). `onSubmit(value)` should call the API and throw on failure; the API's message is shown inline.
 */
export default function PromptButton({
  label, confirmLabel = 'Confirm', placeholder = '', required = false, minLength = 1,
  type = 'text', inputProps = {}, onSubmit, tone = ''
}) {
  const [open, setOpen] = useState(false);
  const [value, setValue] = useState('');
  const [problem, setProblem] = useState('');
  const { busy, error, run, clearError } = useAction();

  function close() {
    setOpen(false);
    setValue('');
    setProblem('');
    clearError();
  }

  async function submit(e) {
    e.preventDefault();
    const trimmed = value.trim();
    if (required && trimmed.length < minLength) {
      setProblem(minLength > 1 ? `Please enter at least ${minLength} characters.` : 'This field is required.');
      return;
    }
    if (type === 'number' && trimmed !== '' && !Number.isFinite(Number(trimmed))) {
      setProblem('Enter a number.');
      return;
    }
    setProblem('');
    const result = await run(() => onSubmit(type === 'number' ? Number(trimmed) : trimmed));
    if (result.ok) close();
  }

  if (!open) {
    return <button type="button" className={`btn btn-small ${tone}`} onClick={() => setOpen(true)}>{label}</button>;
  }

  return (
    <form className="inline-form" onSubmit={submit} noValidate>
      <input
        type={type} value={value} placeholder={placeholder} aria-label={placeholder || label}
        onChange={(e) => { setValue(e.target.value); setProblem(''); }} autoFocus {...inputProps}
      />
      <button type="submit" className={`btn btn-small ${tone || 'btn-primary'}`} disabled={busy}>
        {busy ? 'Working…' : confirmLabel}
      </button>
      <button type="button" className="btn btn-small" onClick={close} disabled={busy}>Cancel</button>
      {problem && <small className="error-text">{problem}</small>}
      <ErrorMessage error={error} />
    </form>
  );
}
