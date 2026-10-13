import { Redirect } from 'expo-router';
import { Text } from 'react-native';
import { useSession } from '../session/Session';
import { Action, Notice, Screen } from '../components/ui';

export default function Index() {
  const session = useSession();
  if (session.loading) return <Screen><Text>Opening Senzy…</Text></Screen>;
  if (session.error) return <Screen><Notice>{session.error}</Notice><Action title="Try again" onPress={() => void session.reload()} /></Screen>;
  return <Redirect href={session.profile ? '/activity' : '/auth'} />;
}
