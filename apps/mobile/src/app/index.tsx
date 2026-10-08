import { StyleSheet, Text, View } from 'react-native';

export default function HomeScreen() {
  return (
    <View style={styles.screen}>
      <View style={styles.badge}>
        <Text style={styles.badgeText}>DEVELOPMENT FOUNDATION</Text>
      </View>
      <Text style={styles.wordmark}>senzy</Text>
      <Text style={styles.title}>Payments that feel human.</Text>
      <Text style={styles.body}>
        The mobile shell, API scaffold, and shared contracts are ready for feature work.
      </Text>
      <View style={styles.card}>
        <Text style={styles.cardLabel}>NEXT MILESTONE</Text>
        <Text style={styles.cardTitle}>Identity, authentication, and a read-only activity feed</Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    justifyContent: 'center',
    paddingHorizontal: 28,
    backgroundColor: '#F5F7F2',
  },
  badge: {
    alignSelf: 'flex-start',
    borderRadius: 999,
    backgroundColor: '#D9F6E6',
    paddingHorizontal: 12,
    paddingVertical: 7,
    marginBottom: 30,
  },
  badgeText: {
    color: '#13633A',
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.1,
  },
  wordmark: {
    color: '#171C19',
    fontSize: 22,
    fontWeight: '800',
    letterSpacing: -0.8,
    marginBottom: 16,
  },
  title: {
    color: '#171C19',
    fontSize: 46,
    fontWeight: '800',
    letterSpacing: -2.2,
    lineHeight: 49,
    maxWidth: 330,
  },
  body: {
    color: '#56615B',
    fontSize: 17,
    lineHeight: 25,
    marginTop: 18,
    maxWidth: 340,
  },
  card: {
    borderRadius: 24,
    backgroundColor: '#171C19',
    marginTop: 42,
    padding: 22,
  },
  cardLabel: {
    color: '#A5B0AA',
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.2,
    marginBottom: 10,
  },
  cardTitle: {
    color: '#FFFFFF',
    fontSize: 19,
    fontWeight: '600',
    lineHeight: 27,
  },
});
