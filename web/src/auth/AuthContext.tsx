import { createContext, useContext, useState, useCallback, type ReactNode } from "react";
import { api, setToken, type LoginResult } from "../api/client";

/** Holds the authenticated session. The JWT lives in memory only (not localStorage) so it
 *  isn't exposed to XSS-readable storage; a refresh logs out — acceptable for this scaffold. */
interface AuthValue {
  user: LoginResult | null;
  isAuthenticated: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<LoginResult | null>(null);

  const login = useCallback(async (username: string, password: string) => {
    const result = await api.login(username, password);
    setToken(result.token);
    setUser(result);
  }, []);

  const logout = useCallback(() => {
    api.logout();        // clear the HttpOnly PDF cookie server-side (best-effort)
    setToken(null);
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: user !== null, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}

/** Human-readable role names (mirrors the server Role enum values). */
export const roleNames: Record<string, { ar: string; en: string }> = {
  Administrator: { ar: "مدير النظام", en: "Administrator" },
  RegistryHead: { ar: "رئيس الديوان", en: "Head of Registry" },
  Copyist: { ar: "الناسخ", en: "Copyist" },
  Reviewer: { ar: "المدقق", en: "Reviewer" },
};
