import type { AuthResponse, DemoLoginPayload, LoginPayload, RegisterPayload } from "../types/auth";
import { apiClient } from "./apiClient";
import { tokenStore } from "./tokenStore";

async function establishSession(request: Promise<AuthResponse>) {
  const session = await request;

  if (!session ||
      typeof session.accessToken !== "string" ||
      !session.accessToken ||
      !session.user) {
    throw new Error("The authentication server returned an invalid response. Please try again.");
  }

  tokenStore.set(session.accessToken, session.accessTokenExpiresAt);
  return session;
}

export const authService = {
  currentUser: () => apiClient.get<AuthResponse["user"]>("/auth/me"),
  login: (payload: LoginPayload) => establishSession(apiClient.post<AuthResponse>("/auth/login", payload, { retryOnUnauthorized: false })),
  demoLogin: (payload: DemoLoginPayload) => establishSession(apiClient.post<AuthResponse>("/auth/demo-login", payload, { retryOnUnauthorized: false })),
  register: async (payload: RegisterPayload) => {
    // Registration is complete once the server accepts the account and sends
    // the verification email. Do not require a login-shaped response here:
    // deployments may intentionally return an empty 201/202 response.
    await apiClient.post<unknown>("/auth/register", payload, { retryOnUnauthorized: false });
    tokenStore.clear();
  },
  requestEmailVerification: (email: string) => apiClient.post<void>("/auth/email-verification/request", { email }, { retryOnUnauthorized: false }),
  verifyEmail: (token: string, email?: string) => apiClient.post<void>("/auth/email-verification/confirm", { token, email }, { retryOnUnauthorized: false }),
  forgotPassword: (email: string) => apiClient.post<void>("/auth/password/forgot", { email }, { retryOnUnauthorized: false }),
  resetPassword: (token: string, newPassword: string) => apiClient.post<void>("/auth/password/reset", { token, newPassword }, { retryOnUnauthorized: false }),
  refresh: () => apiClient.refreshSession(),
  logout: async () => {
    try {
      await apiClient.post<void>("/auth/logout", undefined, { retryOnUnauthorized: false });
    } finally {
      tokenStore.clear();
    }
  },
};
