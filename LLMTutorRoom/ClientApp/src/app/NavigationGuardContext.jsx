import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState
} from "react";

const NavigationGuardContext = createContext(null);

export function NavigationGuardProvider({ children, onLogout }) {
  const beforeLogoutRef = useRef(null);
  const logoutOperationRef = useRef(null);
  const [isPreparingLogout, setIsPreparingLogout] = useState(false);

  const registerBeforeLogout = useCallback(guard => {
    beforeLogoutRef.current = guard;

    return () => {
      if (beforeLogoutRef.current === guard) {
        beforeLogoutRef.current = null;
      }
    };
  }, []);

  const requestLogout = useCallback(() => {
    if (logoutOperationRef.current) {
      return logoutOperationRef.current;
    }

    const operation = (async () => {
      setIsPreparingLogout(true);

      try {
        const canLogout = await (beforeLogoutRef.current?.() ?? true);
        if (!canLogout) {
          return false;
        }

        onLogout();
        return true;
      } catch (error) {
        return false;
      } finally {
        logoutOperationRef.current = null;
        setIsPreparingLogout(false);
      }
    })();

    logoutOperationRef.current = operation;
    return operation;
  }, [onLogout]);

  const value = useMemo(() => ({
    isPreparingLogout,
    registerBeforeLogout,
    requestLogout
  }), [isPreparingLogout, registerBeforeLogout, requestLogout]);

  return (
    <NavigationGuardContext.Provider value={value}>
      {children}
    </NavigationGuardContext.Provider>
  );
}

export function useNavigationGuard() {
  const context = useContext(NavigationGuardContext);
  if (!context) {
    throw new Error("useNavigationGuard must be used inside NavigationGuardProvider.");
  }

  return context;
}
