import axios from 'axios';
import type { AxiosInstance, InternalAxiosRequestConfig, AxiosResponse } from 'axios';
import { useAuthStore } from '@/stores/authStore';
import type {
  TokenPair,
  User,
  Document,
  BotConfig,
  ApiKey,
  TeamMember,
  DashboardUsage,
  NotificationPref,
  OnboardingState,
  PortalEvalSummary,
  PortalEvalRunItem,
  PortalEvalRunDetail,
  PortalEvalRunAcceptedResponse,
  PagedResponse,
} from '@/types';

const client: AxiosInstance = axios.create({
  baseURL: '/',
});

// Request interceptor: attach access token
client.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = localStorage.getItem('sl_access');
  if (token && config.headers) {
    config.headers['Authorization'] = `Bearer ${token}`;
  }
  return config;
});

// Track whether a refresh is in progress to avoid loops
let isRefreshing = false;
let refreshSubscribers: Array<(token: string) => void> = [];

function subscribeTokenRefresh(cb: (token: string) => void) {
  refreshSubscribers.push(cb);
}

function onRefreshed(token: string) {
  refreshSubscribers.forEach((cb) => cb(token));
  refreshSubscribers = [];
}

// Response interceptor: handle 401 → refresh
client.interceptors.response.use(
  (response: AxiosResponse) => response,
  async (error) => {
    const originalRequest = error.config;

    // Don't intercept 401s from auth endpoints (login/refresh) — let the caller handle them
    const isAuthEndpoint = originalRequest.url?.includes('/portal/auth/');
    if (error.response?.status === 401 && !originalRequest._retry && !isAuthEndpoint) {
      const refreshToken = localStorage.getItem('sl_refresh');

      if (!refreshToken) {
        useAuthStore.getState().logout();
        window.location.href = '/login';
        return Promise.reject(error);
      }

      if (isRefreshing) {
        return new Promise((resolve) => {
          subscribeTokenRefresh((token) => {
            originalRequest.headers['Authorization'] = `Bearer ${token}`;
            resolve(client(originalRequest));
          });
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      try {
        const response = await axios.post<{ accessToken: string; refreshToken: string }>(
          '/portal/auth/refresh',
          { refreshToken }
        );
        const { accessToken, refreshToken: newRefresh } = response.data;
        useAuthStore.getState().setTokens(accessToken, newRefresh);
        onRefreshed(accessToken);
        isRefreshing = false;
        originalRequest.headers['Authorization'] = `Bearer ${accessToken}`;
        return client(originalRequest);
      } catch {
        isRefreshing = false;
        useAuthStore.getState().logout();
        window.location.href = '/login';
        return Promise.reject(error);
      }
    }

    return Promise.reject(error);
  }
);

// Auth endpoints
export const auth = {
  login: (email: string, password: string) =>
    client.post<{ accessToken: string; refreshToken: string; user: User; mfaRequired?: boolean; tempToken?: string }>(
      '/portal/auth/login',
      { email, password }
    ),

  refresh: (token: string) =>
    client.post<TokenPair>('/portal/auth/refresh', { refreshToken: token }),

  logout: (token: string) =>
    client.post('/portal/auth/logout', { refreshToken: token }),

  requestPasswordReset: (email: string) =>
    client.post('/portal/auth/password-reset/request', { email }),

  confirmPasswordReset: (token: string, newPassword: string) =>
    client.post('/portal/auth/password-reset/confirm', { token, newPassword }),

  mfaEnroll: () =>
    client.post<{ secret: string; totpUri: string; backupCodes: string[] }>('/portal/auth/mfa/enroll'),

  mfaVerify: (code: string) =>
    client.post('/portal/auth/mfa/verify', { code }),

  mfaLogin: (tempToken: string, code: string) =>
    client.post<{ accessToken: string; refreshToken: string; user: User }>(
      '/portal/auth/mfa/login',
      { tempToken, code }
    ),
};

// Documents endpoints
export const documents = {
  list: () => client.get<Document[]>('/portal/documents'),

  upload: (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return client.post<Document>('/portal/documents', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },

  delete: (id: string) => client.delete(`/portal/documents/${id}`),

  getStatus: (id: string) => client.get<{ status: Document['status'] }>(`/portal/documents/${id}/status`),
};

// Config endpoints
export const config = {
  get: () => client.get<BotConfig>('/portal/config'),
  update: (data: Partial<BotConfig>) => client.put<BotConfig>('/portal/config', data),
};

// API Keys endpoints
export const apiKeys = {
  list: () => client.get<ApiKey[]>('/portal/api-keys'),
  create: (name: string) => client.post<ApiKey & { key: string }>('/portal/api-keys', { name }),
  revoke: (id: string) => client.delete(`/portal/api-keys/${id}`),
};

// Team endpoints
export const team = {
  list: () => client.get<TeamMember[]>('/portal/users'),
  invite: (email: string, role: string) => client.post('/portal/users/invite', { email, role }),
  remove: (id: string) => client.delete(`/portal/users/${id}`),
};

// Settings endpoints
export const settings = {
  getNotifications: () => client.get<NotificationPref[]>('/portal/settings/notifications'),
  updateNotifications: (prefs: NotificationPref[]) =>
    client.put('/portal/settings/notifications', prefs),
};

// Onboarding endpoints
export const onboarding = {
  getState: () => client.get<OnboardingState>('/portal/onboarding'),
  completeStep: (step: number) => client.post(`/portal/onboarding/complete/${step}`),
};

// Dashboard endpoints
export const dashboard = {
  getUsage: () => client.get<DashboardUsage>('/portal/dashboard/usage'),
};

// Eval endpoints
export const evals = {
  summary: () => client.get<PortalEvalSummary>('/portal/evals/summary'),
  runs: (page = 1, pageSize = 20) =>
    client.get<PagedResponse<PortalEvalRunItem>>('/portal/evals/runs', {
      params: { page, pageSize },
    }),
  runDetail: (runId: string) => client.get<PortalEvalRunDetail>(`/portal/evals/runs/${runId}`),
  triggerRun: (runType = 'manual', triggeredBy = 'portal-ui') =>
    client.post<PortalEvalRunAcceptedResponse>('/portal/evals/run', {
      runType,
      triggeredBy,
    }),
};

export default client;
