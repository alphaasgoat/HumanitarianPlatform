import { useState } from 'react';
import { useAction } from '../hooks/useAction.js';
import { mapsApi } from '../api/endpoints.js';
import { Notice } from './ui.jsx';

/**
 * "Find the coordinates from the address" using the platform's map service. If the map service is unavailable the
 * message says so and the person can type the coordinates instead, so nothing is blocked.
 */
export default function LocateButton({ address, onFound, label = 'Find coordinates from the address' }) {
  const { busy, run } = useAction();
  const [message, setMessage] = useState('');
  const [tone, setTone] = useState('info');

  async function find() {
    const text = (address ?? '').trim();
    if (text.length < 3) {
      setTone('warning');
      setMessage('Type the address first.');
      return;
    }
    const result = await run(() => mapsApi.geocode(text));
    if (!result.ok) {
      setTone('error');
      setMessage(result.error?.detail ?? 'The lookup failed.');
      return;
    }
    const g = result.value;
    if (g.found) {
      onFound({ latitude: Number(g.latitude).toFixed(6), longitude: Number(g.longitude).toFixed(6) });
      setTone('success');
      setMessage(`${g.message}${g.displayName ? ` (${g.displayName})` : ''}`);
    } else {
      setTone('warning');
      setMessage(g.message);
    }
  }

  return (
    <div className="locate">
      <button type="button" className="btn btn-small" onClick={find} disabled={busy}>
        {busy ? 'Looking…' : label}
      </button>
      <Notice kind={tone}>{message}</Notice>
    </div>
  );
}
