import { useCallback } from "react";
import { saveAttemptAnswers } from "../../api/classroomApi.js";
import {
  canAttemptAcceptAnswers,
  copyAnswers,
  createFinalSaveOptions,
  getConflictAttempt
} from "./attemptLifecycleUtils.js";

export function useAttemptPersistence({
  accessTokenSnapshotRef,
  answersRef,
  attemptQueuesRef,
  blockedAttemptIdsRef,
  callbacksRef,
  clearAutosaveTimer,
  contextRef,
  finalSaveRevisionsRef,
  hasUnsavedAnswersRef,
  isCurrentContext,
  mountedRef,
  revisionRef,
  savedRevisionRef,
  setAnswersState,
  setDirty,
  setMessageState
}) {
  const enqueueAttemptMutation = useCallback((queuedAttemptId, work) => {
    if (queuedAttemptId == null) {
      return Promise.reject(new Error("Attempt is required for a student mutation."));
    }

    const queues = attemptQueuesRef.current;
    const previous = queues.get(queuedAttemptId) ?? Promise.resolve();
    const operation = previous
      .catch(() => undefined)
      .then(work);
    const tail = operation.catch(() => undefined);
    queues.set(queuedAttemptId, tail);
    tail.then(() => {
      if (queues.get(queuedAttemptId) === tail) {
        queues.delete(queuedAttemptId);
      }
    });

    return operation;
  }, [attemptQueuesRef]);

  const applyConflict = useCallback((
    operationContext,
    attempt,
    conflictMessage,
    shouldUpdateOverview = true
  ) => {
    const conflictedAttemptId = attempt?.id ?? operationContext.attemptId;
    if (conflictedAttemptId != null) {
      blockedAttemptIdsRef.current.add(conflictedAttemptId);
    }

    if (attempt && shouldUpdateOverview) {
      callbacksRef.current.updateAttempt(attempt);
    }

    if (!isCurrentContext(operationContext)) {
      return;
    }

    clearAutosaveTimer();
    const authoritativeAnswers = copyAnswers(attempt?.answers);
    answersRef.current = authoritativeAnswers;
    savedRevisionRef.current = revisionRef.current;
    setAnswersState({
      generation: operationContext.generation,
      value: authoritativeAnswers
    });
    setDirty(false);
    setMessageState({
      generation: operationContext.generation,
      value: conflictMessage
    });
  }, [
    answersRef,
    blockedAttemptIdsRef,
    callbacksRef,
    clearAutosaveTimer,
    isCurrentContext,
    revisionRef,
    savedRevisionRef,
    setAnswersState,
    setDirty,
    setMessageState
  ]);

  const persistAnswers = useCallback(async ({
    operationContext,
    answers,
    revision,
    successMessage,
    conflictMessage,
    requestOptions,
    updateOverviewAfterUnmount = true
  }) => {
    if (blockedAttemptIdsRef.current.has(operationContext.attemptId)) {
      return { conflict: true, attempt: null };
    }

    try {
      const attempt = await saveAttemptAnswers(
        operationContext.attemptId,
        answers,
        requestOptions);

      if (updateOverviewAfterUnmount || mountedRef.current) {
        callbacksRef.current.updateAttempt(attempt);
      }

      if (isCurrentContext(operationContext)) {
        savedRevisionRef.current = Math.max(savedRevisionRef.current, revision);
        const stillHasUnsavedAnswers = revisionRef.current > savedRevisionRef.current;
        setDirty(stillHasUnsavedAnswers);

        if (successMessage && !stillHasUnsavedAnswers) {
          setMessageState({
            generation: operationContext.generation,
            value: successMessage
          });
        }
      }

      return { conflict: false, attempt };
    } catch (error) {
      const conflictAttempt = getConflictAttempt(error);
      if (conflictAttempt) {
        applyConflict(
          operationContext,
          conflictAttempt,
          conflictMessage,
          updateOverviewAfterUnmount || mountedRef.current);
        return { conflict: true, attempt: conflictAttempt };
      }

      throw error;
    }
  }, [
    applyConflict,
    blockedAttemptIdsRef,
    callbacksRef,
    isCurrentContext,
    mountedRef,
    revisionRef,
    savedRevisionRef,
    setDirty,
    setMessageState
  ]);

  const scheduleFinalSave = useCallback(() => {
    const operationContext = contextRef.current;
    if (!hasUnsavedAnswersRef.current
        || !canAttemptAcceptAnswers(
          operationContext.selectedAttempt,
          blockedAttemptIdsRef.current)) {
      return Promise.resolve(true);
    }

    const revision = revisionRef.current;
    const existingFinalSave = finalSaveRevisionsRef.current
      .get(operationContext.attemptId);
    if (existingFinalSave?.generation === operationContext.generation
        && existingFinalSave.revision >= revision) {
      return existingFinalSave.promise;
    }

    const answers = copyAnswers(answersRef.current);
    const requestOptions = createFinalSaveOptions(accessTokenSnapshotRef.current);
    const finalSave = {
      generation: operationContext.generation,
      revision,
      promise: null
    };
    finalSave.promise = enqueueAttemptMutation(operationContext.attemptId, () => persistAnswers({
      operationContext,
      answers,
      revision,
      successMessage: "",
      conflictMessage: "Время выполнения истекло. Ответы больше нельзя изменить.",
      requestOptions,
      updateOverviewAfterUnmount: false
    })).then(() => true, () => false);
    finalSaveRevisionsRef.current.set(operationContext.attemptId, finalSave);
    finalSave.promise.then(saved => {
      if (!saved
          && finalSaveRevisionsRef.current.get(operationContext.attemptId) === finalSave) {
        finalSaveRevisionsRef.current.delete(operationContext.attemptId);
      }
    });

    return finalSave.promise;
  }, [
    accessTokenSnapshotRef,
    answersRef,
    blockedAttemptIdsRef,
    contextRef,
    enqueueAttemptMutation,
    finalSaveRevisionsRef,
    hasUnsavedAnswersRef,
    persistAnswers,
    revisionRef
  ]);

  return {
    applyConflict,
    enqueueAttemptMutation,
    persistAnswers,
    scheduleFinalSave
  };
}

export default useAttemptPersistence;
