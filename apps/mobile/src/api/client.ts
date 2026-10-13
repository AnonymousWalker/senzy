import { Platform } from 'react-native';
import * as SecureStore from 'expo-secure-store';
import { createApi } from '@senzy/contracts/client';

// Expo web keeps tokens in memory; native refresh credentials use OS-backed storage.
let webToken: string | null = null;
export const api = createApi({
  baseUrl: process.env.EXPO_PUBLIC_API_URL ?? (Platform.OS === 'android' ? 'http://10.0.2.2:4001' : 'http://localhost:4001'),
  mode: 'bearer',
  tokenStore: {
    get: () => Platform.OS === 'web' ? Promise.resolve(webToken) : SecureStore.getItemAsync('senzy.refresh'),
    async set(token) {
      if (Platform.OS === 'web') { webToken = token; return; }
      if (token) await SecureStore.setItemAsync('senzy.refresh', token, { keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY });
      else await SecureStore.deleteItemAsync('senzy.refresh');
    },
  },
});
