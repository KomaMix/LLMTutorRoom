import { useCallback, useEffect, useRef, useState } from "react";
import { getAccessToken } from "../../api/httpClient.js";
import {
  copyAnswers,
  getRemainingSeconds
} from "./attemptLifecycleUtils.js";
import { useAttemptAutosave } from "./useAttemptAutosave.js";
import { useAttemptCommands } from "./useAttemptCommands.js";
import { useAttemptNavigation } from "./useAttemptNavigation.js";
import { useAttemptPersistence } from "./useAttemptPersistence.js";

export function useStudentAttempt({
  selectedTest,
  selectedAttempt,
  updateAttempt,
  onAttemptVersionConflict,
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
  const callbacksRef = useRef({ updateAttempt, onAttemptVersionConflict, onSubmitted });
  callbacksRef.current = { updateAttempt, onAttemptVersionConflict, onSubmitted };

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

  const {
    applyConflict,
    enqueueAttemptMutation,
    persistAnswers,
    scheduleFinalSave
  } = useAttemptPersistence({
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
  });

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

  const {
    onAnswerChange,
    onSaveAnswers,
    onStartAttempt,
    onSubmitAttempt
  } = useAttemptCommands({
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
  });

  const {
    prepareForNavigation,
    shouldBlockNavigation
  } = useAttemptNavigation({
    clearAutosaveTimer,
    contextRef,
    hasUnsavedAnswersRef,
    navigationPreparationRef,
    onSaveAnswers,
    saveOperationRef,
    startOperationRef,
    submitOperationRef
  });

  useAttemptAutosave({
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
  });

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
