import { useCallback, useEffect, useRef, useState } from "react";
import { getOverview } from "../api/classroomApi.js";
import { parseOverview } from "../shared/lib/overview.js";

export function useOverview({ enabled, identityKey }) {
  const [data, setData] = useState(null);
  const [isLoading, setIsLoading] = useState(enabled);
  const [error, setError] = useState("");
  const latestRequestId = useRef(0);
  const activeController = useRef(null);
  const lifecycle = useRef({ active: false, identityKey: null });

  const load = useCallback(async ({ clear = false } = {}) => {
    if (!enabled
      || !lifecycle.current.active
      || lifecycle.current.identityKey !== identityKey) {
      return false;
    }

    activeController.current?.abort();
    const controller = new AbortController();
    activeController.current = controller;
    const requestId = ++latestRequestId.current;
    if (clear) {
      setData(null);
    }
    setIsLoading(true);
    setError("");

    try {
      const overview = parseOverview(await getOverview({ signal: controller.signal }));
      if (requestId !== latestRequestId.current
        || controller.signal.aborted
        || !lifecycle.current.active
        || lifecycle.current.identityKey !== identityKey) {
        return false;
      }

      setData(overview);
      return true;
    } catch (requestError) {
      if (requestError?.name === "AbortError"
        || requestId !== latestRequestId.current
        || !lifecycle.current.active
        || lifecycle.current.identityKey !== identityKey) {
        return false;
      }

      setError("Не удалось загрузить данные. Проверьте доступность сервисов.");
      return false;
    } finally {
      if (requestId === latestRequestId.current
        && !controller.signal.aborted
        && lifecycle.current.active
        && lifecycle.current.identityKey === identityKey) {
        setIsLoading(false);
        activeController.current = null;
      }
    }
  }, [enabled, identityKey]);

  useEffect(() => {
    lifecycle.current = { active: enabled, identityKey };
    latestRequestId.current += 1;
    activeController.current?.abort();
    activeController.current = null;
    setError("");

    if (!enabled) {
      setData(null);
      setIsLoading(false);
      return () => {
        lifecycle.current = { active: false, identityKey: null };
      };
    }

    load({ clear: true });
    return () => {
      if (lifecycle.current.identityKey === identityKey) {
        lifecycle.current = { active: false, identityKey: null };
      }
      latestRequestId.current += 1;
      activeController.current?.abort();
      activeController.current = null;
    };
  }, [enabled, identityKey, load]);

  const updateAttempt = useCallback(attempt => {
    setData(current => {
      if (!current) {
        return current;
      }

      const existingAttempt = current.attempts.find(item => item.id === attempt.id);
      const existingIsTerminal = existingAttempt?.status === "submitted"
        || existingAttempt?.status === "expired";
      if (existingIsTerminal && attempt.status === "in-progress") {
        return current;
      }

      return {
        ...current,
        attempts: [
          attempt,
          ...current.attempts.filter(item => item.id !== attempt.id)
        ]
      };
    });
  }, []);

  return {
    data,
    error,
    isLoading,
    refresh: load,
    updateAttempt
  };
}
