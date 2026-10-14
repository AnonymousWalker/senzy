import { profileSchema, configurationSchema, activitySchema, tokenSchema, csrfSchema, problemSchema,
  walletSchema, paymentSchema, paymentListSchema, paymentRequestSchema, paymentRequestListSchema } from './index.ts';
import type { components } from './generated/api.js';
import type { TransferRequest, CreatePaymentRequest } from './index.ts';

export type RegisterInput = components['schemas']['RegisterRequest'];
export type LoginInput = components['schemas']['LoginRequest'];
export type ProfileInput = components['schemas']['UpdateProfileRequest'];
export type TelemetryEvent = 'signup_completed' | 'login_succeeded' | 'profile_saved' | 'activity_viewed' | 'client_error';
export type Screen = 'auth' | 'activity' | 'profile' | 'app';
export function createIdempotencyKey() {
  return `senzy-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`;
}
export class ApiError extends Error {
  status: number;
  code?: string;
  traceId?: string;
  constructor(status: number, message: string, code?: string, traceId?: string) {
    super(message); this.status = status; this.code = code; this.traceId = traceId;
  }
}

export function createApi(options: {
  baseUrl: string;
  mode: 'cookie' | 'bearer';
  tokenStore?: { get: () => Promise<string | null>; set: (token: string | null) => Promise<void> };
}) {
  let accessToken: string | null = null;
  let refreshPending: Promise<boolean> | null = null;
  let telemetryEnabled = false;
  async function raw(path: string, method = 'GET', body?: unknown, headers: Record<string, string> = {}) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 15000);
    try {
      return await fetch(options.baseUrl.replace(/\/$/, '') + path, {
        method, credentials: options.mode === 'cookie' ? 'include' : 'omit',
        headers: { ...(body === undefined ? {} : { 'Content-Type': 'application/json' }), ...headers },
        body: body === undefined ? undefined : JSON.stringify(body), signal: controller.signal,
      });
    } catch { throw new ApiError(0, 'Unable to connect. Check your connection and try again.'); }
    finally { clearTimeout(timer); }
  }
  async function value(response: Response): Promise<unknown> {
    if (!response.ok) {
      const error = problemSchema.safeParse(await response.json().catch(() => ({})));
      throw new ApiError(response.status, error.success ? error.data.detail ?? error.data.title ?? 'Request failed.' : 'Request failed.',
        error.success ? error.data.code : undefined, response.headers.get('X-Correlation-ID') ?? undefined);
    }
    return response.status === 204 || response.headers.get('content-length') === '0' ? undefined : response.json().catch(() => undefined);
  }
  async function saveTokens(response: Response) {
    const tokens = tokenSchema.parse(await value(response));
    await options.tokenStore?.set(tokens.refreshToken);
    accessToken = tokens.accessToken;
  }
  async function refresh() {
    if (refreshPending) return refreshPending;
    refreshPending = (async () => {
      const token = await options.tokenStore?.get();
      if (!token) return false;
      const response = await raw('/v1/auth/refresh', 'POST', { refreshToken: token });
      if (response.status === 401) { accessToken = null; await options.tokenStore?.set(null); return false; }
      await saveTokens(response);
      return true;
    })();
    try { return await refreshPending; } finally { refreshPending = null; }
  }
  async function request(path: string, method = 'GET', body?: unknown, authenticated = true, extraHeaders: Record<string, string> = {}) {
    const headers: Record<string, string> = { ...extraHeaders };
    if (options.mode === 'cookie' && method !== 'GET') {
      headers['X-CSRF-TOKEN'] = csrfSchema.parse(await value(await raw('/v1/auth/csrf'))).token;
    }
    if (options.mode === 'bearer' && authenticated) {
      if (!accessToken) await refresh();
      if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
    }
    let response = await raw(path, method, body, headers);
    if (response.status === 401 && authenticated && options.mode === 'bearer' && await refresh()) {
      response = await raw(path, method, body, { ...headers, Authorization: `Bearer ${accessToken}` });
    }
    return value(response);
  }
  return {
    async configuration() { const config = configurationSchema.parse(await request('/v1/config', 'GET', undefined, false)); telemetryEnabled = config.telemetryEnabled; return config; },
    async register(input: RegisterInput) { return profileSchema.parse(await request('/v1/auth/register', 'POST', input, false)); },
    async login(input: LoginInput) {
      if (options.mode === 'cookie') await request('/v1/auth/login?useCookies=true', 'POST', input, false);
      else await saveTokens(await raw('/v1/auth/login', 'POST', input));
    },
    async logout() {
      await request('/v1/auth/logout', 'POST', {});
      accessToken = null;
      await options.tokenStore?.set(null);
    },
    async forgetSession() { accessToken = null; await options.tokenStore?.set(null); },
    async profile() { return profileSchema.parse(await request('/v1/profile/')); },
    async updateProfile(input: ProfileInput) { return profileSchema.parse(await request('/v1/profile/', 'PUT', input)); },
    async activity() { return activitySchema.parse(await request('/v1/activity')); },
    async wallet() { return walletSchema.parse(await request('/v1/wallet/')); },
    async payments() { return paymentListSchema.parse(await request('/v1/payments/')); },
    async paymentRequests() { return paymentRequestListSchema.parse(await request('/v1/payment-requests/')); },
    async sendPayment(input: TransferRequest, key = createIdempotencyKey()) {
      return paymentSchema.parse(await request('/v1/payments/', 'POST', input, true, { 'Idempotency-Key': key }));
    },
    async createPaymentRequest(input: CreatePaymentRequest, key = createIdempotencyKey()) {
      return paymentRequestSchema.parse(await request('/v1/payment-requests/', 'POST', input, true, { 'Idempotency-Key': key }));
    },
    async acceptPaymentRequest(id: string, key = createIdempotencyKey()) {
      return paymentSchema.parse(await request(`/v1/payment-requests/${encodeURIComponent(id)}/accept`, 'POST', {}, true, { 'Idempotency-Key': key }));
    },
    async reversePayment(id: string, key = createIdempotencyKey()) {
      return paymentSchema.parse(await request(`/v1/payments/${encodeURIComponent(id)}/reverse`, 'POST', {}, true, { 'Idempotency-Key': key }));
    },
    report(event: TelemetryEvent, screen: Screen) {
      if (telemetryEnabled) void request('/v1/telemetry', 'POST', { event, screen }).catch(() => {});
    },
  };
}
