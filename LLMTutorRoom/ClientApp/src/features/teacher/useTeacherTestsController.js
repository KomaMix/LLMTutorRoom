import { useEffect, useRef, useState } from "react";
import {
  createInitialTaskForm,
  createInitialTestForm,
  createTaskFormFromTask,
  createTestFormFromTest
} from "./teacherTestHelpers.js";
import { createTeacherTaskActions } from "./teacherTaskActions.js";
import { createTeacherTestActions } from "./teacherTestActions.js";
import { serializeForm } from "./teacherTestControllerHelpers.js";
import { createTeacherVersionActions } from "./teacherVersionActions.js";
import { useTeacherUnsavedChangesGuard } from "./useTeacherUnsavedChangesGuard.js";

export function useTeacherTestsController({
  models,
  onTestsChanged,
  selectedTest
}) {
  const [viewedTest, setViewedTest] = useState(selectedTest);
  const activeTest = viewedTest?.id === selectedTest?.id ? viewedTest : selectedTest;
  const currentSelectedTestId = activeTest?.id ?? "";
  const currentSelectedTestKey = activeTest
    ? `${activeTest.id}:${activeTest.versionNumber}`
    : "";
  const selectedTestRef = useRef(activeTest);
  const currentSelectedTestIdRef = useRef(currentSelectedTestId);
  const currentSelectedTestKeyRef = useRef(currentSelectedTestKey);
  const catalogTestRef = useRef(selectedTest);
  const selectionGeneration = useRef(0);
  const logicalSelectionGeneration = useRef(0);
  const operationGenerations = useRef({});
  const hasInitializedDefaultModel = useRef(models.length > 0);
  selectedTestRef.current = activeTest;
  catalogTestRef.current = selectedTest;
  currentSelectedTestIdRef.current = currentSelectedTestId;
  currentSelectedTestKeyRef.current = currentSelectedTestKey;

  const [testForm, setTestForm] = useState(() =>
    createInitialTestForm(models[0]?.key ?? ""));
  const [testEditForm, setTestEditForm] = useState(() => createTestFormFromTest(activeTest));
  const [taskForm, setTaskForm] = useState(createInitialTaskForm);
  const [taskEditForm, setTaskEditForm] = useState(createInitialTaskForm);
  const [editingTaskId, setEditingTaskId] = useState("");
  const [taskPanelMode, setTaskPanelMode] = useState("list");
  const [deleteTaskCandidate, setDeleteTaskCandidate] = useState(null);
  const [isCreatingTest, setIsCreatingTest] = useState(false);
  const [isCreatingTask, setIsCreatingTask] = useState(false);
  const [isSavingTest, setIsSavingTest] = useState(false);
  const [isSavingTask, setIsSavingTask] = useState(false);
  const [busyTaskId, setBusyTaskId] = useState("");
  const [isVersionBusy, setIsVersionBusy] = useState(false);
  const [testMessage, setTestMessage] = useState("");
  const [testEditMessage, setTestEditMessage] = useState("");
  const [taskMessage, setTaskMessage] = useState("");
  const [taskEditMessage, setTaskEditMessage] = useState("");
  const [versionMessage, setVersionMessage] = useState("");
  const createTestBaselineRef = useRef(testForm);
  const testEditBaselineRef = useRef(testEditForm);
  const taskCreateBaselineRef = useRef(taskForm);
  const taskEditBaselineRef = useRef(taskEditForm);

  const hasUnsavedCreateTestChanges = serializeForm(testForm)
    !== serializeForm(createTestBaselineRef.current);
  const hasUnsavedTestEditChanges = activeTest?.status === "draft"
    && serializeForm(testEditForm) !== serializeForm(testEditBaselineRef.current);
  const hasUnsavedTaskCreateChanges = activeTest?.status === "draft"
    && serializeForm(taskForm) !== serializeForm(taskCreateBaselineRef.current);
  const hasUnsavedTaskEditChanges = activeTest?.status === "draft"
    && Boolean(editingTaskId)
    && serializeForm(taskEditForm) !== serializeForm(taskEditBaselineRef.current);
  const hasUnsavedDraftChanges = hasUnsavedTestEditChanges
    || hasUnsavedTaskCreateChanges
    || hasUnsavedTaskEditChanges;
  const hasUnsavedTeacherChanges = hasUnsavedCreateTestChanges || hasUnsavedDraftChanges;
  const hasMutationInProgress = isCreatingTest
    || isCreatingTask
    || isSavingTest
    || isSavingTask
    || Boolean(busyTaskId)
    || isVersionBusy;
  const hasNonVersionMutationInProgress = isCreatingTest
    || isCreatingTask
    || isSavingTest
    || isSavingTask
    || Boolean(busyTaskId);

  useTeacherUnsavedChangesGuard(hasUnsavedTeacherChanges);

  function beginOperation(name, testKey = null) {
    const generation = (operationGenerations.current[name] ?? 0) + 1;
    operationGenerations.current[name] = generation;

    return {
      generation,
      selectionGeneration: selectionGeneration.current,
      logicalSelectionGeneration: logicalSelectionGeneration.current,
      testKey
    };
  }

  function isCurrentOperation(name, operation) {
    if (operationGenerations.current[name] !== operation.generation) {
      return false;
    }

    if (operation.testKey === null) {
      return true;
    }

    return selectionGeneration.current === operation.selectionGeneration
      && currentSelectedTestKeyRef.current === operation.testKey;
  }

  function isCurrentLogicalOperation(name, operation) {
    return operationGenerations.current[name] === operation.generation
      && selectionGeneration.current === operation.selectionGeneration
      && currentSelectedTestIdRef.current === operation.logicalTestId;
  }

  function isCurrentLogicalTestOperation(name, operation) {
    return operationGenerations.current[name] === operation.generation
      && logicalSelectionGeneration.current === operation.logicalSelectionGeneration
      && currentSelectedTestIdRef.current === operation.logicalTestId;
  }

  useEffect(() => {
    if (!selectedTest) {
      setViewedTest(null);
      return;
    }

    setViewedTest(current => {
      if (!current || current.id !== selectedTest.id) {
        return selectedTest;
      }

      const currentVersion = current.versionNumber;
      const selectedVersion = selectedTest.versionNumber;
      if (currentVersion === selectedVersion) {
        return selectedTest;
      }

      const currentVersionSummary = selectedTest.versions?.find(
        version => version.versionNumber === currentVersion);
      if (!currentVersionSummary) {
        return selectedTest;
      }

      return {
        ...current,
        status: currentVersionSummary.status,
        publishedVersionNumber: selectedTest.publishedVersionNumber,
        hasDraft: selectedTest.hasDraft,
        versions: selectedTest.versions ?? []
      };
    });
  }, [selectedTest]);

  useEffect(() => {
    selectionGeneration.current += 1;
    const nextTestEditForm = createTestFormFromTest(selectedTestRef.current);
    const nextTaskCreateForm = createInitialTaskForm();
    const nextTaskEditForm = createInitialTaskForm();
    testEditBaselineRef.current = nextTestEditForm;
    taskCreateBaselineRef.current = nextTaskCreateForm;
    taskEditBaselineRef.current = nextTaskEditForm;
    setTestEditForm(nextTestEditForm);
    setTaskForm(nextTaskCreateForm);
    setTaskEditForm(nextTaskEditForm);
    setEditingTaskId("");
    setTaskPanelMode("list");
    setDeleteTaskCandidate(null);
    setTestEditMessage("");
    setTaskMessage("");
    setTaskEditMessage("");
    setIsSavingTest(false);
    setIsCreatingTask(false);
    setIsSavingTask(false);
    setBusyTaskId("");
    setIsVersionBusy(false);
  }, [currentSelectedTestKey]);

  useEffect(() => {
    logicalSelectionGeneration.current += 1;
    setVersionMessage("");
  }, [currentSelectedTestId]);

  useEffect(() => {
    if (!activeTest) {
      return;
    }

    const nextBaseline = createTestFormFromTest(activeTest);
    const previousBaseline = testEditBaselineRef.current;
    testEditBaselineRef.current = nextBaseline;
    setTestEditForm(current => {
      const isDirty = serializeForm(current) !== serializeForm(previousBaseline);
      return isDirty ? current : nextBaseline;
    });

    if (!editingTaskId) {
      return;
    }

    const editingTask = activeTest.tasks?.find(task => task.id === editingTaskId);
    if (!editingTask) {
      return;
    }

    const nextTaskBaseline = createTaskFormFromTask(editingTask);
    const previousTaskBaseline = taskEditBaselineRef.current;
    taskEditBaselineRef.current = nextTaskBaseline;
    setTaskEditForm(current => {
      const isDirty = serializeForm(current) !== serializeForm(previousTaskBaseline);
      return isDirty ? current : nextTaskBaseline;
    });
  }, [activeTest, editingTaskId]);

  useEffect(() => {
    if (hasInitializedDefaultModel.current || models.length === 0) {
      return;
    }

    hasInitializedDefaultModel.current = true;
    setTestForm(current => {
      if (current.llmModelKey) {
        return current;
      }

      const next = { ...current, llmModelKey: models[0].key };
      createTestBaselineRef.current = {
        ...createTestBaselineRef.current,
        llmModelKey: models[0].key
      };
      return next;
    });
  }, [models]);

  const testActions = createTeacherTestActions({
    activeTest,
    beginOperation,
    busyTaskId,
    createTestBaselineRef,
    currentSelectedTestKey,
    hasMutationInProgress,
    isCreatingTask,
    isCreatingTest,
    isCurrentOperation,
    isSavingTask,
    isSavingTest,
    isVersionBusy,
    models,
    onTestsChanged,
    setIsCreatingTest,
    setIsSavingTest,
    setTestEditMessage,
    setTestForm,
    setTestMessage,
    testEditBaselineRef,
    testEditForm,
    testForm
  });
  const taskActions = createTeacherTaskActions({
    activeTest,
    beginOperation,
    busyTaskId,
    currentSelectedTestKey,
    editingTaskId,
    hasMutationInProgress,
    hasUnsavedTaskCreateChanges,
    hasUnsavedTaskEditChanges,
    isCreatingTask,
    isCreatingTest,
    isCurrentOperation,
    isSavingTask,
    isSavingTest,
    isVersionBusy,
    onTestsChanged,
    setBusyTaskId,
    setDeleteTaskCandidate,
    setEditingTaskId,
    setIsCreatingTask,
    setIsSavingTask,
    setTaskEditForm,
    setTaskEditMessage,
    setTaskForm,
    setTaskMessage,
    setTaskPanelMode,
    taskCreateBaselineRef,
    taskEditBaselineRef,
    taskEditForm,
    taskForm,
    taskPanelMode
  });
  const versionActions = createTeacherVersionActions({
    activeTest,
    beginOperation,
    catalogTestRef,
    hasNonVersionMutationInProgress,
    hasUnsavedDraftChanges,
    isCurrentLogicalOperation,
    isCurrentLogicalTestOperation,
    isVersionBusy,
    onTestsChanged,
    setIsVersionBusy,
    setVersionMessage,
    setViewedTest,
    taskPanelMode
  });

  return {
    activeTest,
    areDraftControlsDisabled: hasMutationInProgress,
    busyTaskId,
    deleteTaskCandidate,
    editingTaskId,
    hasMutationInProgress,
    hasUnsavedCreateTestChanges,
    isCreatingTask,
    isCreatingTest,
    isSavingTask,
    isSavingTest,
    setDeleteTaskCandidate,
    setTaskEditForm,
    setTaskForm,
    setTestEditForm,
    setTestForm,
    taskEditForm,
    taskEditMessage,
    taskForm,
    taskMessage,
    taskPanelMode,
    testEditForm,
    testEditMessage,
    testForm,
    testMessage,
    versionMessage,
    ...testActions,
    ...taskActions,
    ...versionActions
  };
}
