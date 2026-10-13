import { Redirect, Tabs } from 'expo-router';
import { Text } from 'react-native';
import { useSession } from '../../session/Session';
import { Screen } from '../../components/ui';

export default function Layout() {
  const session = useSession();
  if (session.loading) return <Screen><Text>Opening Senzy…</Text></Screen>;
  if (!session.profile) return <Redirect href="/auth" />;
  return <Tabs screenOptions={{ headerShown: false, tabBarActiveTintColor: '#214e32', tabBarLabelStyle: { fontSize: 14 } }}>
    <Tabs.Screen name="activity" options={{ title: 'Activity', tabBarIcon: () => <Text>↗</Text> }} />
    <Tabs.Screen name="profile" options={{ title: 'Profile', tabBarIcon: () => <Text>◎</Text> }} />
  </Tabs>;
}
