import { createContext, useCallback, useEffect, useMemo, useState, type PropsWithChildren } from "react";
import { authService } from "../services/authService";
import type { AuthResponse, DemoLoginPayload, LoginPayload, RegisterPayload } from "../types/auth";
import { signalRService } from "../features/collaboration/signalRService";

interface AuthContextValue {
  session: AuthResponse | null;
  isInitializing: boolean;
  login: (payload: LoginPayload) => Promise<void>;
  demoLogin: (payload: DemoLoginPayload) => Promise<AuthResponse>;
  register: (payload: RegisterPayload) => Promise<void>;
  logout: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: PropsWithChildren) {
  const [session, setSession] = useState<AuthResponse | null>(null);
  const [isInitializing, setIsInitializing] = useState(true);

  useEffect(() => {
    let active = true;
    authService.refresh()
      .then((restoredSession) => { if (active) setSession(restoredSession); })
      .catch(() => { if (active) setSession(null); })
      .finally(() => { if (active) setIsInitializing(false); });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    const expire = () => {
      setSession(null);
      void signalRService.disconnect();
    };
    window.addEventListener("coding:session-expired", expire);
    return () => window.removeEventListener("coding:session-expired", expire);
  }, []);

  const login = useCallback(async (payload: LoginPayload) => {
    setSession(await authService.login(payload));
  }, []);

  const demoLogin = useCallback(async (payload: DemoLoginPayload) => {
    const demoSession = await authService.demoLogin(payload);
    setSession(demoSession);
    return demoSession;
  }, []);

  const register = useCallback(async (payload: RegisterPayload) => {
    await authService.register(payload);
    setSession(null);
  }, []);

  const logout = useCallback(async () => {
    try {
      await signalRService.disconnect();
    } finally {
      try {
        await authService.logout();
      } finally {
        setSession(null);
      }
    }
  }, []);

  const value = useMemo(
    () => ({ session, isInitializing, login, demoLogin, register, logout }),
    [session, isInitializing, login, demoLogin, register, logout],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
