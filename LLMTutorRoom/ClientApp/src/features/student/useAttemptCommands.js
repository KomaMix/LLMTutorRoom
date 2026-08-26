import { useCallback } from "react";
import { startAttempt, submitAttempt } from "../../api/classroomApi.js";
import { ApiError } from "../../api/httpClient.js";
import {
  canAttemptAcceptAnswers,
  copyAnswers,
  getConflictAttempt,
  isAttemptEditable
} from "./attemptLifecycleUtils.js";

export function useAttemptCommands({
  answersRef,
  applyConflict,
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
    if (!operationContext.selectedTest
        || (Number.isFinite(deadline) && deadline <= Date.now())) {
      if (isCurrentContext(operationContext)) {
        setMessageState({
          generation: operationContext.generation,
          value: "Срок выполнения теста истёк."
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
        const expectedVersionNumber = operationContext.selectedTest?.versionNumber;
        const attempt = await startAttempt(
          operationContext.testId,
          expectedVersionNumber
        );
        if (Number.isInteger(expectedVersionNumber)
            && attempt.testRevision !== expectedVersionNumber) {
          const refreshed = await callbacksRef.current.onAttemptVersionConflict?.();
          if (!refreshed && isCurrentContext(operationContext)) {
            setMessageState({
              generation: operationContext.generation,
              value: "Версия теста изменилась. Обновите страницу перед продолжением."
            });
          }
          return Boolean(refreshed);
        }

        callbacksRef.current.updateAttempt(attempt);
        return true;
      } catch (error) {
        if (error instanceof ApiError && error.status === 409) {
          await callbacksRef.current.onAttemptVersionConflict?.();
          if (isCurrentContext(operationContext)) {
            setMessageState({
              generation: operationContext.generation,
              value: "Опубликована новая версия теста. Каталог обновлён — начните ещё раз."
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
