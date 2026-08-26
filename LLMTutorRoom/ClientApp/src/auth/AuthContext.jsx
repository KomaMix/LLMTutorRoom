import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState
} from "react";
import {
  getCurrentUser,
  login as loginRequest,
  registerStudent as registerStudentRequest
} from "../api/authApi.js";
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
  const [isRegistering, setIsRegistering] = useState(false);
  const [restoreVersion, setRestoreVersion] = useState(0);
  const authenticationControllerRef = useRef(null);

  const logout = useCallback((message = "") => {
    clearAccessToken();
    setCurrentUser(null);
    setError(message);
    setStatus("unauthenticated");
  }, []);

  const clearError = useCallback(() => setError(""), []);

  const cancelAuthentication = useCallback(() => {
    authenticationControllerRef.current?.abort();
    authenticationControllerRef.current = null;
    setIsSigningIn(false);
    setIsRegistering(false);
  }, []);

  const applySession = useCallback(session => {
    if (!session?.accessToken || !session?.user) {
      throw new ApiError("AuthService вернул неполную сессию.");
    }

    setAccessToken(session.accessToken);
    setCurrentUser(session.user);
    setStatus("authenticated");
  }, []);

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
    cancelAuthentication();
    const controller = new AbortController();
    authenticationControllerRef.current = controller;
    setIsSigningIn(true);
    setError("");

    try {
      const session = await loginRequest(credentials, { signal: controller.signal });
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

      applySession(session);
      return true;
    } catch (requestError) {
      if (requestError?.name === "AbortError") {
        return false;
      }

      if (requestError instanceof ApiError && requestError.status === 401) {
        setError("Неверный email или пароль.");
      } else {
        setError("Не удалось выполнить вход. Проверьте доступность AuthService.");
      }
      return false;
    } finally {
      if (authenticationControllerRef.current === controller) {
        authenticationControllerRef.current = null;
        setIsSigningIn(false);
      }
    }
  }, [applySession, cancelAuthentication]);

  const registerStudent = useCallback(async account => {
    cancelAuthentication();
    const controller = new AbortController();
    authenticationControllerRef.current = controller;
    setIsRegistering(true);
    setError("");

    try {
      const session = await registerStudentRequest(account, { signal: controller.signal });
      if (normalizeRole(session?.user?.role) !== "student") {
        throw new ApiError("AuthService вернул учетную запись без роли студента.");
      }

      applySession(session);
      return true;
    } catch (requestError) {
      if (requestError?.name === "AbortError") {
        return false;
      }

      if (requestError instanceof ApiError && requestError.status === 409) {
        setError("Имя пользователя или email уже заняты.");
      } else if (requestError instanceof ApiError && requestError.status === 400) {
        setError("Проверьте имя пользователя, email и пароль.");
      } else {
        setError("Не удалось создать аккаунт. Проверьте доступность AuthService.");
      }
      return false;
    } finally {
      if (authenticationControllerRef.current === controller) {
        authenticationControllerRef.current = null;
        setIsRegistering(false);
      }
    }
  }, [applySession, cancelAuthentication]);

  const value = useMemo(() => ({
    currentUser,
    status,
    error,
    isSigningIn,
    isRegistering,
    login,
    registerStudent,
    cancelAuthentication,
    clearError,
    logout,
    retrySession: () => setRestoreVersion(current => current + 1)
  }), [
    currentUser,
    status,
    error,
    isSigningIn,
    isRegistering,
    login,
    registerStudent,
    cancelAuthentication,
    clearError,
    logout
  ]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider.");
  }

  return context;
}
