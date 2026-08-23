import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { getCurrentUser, login as loginRequest } from "../api/authApi.js";
import {
  ApiError,
  clearAccessToken,
  getAccessToken,
  setAccessToken,
  subscribeToUnauthorized
} from "../api/httpClient.js";
import { normalizeRole } from "../shared/lib/roles.js";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [currentUser, setCurrentUser] = useState(null);
  const [status, setStatus] = useState("checking");
  const [error, setError] = useState("");
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [restoreVersion, setRestoreVersion] = useState(0);

  const logout = useCallback((message = "") => {
    clearAccessToken();
    setCurrentUser(null);
    setError(message);
    setStatus("unauthenticated");
  }, []);

  const clearError = useCallback(() => setError(""), []);

  useEffect(() => subscribeToUnauthorized(() => {
    logout("Сессия истекла. Войдите снова.");
  }), [logout]);

  useEffect(() => {
    const controller = new AbortController();

    async function restoreSession() {
      if (!getAccessToken()) {
        setCurrentUser(null);
        setError("");
        setStatus("unauthenticated");
        return;
      }

      setError("");
      setStatus("checking");

      try {
        const user = await getCurrentUser({ signal: controller.signal });
        setCurrentUser(user);
        setStatus("authenticated");
      } catch (requestError) {
        if (requestError?.name === "AbortError") {
          return;
        }

        if (requestError instanceof ApiError && requestError.status === 401) {
          return;
        }

        setError("Не удалось проверить сессию. Проверьте доступность AuthService.");
        setStatus("error");
      }
    }

    restoreSession();
    return () => controller.abort();
  }, [restoreVersion]);

  const login = useCallback(async (credentials, expectedRole) => {
    setIsSigningIn(true);
    setError("");

    try {
      const session = await loginRequest(credentials);
      if (!session?.accessToken || !session?.user) {
        throw new ApiError("AuthService вернул неполную сессию.");
      }

      const actualRole = normalizeRole(session.user.role);
      if (actualRole !== "admin"
        && (actualRole === "teacher" || actualRole === "student")
        && expectedRole
        && actualRole !== expectedRole) {
        const roleLabel = actualRole === "teacher" ? "Преподаватель" : "Студент";
        setError(`У этой учетной записи роль «${roleLabel}». Выберите соответствующий кабинет.`);
        return false;
      }

      setAccessToken(session.accessToken);
      setCurrentUser(session.user);
      setStatus("authenticated");
      return true;
    } catch (requestError) {
      if (requestError instanceof ApiError && requestError.status === 401) {
        setError("Неверный email или пароль.");
      } else {
        setError("Не удалось выполнить вход. Проверьте доступность AuthService.");
      }
      return false;
    } finally {
      setIsSigningIn(false);
    }
  }, []);

  const value = useMemo(() => ({
    currentUser,
    status,
    error,
    isSigningIn,
    login,
    clearError,
    logout,
    retrySession: () => setRestoreVersion(current => current + 1)
  }), [currentUser, status, error, isSigningIn, login, clearError, logout]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider.");
  }

  return context;
}
