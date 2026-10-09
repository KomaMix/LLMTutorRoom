import { useCallback } from "react";
import { startAttempt, submitAttempt } from "../../api/classroomApi.js";
import { ApiError } from "../../api/httpClient.js";
import {
  canAttemptAcceptAnswers,
  copyAnswers,
  getConflictAttempt,
  isAttemptAccessDenied,
  isAttemptEditable
} from "./attemptLifecycleUtils.js";

export function useAttemptCommands({
  answersRef,
  applyConflict,
  applyAccessDenied,
  blockedAttemptIdsRef,
  callbacksRef,
  clearAutosaveTimer,
  contextRef,
  enqueueAttemptMutation,
  hasUnsavedAnswersRef,
  isCurrentContext,
  navigationPreparationRef,
  persistAnswers,
  revisionRef,
  savedRevisionRef,
  saveOperationRef,
  setAnswersState,
  setDirty,
  setIsSavingAttempt,
  setIsStartingAttempt,
  setIsSubmittingAttempt,
  setMessageState,
  startOperationRef,
  submitOperationRef
}) {
  const onAnswerChange = useCallback((taskId, value) => {
    const operationContext = contextRef.current;
    if (!isAttemptEditable(
      operationContext.selectedAttempt,
      blockedAttemptIdsRef.current)
        || submitOperationRef.current?.generation === operationContext.generation
        || navigationPreparationRef.current?.generation === operationContext.generation) {
      return;
    }

    const nextAnswers = {
      ...answersRef.current,
      [taskId]: value
    };
    revisionRef.current += 1;
    answersRef.current = nextAnswers;
    setAnswersState({
      generation: operationContext.generation,
      value: nextAnswers
    });
    setDirty(true);
    setMessageState({ generation: operationContext.generation, value: "" });
  }, [
    answersRef,
    blockedAttemptIdsRef,
    contextRef,
    navigationPreparationRef,
    revisionRef,
    setAnswersState,
    setDirty,
    setMessageState,
    submitOperationRef
  ]);

  const onStartAttempt = useCallback(() => {
    const operationContext = contextRef.current;
    const existingOperation = startOperationRef.current;
    if (existingOperation?.generation === operationContext.generation) {
      return existingOperation.promise;
    }

    const deadline = new Date(operationContext.selectedTest?.deadline).getTime();
    const availableFrom = new Date(operationContext.selectedTest?.availableFrom).getTime();
    if (!operationContext.selectedTest
        || (Number.isFinite(deadline) && deadline <= Date.now())
        || (Number.isFinite(availableFrom) && availableFrom > Date.now())) {
      if (isCurrentContext(operationContext)) {
        setMessageState({
          generation: operationContext.generation,
          value: Number.isFinite(availableFrom) && availableFrom > Date.now()
            ? "Тест ещё не открыт для выполнения."
            : "Срок выполнения теста истёк."
        });
      }
      return Promise.resolve(false);
    }

    const operation = {
      generation: operationContext.generation,
      promise: null
    };
    startOperationRef.current = operation;
    setIsStartingAttempt(true);
    setMessageState({ generation: operationContext.generation, value: "" });

    operation.promise = (async () => {
      try {
        const attempt = await startAttempt(operationContext.testId);

        callbacksRef.current.updateAttempt(attempt);
        return true;
      } catch (error) {
        if (isAttemptAccessDenied(error)) {
          await applyAccessDenied(operationContext);
          return false;
        }

        if (error instanceof ApiError && error.status === 409) {
          await callbacksRef.current.onAttemptStartConflict?.();
          if (isCurrentContext(operationContext)) {
            setMessageState({
              generation: operationContext.generation,
              value: "Данные назначения изменились. Каталог обновлён — начните ещё раз."
            });
          }
          return false;
        }

        if (isCurrentContext(operationContext)) {
          setMessageState({
            generation: operationContext.generation,
            value: "Не удалось начать тест."
          });
        }
        return false;
      } finally {
        if (startOperationRef.current === operation) {
          startOperationRef.current = null;
        }
        if (isCurrentContext(operationContext)) {
          setIsStartingAttempt(false);
        }
      }
    })();

    return operation.promise;
  }, [
    applyAccessDenied,
    callbacksRef,
    contextRef,
    isCurrentContext,
    setIsStartingAttempt,
    setMessageState,
    startOperationRef
  ]);

  const onSaveAnswers = useCallback(() => {
    const operationContext = contextRef.current;
    const existingOperation = saveOperationRef.current;
    if (existingOperation?.generation === operationContext.generation) {
      return existingOperation.promise;
    }

    const submission = submitOperationRef.current;
    if (submission?.generation === operationContext.generation) {
      return submission.promise;
    }

    if (!canAttemptAcceptAnswers(
      operationContext.selectedAttempt,
      blockedAttemptIdsRef.current)) {
      return Promise.resolve(!hasUnsavedAnswersRef.current);
    }

    clearAutosaveTimer();
    const revision = revisionRef.current;
    const answers = copyAnswers(answersRef.current);
    const operation = {
      generation: operationContext.generation,
      promise: null
    };
    saveOperationRef.current = operation;
    setIsSavingAttempt(true);
    setMessageState({ generation: operationContext.generation, value: "" });

    operation.promise = enqueueAttemptMutation(
      operationContext.attemptId,
      () => persistAnswers({
        operationContext,
        answers,
        revision,
        successMessage: "Ответы сохранены.",
        conflictMessage: "Время выполнения истекло. Ответы больше нельзя изменить."
      }))
      .then(() => true)
      .catch(() => {
        if (isCurrentContext(operationContext)) {
          setMessageState({
            generation: operationContext.generation,
            value: "Не удалось сохранить ответы."
          });
        }
        return false;
      })
      .finally(() => {
        if (saveOperationRef.current === operation) {
          saveOperationRef.current = null;
        }
        if (isCurrentContext(operationContext)) {
          setIsSavingAttempt(false);
        }
      });

    return operation.promise;
  }, [
    answersRef,
    blockedAttemptIdsRef,
    clearAutosaveTimer,
    contextRef,
    enqueueAttemptMutation,
    hasUnsavedAnswersRef,
    isCurrentContext,
    persistAnswers,
    revisionRef,
    saveOperationRef,
    setIsSavingAttempt,
    setMessageState,
    submitOperationRef
  ]);

  const onSubmitAttempt = useCallback(() => {
    const operationContext = contextRef.current;
    const existingOperation = submitOperationRef.current;
    if (existingOperation?.generation === operationContext.generation) {
      return existingOperation.promise;
    }

    if (!isAttemptEditable(
      operationContext.selectedAttempt,
      blockedAttemptIdsRef.current)) {
      return Promise.resolve(false);
    }

    clearAutosaveTimer();
    const revision = revisionRef.current;
    const answers = copyAnswers(answersRef.current);
    const operation = {
      generation: operationContext.generation,
      promise: null
    };
    submitOperationRef.current = operation;
    setIsSubmittingAttempt(true);
    setMessageState({ generation: operationContext.generation, value: "" });

    operation.promise = (async () => {
      let submittedAttempt = null;

      try {
        const result = await enqueueAttemptMutation(operationContext.attemptId, async () => {
          const saveResult = await persistAnswers({
            operationContext,
            answers,
            revision,
            successMessage: "",
            conflictMessage: "Время выполнения истекло. Завершить тест уже нельзя."
          });

          if (saveResult.conflict) {
            return null;
          }

          try {
            const attempt = await submitAttempt(operationContext.attemptId);
            callbacksRef.current.updateAttempt(attempt);
            return attempt;
          } catch (error) {
            if (isAttemptAccessDenied(error)) {
              await applyAccessDenied(operationContext);
              return null;
            }

            const conflictAttempt = getConflictAttempt(error);
            if (!conflictAttempt) {
              throw error;
            }

            applyConflict(
              operationContext,
              conflictAttempt,
              "Время выполнения истекло. Ответы больше нельзя изменить.");
            return null;
          }
        });

        submittedAttempt = result;
        if (submittedAttempt && isCurrentContext(operationContext)) {
          const authoritativeAnswers = copyAnswers(submittedAttempt.answers);
          answersRef.current = authoritativeAnswers;
          savedRevisionRef.current = revisionRef.current;
          setAnswersState({
            generation: operationContext.generation,
            value: authoritativeAnswers
          });
          setDirty(false);
          setMessageState({
            generation: operationContext.generation,
            value: ""
          });
        }
      } catch (error) {
        if (isCurrentContext(operationContext)) {
          setMessageState({
            generation: operationContext.generation,
            value: "Не удалось завершить тест."
          });
        }
      } finally {
        if (submitOperationRef.current === operation) {
          submitOperationRef.current = null;
        }
        if (isCurrentContext(operationContext)) {
          setIsSubmittingAttempt(false);
        }
      }

      if (!submittedAttempt) {
        return false;
      }

      await callbacksRef.current.onSubmitted?.(submittedAttempt);
      return true;
    })();

    return operation.promise;
  }, [
    answersRef,
    applyAccessDenied,
    applyConflict,
    blockedAttemptIdsRef,
    callbacksRef,
    clearAutosaveTimer,
    contextRef,
    enqueueAttemptMutation,
    isCurrentContext,
    persistAnswers,
    revisionRef,
    savedRevisionRef,
    setAnswersState,
    setDirty,
    setIsSubmittingAttempt,
    setMessageState,
    submitOperationRef
  ]);

  return {
    onAnswerChange,
    onSaveAnswers,
    onStartAttempt,
    onSubmitAttempt
  };
}

export default useAttemptCommands;
