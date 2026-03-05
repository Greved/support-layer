import { create } from 'zustand';
import type { User } from '@/types';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  user: User | null;
  isAuthenticated: boolean;
  login: (accessToken: string, refreshToken: string, user: User) => void;
  logout: () => void;
  setTokens: (access: string, refresh: string) => void;
}

const LS_ACCESS = 'sl_access';
const LS_REFRESH = 'sl_refresh';
const LS_USER = 'sl_user';

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: localStorage.getItem(LS_ACCESS),
  refreshToken: localStorage.getItem(LS_REFRESH),
  user: (() => {
    try {
      const raw = localStorage.getItem(LS_USER);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  })(),
  isAuthenticated: !!localStorage.getItem(LS_ACCESS),

  login: (accessToken, refreshToken, user) => {
    localStorage.setItem(LS_ACCESS, accessToken);
    localStorage.setItem(LS_REFRESH, refreshToken);
    localStorage.setItem(LS_USER, JSON.stringify(user));
    set({ accessToken, refreshToken, user, isAuthenticated: true });
  },

  logout: () => {
    localStorage.removeItem(LS_ACCESS);
    localStorage.removeItem(LS_REFRESH);
    localStorage.removeItem(LS_USER);
    set({ accessToken: null, refreshToken: null, user: null, isAuthenticated: false });
  },

  setTokens: (access, refresh) => {
    localStorage.setItem(LS_ACCESS, access);
    localStorage.setItem(LS_REFRESH, refresh);
    set({ accessToken: access, refreshToken: refresh, isAuthenticated: true });
  },
}));
