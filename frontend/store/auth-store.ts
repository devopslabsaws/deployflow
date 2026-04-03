import { create } from "zustand";
import { persist, createJSONStorage } from "zustand/middleware";
import type { User } from "@/types";
import { apiClient, setCachedTokens, clearCachedTokens } from "@/lib/api-client";

interface AuthState {
  user: User | null;
  accessToken: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
  logout: () => void;
  refreshUser: () => Promise<void>;
  setUser: (user: User) => void;
  setToken: (accessToken: string, refreshToken?: string) => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      accessToken: null,
      refreshToken: null,
      isAuthenticated: false,
      isLoading: false,

      login: async (email: string, password: string) => {
        set({ isLoading: true });
        try {
          const response = await apiClient.post<{
            accessToken: string;
            refreshToken: string;
            user: User;
          }>("/auth/login", { email, password });          // Warm the in-memory token cache before any post-login requests fire
          setCachedTokens(response.accessToken, response.refreshToken);          set({
            user: response.user,
            accessToken: response.accessToken,
            refreshToken: response.refreshToken,
            isAuthenticated: true,
            isLoading: false,
          });
        } catch (error) {
          set({ isLoading: false });
          throw error;
        }
      },

      register: async (name: string, email: string, password: string) => {
        set({ isLoading: true });
        try {
          const response = await apiClient.post<{
            accessToken: string;
            refreshToken: string;
            user: User;
          }>("/auth/register", { name, email, password });          setCachedTokens(response.accessToken, response.refreshToken);          set({
            user: response.user,
            accessToken: response.accessToken,
            refreshToken: response.refreshToken,
            isAuthenticated: true,
            isLoading: false,
          });
        } catch (error) {
          set({ isLoading: false });
          throw error;
        }
      },

      logout: () => {
        apiClient.post("/auth/logout", {}).catch(() => {});
        clearCachedTokens();
        set({
          user: null,
          accessToken: null,
          refreshToken: null,
          isAuthenticated: false,
        });
      },

      refreshUser: async () => {
        const { accessToken } = get();
        if (!accessToken) return;
        try {
          const user = await apiClient.get<User>("/auth/me");
          set({ user });
        } catch (err: any) {
          // Only clear auth when the server explicitly says the token is invalid (401).
          // Network errors (err.response undefined) or server outages must NOT log the user out.
          const is401 = err?.response?.status === 401 || (typeof err?.message === "string" && err.message.toLowerCase().includes("401"));
          if (is401) {
            set({ user: null, accessToken: null, refreshToken: null, isAuthenticated: false });
          }
          // Otherwise silently ignore — keep user logged in
        }
      },

      setUser: (user: User) => set({ user }),
      setToken: (accessToken: string, refreshToken?: string) =>
        set({
          accessToken,
          ...(refreshToken !== undefined ? { refreshToken } : {}),
          isAuthenticated: true,
        }),
    }),
    {
      name: "deployflow-auth",
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        user: state.user,
        accessToken: state.accessToken,
        refreshToken: state.refreshToken,
        isAuthenticated: state.isAuthenticated,
      }),
    }
  )
);
