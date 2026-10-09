import { useCallback, useEffect, useState } from "react";
import { getFeedbackTemplates } from "../../api/teachingApi.js";
import { getApiErrorMessage } from "./teacherTestControllerHelpers.js";

export function useFeedbackTemplates() {
  const [templates, setTemplates] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");

  const reload = useCallback(async ({ signal } = {}) => {
    setIsLoading(true);
    setError("");
    try {
      const response = await getFeedbackTemplates({ signal });
      const nextTemplates = Array.isArray(response) ? response : [];
      setTemplates(nextTemplates);
      return nextTemplates;
    } catch (requestError) {
      if (requestError?.name === "AbortError") {
        return null;
      }

      setError(getApiErrorMessage(
        requestError,
        "Не удалось загрузить шаблоны обратной связи."));
      return null;
    } finally {
      if (!signal?.aborted) {
        setIsLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    reload({ signal: controller.signal });
    return () => controller.abort();
  }, [reload]);

  return {
    error,
    isLoading,
    reload,
    templates
  };
}
