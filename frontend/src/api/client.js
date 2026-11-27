import { ApiError, defaultMessageFor, toApiError } from './errors.js';
import { getStoredToken } from '../auth/session.js';

// Empty base = same-origin "/api/..." (Vite proxies it in development). Set VITE_API_URL for another origin.
const BASE = (import.meta.env.VITE_API_URL ?? '').replace(/\/+$/, '');

let unauthorizedHandler = () => {};
/** AuthProvider registers a callback so an expired/invalid token signs the user out everywhere. */
export function setUnauthorizedHandler(fn) {
  unauthorizedHandler = typeof fn === 'function' ? fn : () => {};
}

function buildUrl(path, query) {
  const url = `${BASE}${path}`;
  if (!query) return url;
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(query)) {
    if (v !== undefined && v !== null && v !== '') params.append(k, String(v));
  }
  const qs = params.toString();
  return qs ? `${url}?${qs}` : url;
}

/**
 * Low-level call. Returns { status, data }. Throws ApiError for every non-2xx response and for network failures.
 * Options: method, body (object -> JSON), query, auth (default true), signal.
 */
export async function apiRaw(path, { method = 'GET', body, query, auth = true, signal } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (auth) {
    const token = getStoredToken();
    if (token) headers.Authorization = `Bearer ${token}`;
  }

  let res;
  try {
    res = await fetch(buildUrl(path, query), {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal
    });
  } catch (err) {
    if (err?.name === 'AbortError') throw err; // caller cancelled: not an error to show
    throw new ApiError(0, 'Network error', defaultMessageFor(0));
  }

  let data = null;
  if (res.status !== 204) {
    const text = await res.text();
    if (text) {
      try { data = JSON.parse(text); } catch { data = null; }
    }
  }

  if (!res.ok) {
    // 401 on a protected call = token expired/invalid -> sign out. (A 401 from /login just means wrong password.)
    if (res.status === 401 && auth) unauthorizedHandler();
    throw toApiError(res.status, data);
  }
  return { status: res.status, data };
}

export async function api(path, options) {
  return (await apiRaw(path, options)).data;
}
