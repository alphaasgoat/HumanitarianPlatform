import { useCallback, useState } from 'react';

/** Small form-state helper: values, per-field errors and one change handler for inputs, selects and checkboxes. */
export function useForm(initial) {
  const [values, setValues] = useState(initial);
  const [errors, setErrors] = useState({});

  const change = useCallback((e) => {
    const { name, type, checked, value } = e.target;
    setValues((v) => ({ ...v, [name]: type === 'checkbox' ? checked : value }));
    setErrors((er) => ({ ...er, [name]: undefined }));
  }, []);

  const set = useCallback((patch) => setValues((v) => ({ ...v, ...patch })), []);
  return { values, setValues, errors, setErrors, change, set };
}

/** Puts keyboard focus on the first invalid field (in page order) so screen-reader and keyboard users find it. */
export function focusFirstError(errors, order) {
  const first = order.find((f) => errors[f]);
  if (first) document.getElementById(first)?.focus();
  return Boolean(first);
}
