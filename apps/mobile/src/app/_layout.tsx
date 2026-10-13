import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { SessionProvider } from '../session/Session';
import { Action, Screen, styles } from '../components/ui';
import { Text } from 'react-native';
import { useEffect } from 'react';
import type { ErrorBoundaryProps } from 'expo-router';
import { api } from '../api/client';

export function ErrorBoundary({ retry }: ErrorBoundaryProps) {
  useEffect(() => { api.report('client_error', 'app'); }, []);
  return <Screen><Text style={styles.title}>Something went wrong</Text><Action title="Try again" onPress={() => void retry()} /></Screen>;
}

export default function RootLayout() {
  return (
    <SessionProvider>
      <Stack screenOptions={{ headerShown: false }} />
      <StatusBar style="dark" />
    </SessionProvider>
  );
}
