import { useEffect } from "react";
import {
  autosaveDelayMilliseconds,
  copyAnswers,
  isAttemptEditable
} from "./attemptLifecycleUtils.js";

export function useAttemptAutosave({
  answersRef,
  answersState,
  autosaveTimerRef,
  blockedAttemptIdsRef,
  clearAutosaveTimer,
  context,
  contextRef,
  enqueueAttemptMutation,
  hasUnsavedAnswers,
  hasUnsavedAnswersRef,
  isSavingAttempt,
  isSubmittingAttempt,
  mountedRef,
  navigationPreparationRef,
  persistAnswers,
  revisionRef,
  saveOperationRef,
  scheduleFinalSave,
  shouldBlockNavigation,
  submitOperationRef
}) {
  useEffect(() => {
    clearAutosaveTimer();
    const operationContext = contextRef.current;

    if (!hasUnsavedAnswers
        || operationContext.generation !== context.generation
        || !isAttemptEditable(
          operationContext.selectedAttempt,
          blockedAttemptIdsRef.current)
        || saveOperationRef.current?.generation === operationContext.generation
        || submitOperationRef.current?.generation === operationContext.generation
        || navigationPreparationRef.current?.generation === operationContext.generation) {
      return;
    }

    const revision = revisionRef.current;
    const answers = copyAnswers(answersRef.current);
    autosaveTimerRef.current = window.setTimeout(() => {
      autosaveTimerRef.current = null;
      enqueueAttemptMutation(operationContext.attemptId, () => persistAnswers({
        operationContext,
        answers,
        revision,
        successMessage: "",
        conflictMessage: "Время выполнения истекло. Ответы больше нельзя изменить."
      })).catch(() => {
        // Dirty остается true: следующая правка, ручное сохранение или
        // navigation guard повторят запись последней ревизии.
      });
    }, autosaveDelayMilliseconds);

    return clearAutosaveTimer;
  }, [
    answersRef,
    answersState,
    autosaveTimerRef,
    blockedAttemptIdsRef,
    clearAutosaveTimer,
    context.generation,
    contextRef,
    enqueueAttemptMutation,
    hasUnsavedAnswers,
    isSavingAttempt,
    isSubmittingAttempt,
    navigationPreparationRef,
    persistAnswers,
    revisionRef,
    saveOperationRef,
    submitOperationRef
  ]);

  useEffect(() => {
    mountedRef.current = true;

    function handleBeforeUnload(event) {
      if (!shouldBlockNavigation()) {
        return;
      }

      if (hasUnsavedAnswersRef.current) {
        scheduleFinalSave();
      }
      event.preventDefault();
      event.returnValue = "";
    }

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => {
      window.removeEventListener("beforeunload", handleBeforeUnload);
      clearAutosaveTimer();
      mountedRef.current = false;
      scheduleFinalSave();
    };
  }, [
    clearAutosaveTimer,
    hasUnsavedAnswersRef,
    mountedRef,
    scheduleFinalSave,
    shouldBlockNavigation
  ]);
}

export default useAttemptAutosave;
