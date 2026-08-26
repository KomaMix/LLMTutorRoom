import { useCallback } from "react";

export function useAttemptNavigation({
  clearAutosaveTimer,
  contextRef,
  hasUnsavedAnswersRef,
  navigationPreparationRef,
  onSaveAnswers,
  saveOperationRef,
  startOperationRef,
  submitOperationRef
}) {
  const prepareForNavigation = useCallback(() => {
    const existingPreparation = navigationPreparationRef.current;
    if (existingPreparation) {
      return existingPreparation.promise;
    }

    const operation = {
      generation: contextRef.current.generation,
      promise: null
    };
    navigationPreparationRef.current = operation;
    operation.promise = (async () => {
      clearAutosaveTimer();

      const submission = submitOperationRef.current;
      if (submission) {
        const submitted = await submission.promise;
        if (!submitted) {
          return false;
        }
      }

      const start = startOperationRef.current;
      if (start) {
        await start.promise;
      }

      const save = saveOperationRef.current;
      if (save) {
        const saved = await save.promise;
        if (!saved && hasUnsavedAnswersRef.current) {
          return false;
        }
      }

      if (!hasUnsavedAnswersRef.current) {
        return true;
      }

      const saved = await onSaveAnswers();
      return saved || !hasUnsavedAnswersRef.current;
    })().finally(() => {
      if (navigationPreparationRef.current === operation) {
        navigationPreparationRef.current = null;
      }
    });

    return operation.promise;
  }, [
    clearAutosaveTimer,
    contextRef,
    hasUnsavedAnswersRef,
    navigationPreparationRef,
    onSaveAnswers,
    saveOperationRef,
    startOperationRef,
    submitOperationRef
  ]);

  const shouldBlockNavigation = useCallback(() => {
    const currentGeneration = contextRef.current.generation;
    return hasUnsavedAnswersRef.current
      || startOperationRef.current?.generation === currentGeneration
      || saveOperationRef.current?.generation === currentGeneration
      || submitOperationRef.current?.generation === currentGeneration
      || navigationPreparationRef.current?.generation === currentGeneration;
  }, [
    contextRef,
    hasUnsavedAnswersRef,
    navigationPreparationRef,
    saveOperationRef,
    startOperationRef,
    submitOperationRef
  ]);

  return { prepareForNavigation, shouldBlockNavigation };
}

export default useAttemptNavigation;
