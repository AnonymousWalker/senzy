import { useEffect, useState } from 'react';
import { Text } from 'react-native';
import { ApiError } from '@senzy/contracts/client';
import { api } from '../../api/client';
import { useSession } from '../../session/Session';
import { Action, Field, Notice, Screen, styles } from '../../components/ui';

export default function ProfileScreen() {
  const session = useSession();
  const [name, setName] = useState(session.profile?.displayName ?? '');
  const [handle, setHandle] = useState(session.profile?.handle ?? '');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => { setName(session.profile?.displayName ?? ''); setHandle(session.profile?.handle ?? ''); }, [session.profile]);
  async function run(action: () => Promise<void>) {
    setBusy(true); setNotice('');
    try { await action(); }
    catch (cause) { setNotice(cause instanceof Error ? cause.message : 'Unable to complete request.'); if (cause instanceof ApiError && cause.status === 401) session.setProfile(null); }
    finally { setBusy(false); }
  }
  return <Screen><Text style={styles.title}>Your profile</Text><Text style={styles.text}>Your handle is how people will find you.</Text><Notice>{notice}</Notice>
    <Field label="Display name" value={name} onChangeText={setName} maxLength={80} autoComplete="name" />
    <Field label="Payment handle" value={handle} onChangeText={setHandle} maxLength={20} autoCapitalize="none" autoCorrect={false} />
    <Text style={styles.text}>3–20 letters, numbers or underscores. Start with a letter.</Text>
    <Action title={busy ? 'Please wait…' : 'Save profile'} disabled={busy} onPress={() => void run(async () => { session.setProfile(await api.updateProfile({ displayName: name, handle })); setNotice('Profile saved.'); api.report('profile_saved', 'profile'); })} />
    <Text style={styles.text}>Your activity is private by default.</Text>
    <Action title="Sign out on all devices" disabled={busy} onPress={() => void run(session.signOut)} />
  </Screen>;
}
