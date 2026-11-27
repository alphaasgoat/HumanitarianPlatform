import { useCallback, useEffect, useState } from 'react';

/**
 * Runs an async loader whenever `deps` change and tracks { data, error, loading }.
 * `loader` receives an AbortSignal; stale responses are cancelled so a slow old request cannot overwrite a new one.
 * `setData` lets an action that returns fresh data (e.g. a review) update the screen without another request.
 */
export function useApi(loader, deps = []) {
  const [state, setState] = useState({ data: null, error: null, loading: true });
  const [tick, setTick] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setState((s) => ({ ...s, loading: true, error: null }));
    loader(controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setState({ data, error: null, loading: false });
      })
      .catch((error) => {
        if (controller.signal.aborted || error?.name === 'AbortError') return;
        setState({ data: null, error, loading: false });
      });
    return () => controller.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);

  const reload = useCallback(() => setTick((t) => t + 1), []);
  const setData = useCallback((data) => setState((s) => ({ ...s, data, error: null })), []);
  return { ...state, reload, setData };
}
