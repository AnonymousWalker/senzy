import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import type { TextInputProps } from 'react-native';
import type { ReactNode } from 'react';
import { SafeAreaView } from 'react-native-safe-area-context';

export function Screen({ children }: { children: ReactNode }) {
  return <SafeAreaView style={styles.safe} edges={['top', 'left', 'right']}><ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.screen}>{children}</ScrollView></SafeAreaView>;
}
export function Action({ title, onPress, disabled = false }: { title: string; onPress: () => void; disabled?: boolean }) {
  return <Pressable accessibilityRole="button" accessibilityState={{ disabled }} disabled={disabled} onPress={onPress} style={[styles.button, disabled && { opacity: 0.5 }]}><Text style={styles.buttonText}>{title}</Text></Pressable>;
}
export function Field({ label, ...props }: TextInputProps & { label: string }) {
  return <View style={styles.field}><Text style={styles.label}>{label}</Text><TextInput accessibilityLabel={label} style={styles.input} placeholderTextColor="#697967" {...props} /></View>;
}
export function Notice({ children }: { children: string }) { return children ? <Text accessibilityRole="alert" accessibilityLiveRegion="polite" style={styles.notice}>{children}</Text> : null; }
export const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: '#f7f9f3' }, screen: { padding: 24, gap: 18, paddingBottom: 48, maxWidth: 650, width: '100%', alignSelf: 'center' },
  brand: { fontSize: 28, fontWeight: '800', color: '#1b4731', letterSpacing: -1 },
  title: { fontSize: 34, fontWeight: '700', color: '#193c2b', letterSpacing: -1 },
  subtitle: { fontSize: 22, fontWeight: '600', color: '#193c2b' }, text: { fontSize: 16, lineHeight: 24, color: '#52664e' },
  badge: { color: '#356238', fontSize: 12, backgroundColor: '#e5efdc', padding: 10, borderRadius: 12, alignSelf: 'flex-start' },
  field: { gap: 7 }, label: { color: '#244c30', fontWeight: '600', fontSize: 14 },
  input: { borderWidth: 1, borderColor: '#c8d6c0', borderRadius: 12, padding: 14, fontSize: 16, color: '#193c2b', backgroundColor: '#fff', minHeight: 48 },
  button: { backgroundColor: '#214e32', padding: 16, borderRadius: 12, alignItems: 'center', minHeight: 50 }, buttonText: { fontWeight: '700', color: '#fff', fontSize: 16 },
  notice: { color: '#713e26', backgroundColor: '#fff0df', padding: 12, borderRadius: 10, lineHeight: 22 },
  card: { backgroundColor: '#fff', borderColor: '#dce6d6', borderWidth: 1, padding: 20, borderRadius: 18, gap: 10 },
  balance: { backgroundColor: '#193e2b', padding: 24, borderRadius: 20, gap: 12 }, amount: { fontSize: 38, color: '#fff', fontWeight: '600' },
});
