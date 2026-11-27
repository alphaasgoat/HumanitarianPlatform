import { parseUtc } from '../lib/dates.js';

// Design decision: the JWT lives in sessionStorage, not localStorage. It disappears when the tab closes and is not
// shared between tabs, which limits how long a stolen token is useful. The trade-off (any XSS can read it) is
// reduced by React escaping output and by the API's short token lifetime (60 minutes). A production system would
// prefer an httpOnly cookie plus refresh tokens - mention this in the viva.
const KEY = 'humanitarian.session';

export function loadSession() {
  try {
    const raw = sessionStorage.getItem(KEY);
    if (!raw) return null;
    const s = JSON.parse(raw);
    const exp = parseUtc(s?.expiresAtUtc);
    if (!s?.token || !s?.user || !exp || exp.getTime() <= Date.now()) {
      sessionStorage.removeItem(KEY);
      return null;
    }
    return s;
  } catch {
    return null;
  }
}

export function saveSession(session) {
  try { sessionStorage.setItem(KEY, JSON.stringify(session)); } catch { /* storage unavailable: session lasts until reload */ }
}

export function clearSession() {
  try { sessionStorage.removeItem(KEY); } catch { /* ignore */ }
}

export function getStoredToken() {
  return loadSession()?.token ?? null;
}
