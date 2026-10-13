import { useCallback, useState } from 'react';
import { useFocusEffect } from 'expo-router';
import { Text, View } from 'react-native';
import type { ActivityFeed } from '@senzy/contracts';
import { ApiError } from '@senzy/contracts/client';
import { api } from '../../api/client';
import { useSession } from '../../session/Session';
import { Action, Notice, Screen, styles } from '../../components/ui';

export default function Activity() {
  const { profile, config, setProfile } = useSession();
  const [feed, setFeed] = useState<ActivityFeed | null>(null);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  useFocusEffect(useCallback(() => {
    let active = true;
    setError(''); setFeed(null);
    if (config?.activityEnabled) void api.activity().then(value => { if (active) { setFeed(value); api.report('activity_viewed', 'activity'); } })
      .catch(cause => { if (active) { setError(cause instanceof Error ? cause.message : 'Unable to load activity.'); if (cause instanceof ApiError && cause.status === 401) setProfile(null); } });
    return () => { active = false; };
  }, [config?.activityEnabled, profile?.userId, attempt, setProfile]));
  return <Screen><Text style={styles.brand}>senzy</Text><Text style={styles.badge}>Sandbox · no real money</Text><Text style={styles.title}>Hello, {profile?.displayName}.</Text><Text style={styles.text}>${profile?.handle}</Text>
    <View style={styles.balance}><Text style={{ color: '#d4e5c8' }}>Demo balance</Text><Text style={styles.amount}>$0.00</Text><Text style={{ color: '#d4e5c8' }}>Payments are coming in the next phase.</Text></View>
    <Text style={styles.subtitle}>Activity</Text><Text style={styles.text}>Sample data, private to your session. No money moved.</Text><Notice>{error}</Notice>
    {!config?.activityEnabled ? <Text style={styles.text}>Activity is currently unavailable.</Text> : !feed && !error ? <Text>Loading activity…</Text> : feed?.items.length === 0 ? <Text style={styles.text}>No activity yet.</Text> : feed?.items.map(item => <View style={styles.card} key={item.id}><Text style={styles.subtitle}>{item.title}</Text><Text style={styles.text}>{item.description}</Text><Text style={styles.subtitle}>{item.kind === 'received' ? '+' : '−'}${(item.amountMinor / 100).toFixed(2)}</Text><Text style={styles.text}>{new Date(item.occurredAt).toLocaleDateString()}</Text></View>)}
    {error && <Action title="Retry" onPress={() => setAttempt(value => value + 1)} />}
  </Screen>;
}
