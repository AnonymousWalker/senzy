import { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import type { ClientConfiguration, Profile } from '@senzy/contracts';
import { ApiError } from '@senzy/contracts/client';
import { api } from '../api/client';

type Session = { profile: Profile | null; config: ClientConfiguration | null; loading: boolean; error: string; reload: () => Promise<void>; setProfile: (profile: Profile | null) => void; signOut: () => Promise<void> };
const Context = createContext<Session | null>(null);
export function SessionProvider({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [config, setConfig] = useState<ClientConfiguration | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  async function reload() {
    setLoading(true); setError('');
    try {
      setConfig(await api.configuration());
      try { setProfile(await api.profile()); }
      catch (cause) { if (cause instanceof ApiError && cause.status === 401) setProfile(null); else throw cause; }
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Unable to connect.'); }
    finally { setLoading(false); }
  }
  useEffect(() => { void reload(); }, []);
  async function signOut() { await api.logout(); setProfile(null); }
  return <Context.Provider value={{ profile, config, loading, error, reload, setProfile, signOut }}>{children}</Context.Provider>;
}
export function useSession() { const value = useContext(Context); if (!value) throw new Error('Missing session provider'); return value; }
