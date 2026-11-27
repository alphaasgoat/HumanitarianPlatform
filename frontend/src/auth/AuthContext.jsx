import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { authApi } from '../api/endpoints.js';
import { setUnauthorizedHandler } from '../api/client.js';
import { parseUtc } from '../lib/dates.js';
import { clearSession, loadSession, saveSession } from './session.js';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [session, setSession] = useState(() => loadSession());
  const [notice, setNotice] = useState('');

  const signOut = useCallback((message = '') => {
    clearSession();
    setSession(null);
    setNotice(message);
  }, []);

  const startSession = useCallback((res) => {
    const next = { token: res.accessToken, expiresAtUtc: res.expiresAtUtc, user: res.user };
    saveSession(next);
    setSession(next);
    setNotice('');
    return res.user;
  }, []);

  const login = useCallback(async (email, password) => startSession(await authApi.login(email.trim(), password)), [startSession]);

  const register = useCallback(async (payload) => startSession(await authApi.register(payload)), [startSession]);

  // Any protected API call that comes back 401 signs the user out.
  useEffect(() => {
    setUnauthorizedHandler(() => signOut('Your session expired. Please sign in again.'));
    return () => setUnauthorizedHandler(null);
  }, [signOut]);

  // Sign out automatically when the token's lifetime ends, even if the person is idle.
  const expiresAtUtc = session?.expiresAtUtc;
  useEffect(() => {
    if (!expiresAtUtc) return undefined;
    const exp = parseUtc(expiresAtUtc);
    if (!exp) return undefined;
    const ms = Math.min(Math.max(exp.getTime() - Date.now(), 0), 2_000_000_000);
    const id = setTimeout(() => signOut('Your session expired. Please sign in again.'), ms);
    return () => clearTimeout(id);
  }, [expiresAtUtc, signOut]);

  const value = useMemo(() => {
    const user = session?.user ?? null;
    return {
      user,
      isAuthenticated: Boolean(session),
      hasRole: (...roles) => Boolean(user) && roles.flat().includes(user.role),
      login,
      register,
      logout: () => signOut(''),
      notice,
      clearNotice: () => setNotice('')
    };
  }, [session, notice, login, register, signOut]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>.');
  return ctx;
}
