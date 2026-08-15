import { useCallback, useEffect, useRef, useState } from "react";
import {
  saveAttemptAnswers,
  startAttempt,
  submitAttempt
} from "../../api/classroomApi.js";
import { ApiError, getAccessToken } from "../../api/httpClient.js";

const autosaveDelayMilliseconds = 900;

function copyAnswers(answers) {
  if (!answers || typeof answers !== "object" || Array.isArray(answers)) {
    return {};
  }

  return { ...answers };
}

function getRemainingSeconds(attempt, now) {
  if (!attempt || attempt.status !== "in-progress") {
    return 0;
  }

  const endsAt = new Date(attempt.endsAt).getTime();
  if (!Number.isFinite(endsAt)) {
    return 0;
  }

  return Math.max(0, Math.ceil((endsAt - now) / 1000));
}

function getConflictAttempt(error) {
  if (!(error instanceof ApiError) || error.status !== 409) {
    return null;
  }

  return error.data && typeof error.data === "object"
    ? error.data
    : null;
}

function canAttemptAcceptAnswers(attempt, blockedAttemptIds) {
  return Boolean(
    attempt
    && attempt.status === "in-progress"
    && !blockedAttemptIds.has(attempt.id)
  );
}

function isAttemptEditable(attempt, blockedAttemptIds) {
  if (!canAttemptAcceptAnswers(attempt, blockedAttemptIds)) {
    return false;
  }

  const endsAt = new Date(attempt.endsAt).getTime();
  return Number.isFinite(endsAt) && endsAt > Date.now();
}

function createFinalSaveOptions(accessToken) {
  const headers = accessToken
    ? { Authorization: `Bearer ${accessToken}` }
    : {};

  return {
    auth: false,
    headers,
    keepalive: true
  };
}

export function useStudentAttempt({
  selectedTest,
  selectedAttempt,
  updateAttempt,
  onSubmitted
}) {
  const testId = selectedTest?.id ?? null;
  const attemptId = selectedAttempt?.id ?? null;
  const contextRef = useRef({
    generation: 0,
    testId: Symbol("initial-test"),
    attemptId: Symbol("initial-attempt"),
    selectedTest: null,
    selectedAttempt: null
  });
  const previousContext = contextRef.current;
  const didContextChange = previousContext.testId !== testId
    || previousContext.attemptId !== attemptId;

  if (didContextChange) {
    contextRef.current = {
      generation: previousContext.generation + 1,
      testId,
      attemptId,
      selectedTest,
      selectedAttempt
    };
  } else {
    previousContext.selectedTest = selectedTest;
    previousContext.selectedAttempt = selectedAttempt;
  }

  const context = contextRef.current;
  const callbacksRef = useRef({ updateAttempt, onSubmitted });
  callbacksRef.current = { updateAttempt, onSubmitted };

  const mountedRef = useRef(true);
  const accessTokenSnapshotRef = useRef(getAccessToken());
  const answersRef = useRef(copyAnswers(selectedAttempt?.answers));
  const revisionRef = useRef(0);
  const savedRevisionRef = useRef(0);
  const attemptQueuesRef = useRef(new Map());
  const autosaveTimerRef = useRef(null);
  const hasUnsavedAnswersRef = useRef(false);
  const blockedAttemptIdsRef = useRef(new Set());
  const finalSaveRevisionsRef = useRef(new Map());
  const startOperationRef = useRef(null);
  const saveOperationRef = useRef(null);
  const submitOperationRef = useRef(null);
  const navigationPreparationRef = useRef(null);

  if (didContextChange) {
    answersRef.current = copyAnswers(selectedAttempt?.answers);
    revisionRef.current = 0;
    savedRevisionRef.current = 0;
    hasUnsavedAnswersRef.current = false;
    startOperationRef.current = null;
    saveOperationRef.current = null;
    submitOperationRef.current = null;
    navigationPreparationRef.current = null;
  }

  const [answersState, setAnswersState] = useState(() => ({
    generation: context.generation,
    value: copyAnswers(selectedAttempt?.answers)
  }));
  const [messageState, setMessageState] = useState(() => ({
    generation: context.generation,
    value: ""
  }));
  const [hasUnsavedAnswers, setHasUnsavedAnswers] = useState(false);
  const [isStartingAttempt, setIsStartingAttempt] = useState(false);
  const [isSavingAttempt, setIsSavingAttempt] = useState(false);
  const [isSubmittingAttempt, setIsSubmittingAttempt] = useState(false);
  const [now, setNow] = useState(Date.now());

  const isCurrentContext = useCallback(operationContext =>
    mountedRef.current
      && contextRef.current.generation === operationContext.generation,
  []);

  const setDirty = useCallback(value => {
    hasUnsavedAnswersRef.current = value;
    if (mountedRef.current) {
      setHasUnsavedAnswers(value);
    }
  }, []);

  const clearAutosaveTimer = useCallback(() => {
    if (autosaveTimerRef.current !== null) {
      window.clearTimeout(autosaveTimerRef.current);
      autosaveTimerRef.current = null;
    }
  }, []);

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
  }, []);

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
  }, [clearAutosaveTimer, isCurrentContext, setDirty]);

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
  }, [applyConflict, isCurrentContext, setDirty]);

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
  }, [enqueueAttemptMutation, persistAnswers]);

  useEffect(() => {
    const operationContext = contextRef.current;
    if (operationContext.generation !== context.generation) {
      return;
    }

    clearAutosaveTimer();
    const initialAnswers = copyAnswers(operationContext.selectedAttempt?.answers);
    answersRef.current = initialAnswers;
    revisionRef.current = 0;
    savedRevisionRef.current = 0;
    startOperationRef.current = null;
    saveOperationRef.current = null;
    submitOperationRef.current = null;
    navigationPreparationRef.current = null;
    setAnswersState({
      generation: operationContext.generation,
      value: initialAnswers
    });
    setMessageState({ generation: operationContext.generation, value: "" });
    setDirty(false);
    setIsStartingAttempt(false);
    setIsSavingAttempt(false);
    setIsSubmittingAttempt(false);
    setNow(Date.now());
  }, [clearAutosaveTimer, context.generation, setDirty]);

  useEffect(() => {
    if (selectedAttempt?.status === "in-progress") {
      return;
    }

    clearAutosaveTimer();

    if (selectedAttempt?.id != null) {
      blockedAttemptIdsRef.current.add(selectedAttempt.id);
    }

    if (!isCurrentContext(context)) {
      return;
    }

    const authoritativeAnswers = copyAnswers(selectedAttempt?.answers);
    answersRef.current = authoritativeAnswers;
    savedRevisionRef.current = revisionRef.current;
    setAnswersState({
      generation: context.generation,
      value: authoritativeAnswers
    });
    setDirty(false);
  }, [
    clearAutosaveTimer,
    context,
    isCurrentContext,
    selectedAttempt?.answers,
    selectedAttempt?.id,
    selectedAttempt?.status,
    setDirty
  ]);

  useEffect(() => {
    if (!selectedAttempt || selectedAttempt.status !== "in-progress") {
      setNow(Date.now());
      return;
    }

    const endsAt = new Date(selectedAttempt.endsAt).getTime();
    const tick = () => {
      const currentTime = Date.now();
      setNow(currentTime);
      return Number.isFinite(endsAt) && currentTime >= endsAt;
    };

    if (tick()) {
      return;
    }

    const timer = window.setInterval(() => {
      if (tick()) {
        window.clearInterval(timer);
      }
    }, 1000);

    return () => window.clearInterval(timer);
  }, [selectedAttempt]);

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
  }, [setDirty]);

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
        const attempt = await startAttempt(operationContext.testId);
        callbacksRef.current.updateAttempt(attempt);
        return true;
      } catch (error) {
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
  }, [isCurrentContext]);

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
  }, [clearAutosaveTimer, enqueueAttemptMutation, isCurrentContext, persistAnswers]);

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
            value: "Ответы отправлены. Результаты станут доступны позже."
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
    applyConflict,
    clearAutosaveTimer,
    enqueueAttemptMutation,
    isCurrentContext,
    persistAnswers,
    setDirty
  ]);

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
  }, [clearAutosaveTimer, onSaveAnswers]);

  const shouldBlockNavigation = useCallback(() => {
    const currentGeneration = contextRef.current.generation;
    return hasUnsavedAnswersRef.current
      || startOperationRef.current?.generation === currentGeneration
      || saveOperationRef.current?.generation === currentGeneration
      || submitOperationRef.current?.generation === currentGeneration
      || navigationPreparationRef.current?.generation === currentGeneration;
  }, []);

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
    answersState,
    clearAutosaveTimer,
    context.generation,
    enqueueAttemptMutation,
    hasUnsavedAnswers,
    isSavingAttempt,
    isSubmittingAttempt,
    persistAnswers
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
  }, [clearAutosaveTimer, scheduleFinalSave, shouldBlockNavigation]);

  const answers = answersState.generation === context.generation
    ? answersState.value
    : copyAnswers(selectedAttempt?.answers);
  const message = messageState.generation === context.generation
    ? messageState.value
    : "";

  return {
    answers,
    remainingSeconds: getRemainingSeconds(selectedAttempt, now),
    message,
    hasUnsavedAnswers: answersState.generation === context.generation
      && hasUnsavedAnswers,
    isStartingAttempt: isStartingAttempt
      && startOperationRef.current?.generation === context.generation,
    isSavingAttempt: isSavingAttempt
      && saveOperationRef.current?.generation === context.generation,
    isSubmittingAttempt: isSubmittingAttempt
      && submitOperationRef.current?.generation === context.generation,
    onAnswerChange,
    onStartAttempt,
    onSaveAnswers,
    onSubmitAttempt,
    prepareForNavigation,
    shouldBlockNavigation
  };
}

export default useStudentAttempt;
