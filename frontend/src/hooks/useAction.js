import { useCallback, useState } from 'react';

/**
 * Wraps a button/form action: tracks busy + error so the UI can disable the button and show the API's message.
 * run(fn) resolves to { ok: true, value } or { ok: false, error } - it never throws.
 */
export function useAction() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  const run = useCallback(async (fn) => {
    setBusy(true);
    setError(null);
    try {
      const value = await fn();
      return { ok: true, value };
    } catch (err) {
      setError(err);
      return { ok: false, error: err };
    } finally {
      setBusy(false);
    }
  }, []);

  const clearError = useCallback(() => setError(null), []);
  return { busy, error, run, clearError };
}
