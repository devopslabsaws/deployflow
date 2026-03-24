import axios, { AxiosInstance, AxiosRequestConfig, AxiosError } from "axios";
import { getMockResponse } from "./mock-data";

// Prefer explicit API URL, but default to Next.js same-origin proxy to avoid local port drift.
const BASE_URL = process.env.NEXT_PUBLIC_API_URL || "/proxy";

// Module-level token cache — avoids repeated JSON.parse on every HTTP request
let _cachedAccessToken: string | null = null;
let _cachedRefreshToken: string | null = null;

function readTokensFromStorage(): { accessToken: string | null; refreshToken: string | null } {
  try {
    const raw = localStorage.getItem("deployflow-auth");
    if (!raw) return { accessToken: null, refreshToken: null };
    const parsed = JSON.parse(raw);
    return {
      accessToken: parsed?.state?.accessToken ?? null,
      refreshToken: parsed?.state?.refreshToken ?? null,
    };
  } catch {
    return { accessToken: null, refreshToken: null };
  }
}

/** Call this after login/refresh to update the in-memory cache immediately. */
export function setCachedTokens(accessToken: string, refreshToken?: string) {
  _cachedAccessToken = accessToken;
  if (refreshToken !== undefined) _cachedRefreshToken = refreshToken;
}

/** Call this on logout to clear the in-memory cache. */
export function clearCachedTokens() {
  _cachedAccessToken = null;
  _cachedRefreshToken = null;
}

class ApiClient {
  private instance: AxiosInstance;

  constructor() {
    this.instance = axios.create({
      baseURL: BASE_URL,
      timeout: 10000,
      headers: {
        "Content-Type": "application/json",
      },
    });

    this.instance.interceptors.request.use(
      (config) => {
        // Use in-memory cache; fall back to localStorage on first request
        if (!_cachedAccessToken) {
          const tokens = readTokensFromStorage();
          _cachedAccessToken = tokens.accessToken;
          _cachedRefreshToken = tokens.refreshToken;
        }
        if (_cachedAccessToken) {
          config.headers.Authorization = `Bearer ${_cachedAccessToken}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    this.instance.interceptors.response.use(
      (response) => response,
      async (error: AxiosError) => {
        const originalRequest = error.config as any;

        // Only attempt token refresh for non-auth endpoints (don't refresh on /auth/login etc.)
        const isAuthEndpoint = originalRequest?.url?.includes("/auth/login") ||
          originalRequest?.url?.includes("/auth/register") ||
          originalRequest?.url?.includes("/auth/refresh");

        if (error.response?.status === 401 && !originalRequest._retry && !isAuthEndpoint) {
          originalRequest._retry = true;
          try {
            // Use cached refresh token; fallback to storage if cache is cold
            if (!_cachedRefreshToken) {
              _cachedRefreshToken = readTokensFromStorage().refreshToken;
            }
            if (_cachedRefreshToken) {
              const res = await axios.post(`${BASE_URL}/auth/refresh`, { refreshToken: _cachedRefreshToken });
              const { accessToken, refreshToken: newRefreshToken } = res.data;
              // Update cache
              _cachedAccessToken = accessToken;
              if (newRefreshToken) _cachedRefreshToken = newRefreshToken;
              // Persist to storage
              try {
                const raw = localStorage.getItem("deployflow-auth");
                if (raw) {
                  const parsed = JSON.parse(raw);
                  parsed.state.accessToken = accessToken;
                  if (newRefreshToken) parsed.state.refreshToken = newRefreshToken;
                  localStorage.setItem("deployflow-auth", JSON.stringify(parsed));
                }
              } catch {}
              originalRequest.headers.Authorization = `Bearer ${accessToken}`;
              return this.instance(originalRequest);
            }
          } catch (refreshError: any) {
            // Only clear auth and redirect if the refresh endpoint actually responded
            // with an error (not a network failure). A network failure during refresh
            // should not log the user out — the server may just be temporarily down.
            const isNetworkFailure = !refreshError?.response;
            if (!isNetworkFailure) {
              clearCachedTokens();
              localStorage.removeItem("deployflow-auth");
              window.location.href = "/login";
            }
          }
        }

        const errorData = error.response?.data as any;

        // Network-level failure (backend unreachable, CORS preflight blocked, etc.)
        // error.response is undefined — no HTTP response was ever received.
        if (!error.response) {
          const isTimeout = error.code === "ECONNABORTED";
          const message = isTimeout
            ? "Request timed out. The server is taking too long to respond — check that the backend is running."
            : "Unable to reach the server. The backend may be starting up or offline. Please wait a moment and try again.";
          return Promise.reject(new Error(message));
        }

        const message =
          errorData?.error ||
          errorData?.message ||
          error.message ||
          "An unexpected error occurred";
        return Promise.reject(new Error(message));
      }
    );
  }

  private mockEnabled() {
    return process.env.NEXT_PUBLIC_USE_MOCK_API === "true";
  }

  async get<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
    if (this.mockEnabled()) {
      return getMockResponse(url) as T;
    }
    const response = await this.instance.get<T>(url, config);
    return response.data;
  }

  // POST/PUT/PATCH/DELETE: always throw on error — caller must handle failures
  async post<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    if (this.mockEnabled()) {
      return { id: `mock-${Date.now()}`, ...data, createdAt: new Date().toISOString() } as T;
    }
    const response = await this.instance.post<T>(url, data, config);
    return response.data;
  }

  async put<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    if (this.mockEnabled()) {
      return { ...data, updatedAt: new Date().toISOString() } as T;
    }
    const response = await this.instance.put<T>(url, data, config);
    return response.data;
  }

  async patch<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    if (this.mockEnabled()) {
      return { ...data, updatedAt: new Date().toISOString() } as T;
    }
    const response = await this.instance.patch<T>(url, data, config);
    return response.data;
  }

  async delete<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
    if (this.mockEnabled()) {
      return { success: true } as T;
    }
    const response = await this.instance.delete<T>(url, config);
    return response.data;
  }

  getAxiosInstance(): AxiosInstance {
    return this.instance;
  }
}

export const apiClient = new ApiClient();
