import { useState } from 'react';
import { Redirect } from 'expo-router';
import { Text } from 'react-native';
import { api } from '../api/client';
import { useSession } from '../session/Session';
import { Action, Field, Notice, Screen, styles } from '../components/ui';

export default function Auth() {
  const session = useSession();
  const [signUp, setSignUp] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [handle, setHandle] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function submit() {
    setBusy(true); setError('');
    try {
      if (signUp) await api.register({ email, password, displayName, handle });
      await api.login({ email, password });
      session.setProfile(await api.profile());
      api.report(signUp ? 'signup_completed' : 'login_succeeded', 'auth');
      setPassword('');
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Unable to sign in.'); }
    finally { setBusy(false); }
  }
  if (session.loading) return <Screen><Text>Opening Senzy…</Text></Screen>;
  if (session.profile) return <Redirect href="/activity" />;
  return <Screen><Text style={styles.brand}>senzy</Text><Text style={styles.badge}>Sandbox · no real money</Text><Text style={styles.title}>{signUp ? 'Make yourself at home.' : 'Welcome back.'}</Text><Text style={styles.text}>Your people. Your own space.</Text>
    <Notice>{error || session.error}</Notice>
    {signUp && <><Field label="Display name" value={displayName} onChangeText={setDisplayName} maxLength={80} autoComplete="name" /><Field label="Payment handle" value={handle} onChangeText={setHandle} maxLength={20} autoCapitalize="none" autoCorrect={false} /><Text style={styles.text}>3–20 letters, numbers or underscores. Start with a letter.</Text></>}
    <Field label="Email" value={email} onChangeText={setEmail} keyboardType="email-address" autoCapitalize="none" autoCorrect={false} autoComplete="email" maxLength={254} />
    <Field label="Password" value={password} onChangeText={setPassword} secureTextEntry autoComplete={signUp ? 'new-password' : 'current-password'} maxLength={128} />
    {signUp && <Text style={styles.text}>Use at least 12 characters.</Text>}
    <Action title={busy ? 'Please wait…' : signUp ? 'Create account' : 'Sign in'} disabled={busy || !email || !password || !session.config} onPress={() => void submit()} />
    {session.config?.registrationEnabled && <Action title={signUp ? 'Already a member? Sign in' : 'Create an account'} disabled={busy} onPress={() => { setSignUp(!signUp); setError(''); }} />}
    {session.error && <Action title="Retry connection" onPress={() => void session.reload()} />}
  </Screen>;
}
