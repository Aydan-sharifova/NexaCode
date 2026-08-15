import type { AuthResponse } from "../types/auth";
import { tokenStore } from "./tokenStore";

const API_URL = import.meta.env.VITE_API_URL ?? "/api";
let refreshRequest: Promise<AuthResponse> | null = null;

export class ApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = "ApiError";
  }
}

async function getError(response: Response): Promise<ApiError> {
  const responseBody = await response.text().catch(() => "");
  type ProblemResponse = {
    title?: string;
    detail?: string;
    errors?: Record<string, string[]>;
  };
  let problem: ProblemResponse | null = null;
  try {
    problem = responseBody ? JSON.parse(responseBody) as ProblemResponse : null;
  } catch {
    problem = null;
  }
  const validationError = problem?.errors
    ? Object.values(problem.errors).flat()[0]
    : undefined;
  const gatewayMessage = [502, 503, 504].includes(response.status)
    ? "The API is temporarily unavailable. Please try again shortly."
    : undefined;
  const safeServerText = !problem && responseBody && responseBody.length <= 300 && !responseBody.includes("<")
    ? responseBody
    : undefined;
  const statusMessage = response.status === 409
    ? "An account with that email address or username already exists."
    : response.status === 429
      ? "Too many attempts. Please wait a minute and try again."
      : undefined;
  return new ApiError(validationError ?? problem?.detail ?? problem?.title ?? safeServerText ?? gatewayMessage ?? statusMessage ?? `Request failed (${response.status}). Please try again.`, response.status);
}

async function refreshSession(): Promise<AuthResponse> {
  if (!refreshRequest) {
    refreshRequest = fetch(`${API_URL}/auth/refresh`, {
      method: "POST",
      credentials: "include",
    }).then(async (response) => {
      if (!response.ok) throw await getError(response);
      const session = await response.json() as AuthResponse;
      tokenStore.set(session.accessToken);
      return session;
    }).finally(() => { refreshRequest = null; });
  }
  return refreshRequest;
}

interface RequestOptions extends RequestInit {
  retryOnUnauthorized?: boolean;
}

async function request<TResponse>(path: string, options: RequestOptions = {}): Promise<TResponse> {
  const { retryOnUnauthorized = true, headers, ...requestOptions } = options;
  const token = tokenStore.get();
  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      ...requestOptions,
      credentials: "include",
      headers: {
        ...(requestOptions.body && !(requestOptions.body instanceof FormData) ? { "Content-Type": "application/json" } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...headers,
      },
    });
  } catch {
    throw new ApiError(
      import.meta.env.DEV
        ? "Cannot connect to the API. Make sure the backend is running on port 5192."
        : "Cannot connect to the API. Please try again shortly.",
      0,
    );
  }

  if (response.status === 401 && retryOnUnauthorized && !path.startsWith("/auth/")) {
    await refreshSession();
    return request<TResponse>(path, { ...options, retryOnUnauthorized: false });
  }
  if (!response.ok) throw await getError(response);
  if (response.status === 204) return undefined as TResponse;
  const responseText = await response.text();
  if (!responseText) return undefined as TResponse;
  return JSON.parse(responseText) as TResponse;
}

export const apiClient = {
  get: <TResponse>(path: string, options?: RequestOptions) => request<TResponse>(path, options),
  post: <TResponse>(path: string, body?: unknown, options?: RequestOptions) => request<TResponse>(path, {
    ...options,
    method: "POST",
    body: body === undefined ? undefined : JSON.stringify(body),
  }),
  postForm: <TResponse>(path: string, body: FormData) => request<TResponse>(path, { method: "POST", body }),
  put: <TResponse>(path: string, body: unknown) => request<TResponse>(path, { method: "PUT", body: JSON.stringify(body) }),
  delete: <TResponse>(path: string, body?: unknown) => request<TResponse>(path, { method: "DELETE", body: body === undefined ? undefined : JSON.stringify(body) }),
  refreshSession,
};
