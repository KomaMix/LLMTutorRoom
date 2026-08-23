import { useCallback, useEffect, useRef, useState } from "react";
import { useBlocker } from "react-router-dom";
import { useNavigationGuard } from "../../app/NavigationGuardContext.jsx";
import {
  createDraftVersion,
  createTask as createTaskRequest,
  createTest as createTestRequest,
  deleteDraftVersion,
  deleteTask as deleteTaskRequest,
  getTestVersion,
  publishTestVersion,
  setTaskVisibility as setTaskVisibilityRequest,
  updateTask as updateTaskRequest,
  updateTest as updateTestRequest
} from "../../api/teachingApi.js";
import { TestDetails } from "./TestDetails.jsx";
import { TestList } from "./TestList.jsx";
import {
  createInitialTaskForm,
  createInitialTestForm,
  createTaskFormFromTask,
  createTaskPayload,
  createTestFormFromTest,
  createTestPayload,
  validateTaskForm
} from "./teacherTestHelpers.js";

const unsavedChangesPrompt = "Есть несохранённые изменения теста или задания. Покинуть страницу и потерять их?";

function serializeForm(form) {
  return JSON.stringify(form ?? null);
}

function getApiErrorMessage(error, fallback) {
  const response = error?.data;
  const detail = typeof response === "string"
    ? response
    : typeof response?.detail === "string"
      ? response.detail
      : "";

  if (detail.trim()) {
    return detail.trim();
  }

  if (error?.status === 401) {
    return "Сессия завершилась. Войдите снова.";
  }

  if (error?.status === 403) {
    return "Недостаточно прав для этого действия.";
  }

  if (!error?.status) {
    return "Сервис недоступен. Проверьте подключение и повторите попытку.";
  }

  return fallback;
}

function mergeCatalogVersionMetadata(version, catalogTest) {
  if (!version || !catalogTest || version.id !== catalogTest.id) {
    return version;
  }

  const versionNumber = version.versionNumber;
  const catalogVersionNumber = catalogTest.versionNumber;
  if (catalogVersionNumber === versionNumber
    && catalogTest.contentRevision >= version.contentRevision) {
    return catalogTest;
  }

  const versionSummary = catalogTest.versions?.find(
    item => item.versionNumber === versionNumber);
  if (Array.isArray(catalogTest.versions) && !versionSummary) {
    return catalogTest;
  }

  return {
    ...version,
    status: versionSummary?.status ?? version.status,
    publishedVersionNumber: catalogTest.publishedVersionNumber,
    hasDraft: catalogTest.hasDraft,
    versions: catalogTest.versions ?? version.versions ?? []
  };
}

export function TeacherTests({
  tests,
  models,
  selectedTest,
  selectedTestId,
  onSelectTest,
  onTestsChanged
}) {
  const { registerBeforeLogout } = useNavigationGuard();
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
  const areDraftControlsDisabled = hasMutationInProgress;

  const shouldBlockNavigation = useCallback(({ currentLocation, nextLocation }) => {
    const currentUrl = `${currentLocation.pathname}${currentLocation.search}${currentLocation.hash}`;
    const nextUrl = `${nextLocation.pathname}${nextLocation.search}${nextLocation.hash}`;
    return currentUrl !== nextUrl && hasUnsavedTeacherChanges;
  }, [hasUnsavedTeacherChanges]);
  const blocker = useBlocker(shouldBlockNavigation);
  const {
    state: blockerState,
    location: blockedLocation,
    proceed: proceedNavigation,
    reset: resetNavigation
  } = blocker;
  const confirmDiscardChanges = useCallback(() => (
    !hasUnsavedTeacherChanges || window.confirm(unsavedChangesPrompt)
  ), [hasUnsavedTeacherChanges]);

  useEffect(() => registerBeforeLogout(confirmDiscardChanges), [
    confirmDiscardChanges,
    registerBeforeLogout
  ]);

  useEffect(() => {
    if (blockerState !== "blocked") {
      return;
    }

    if (window.confirm(unsavedChangesPrompt)) {
      proceedNavigation();
    } else {
      resetNavigation();
    }
  }, [blockedLocation?.key, blockerState, proceedNavigation, resetNavigation]);

  useEffect(() => {
    if (!hasUnsavedTeacherChanges) {
      return;
    }

    function handleBeforeUnload(event) {
      event.preventDefault();
      event.returnValue = "";
    }

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [hasUnsavedTeacherChanges]);

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

  async function handleCreateTest(event) {
    event.preventDefault();

    if (hasMutationInProgress) {
      return;
    }

    const operation = beginOperation("createTest");
    setIsCreatingTest(true);
    setTestMessage("");

    try {
      const test = await createTestRequest(createTestPayload(testForm));
      if (isCurrentOperation("createTest", operation)) {
        const nextForm = createInitialTestForm(models[0]?.key ?? "");
        createTestBaselineRef.current = nextForm;
        setTestForm(nextForm);
        setTestMessage("Тест создан.");
      }
      await onTestsChanged(test.id);
    } catch (error) {
      if (isCurrentOperation("createTest", operation)) {
        setTestMessage(getApiErrorMessage(error, "Не удалось создать тест."));
      }
    } finally {
      if (isCurrentOperation("createTest", operation)) {
        setIsCreatingTest(false);
      }
    }
  }

  async function handleUpdateTest(event) {
    event.preventDefault();

    if (!activeTest
      || activeTest.status !== "draft"
      || isCreatingTest
      || isSavingTest
      || isVersionBusy
      || isCreatingTask
      || isSavingTask
      || Boolean(busyTaskId)) {
      return;
    }

    const testId = activeTest.id;
    const versionNumber = activeTest.versionNumber;
    const contentRevision = activeTest.contentRevision;
    const operation = beginOperation("saveTest", currentSelectedTestKey);
    setIsSavingTest(true);
    setTestEditMessage("");

    try {
      await updateTestRequest(
        testId,
        versionNumber,
        contentRevision,
        createTestPayload(testEditForm)
      );
      if (isCurrentOperation("saveTest", operation)) {
        testEditBaselineRef.current = testEditForm;
        setTestEditMessage("Тест сохранен.");
      }
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentOperation("saveTest", operation)) {
        setTestEditMessage(getApiErrorMessage(error, "Не удалось сохранить тест."));
      }
    } finally {
      if (isCurrentOperation("saveTest", operation)) {
        setIsSavingTest(false);
      }
    }
  }

  async function handleCreateTask(event) {
    event.preventDefault();

    if (!activeTest
      || activeTest.status !== "draft"
      || isCreatingTest
      || isCreatingTask
      || isVersionBusy
      || isSavingTest
      || isSavingTask
      || Boolean(busyTaskId)) {
      return;
    }

    const validationError = validateTaskForm(taskForm);
    if (validationError) {
      setTaskMessage(validationError);
      return;
    }

    const testId = activeTest.id;
    const versionNumber = activeTest.versionNumber;
    const contentRevision = activeTest.contentRevision;
    const operation = beginOperation("createTask", currentSelectedTestKey);
    setIsCreatingTask(true);
    setTaskMessage("");

    try {
      await createTaskRequest(
        testId,
        versionNumber,
        contentRevision,
        createTaskPayload(taskForm, Boolean(activeTest.llmModelKey))
      );
      if (isCurrentOperation("createTask", operation)) {
        const nextForm = createInitialTaskForm();
        taskCreateBaselineRef.current = nextForm;
        setTaskForm(nextForm);
        setTaskMessage("Задание добавлено.");
        setTaskPanelMode("list");
      }
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentOperation("createTask", operation)) {
        setTaskMessage(getApiErrorMessage(error, "Не удалось добавить задание."));
      }
    } finally {
      if (isCurrentOperation("createTask", operation)) {
        setIsCreatingTask(false);
      }
    }
  }

  async function handleUpdateTask(event) {
    event.preventDefault();

    if (!activeTest
      || activeTest.status !== "draft"
      || !editingTaskId
      || isCreatingTest
      || isSavingTask
      || isVersionBusy
      || isSavingTest
      || isCreatingTask
      || Boolean(busyTaskId)) {
      return;
    }

    const validationError = validateTaskForm(taskEditForm);
    if (validationError) {
      setTaskEditMessage(validationError);
      return;
    }

    const testId = activeTest.id;
    const versionNumber = activeTest.versionNumber;
    const contentRevision = activeTest.contentRevision;
    const taskId = editingTaskId;
    const operation = beginOperation("saveTask", currentSelectedTestKey);
    setIsSavingTask(true);
    setTaskEditMessage("");

    try {
      await updateTaskRequest(
        testId,
        versionNumber,
        contentRevision,
        taskId,
        createTaskPayload(taskEditForm, Boolean(activeTest.llmModelKey))
      );
      if (isCurrentOperation("saveTask", operation)) {
        taskEditBaselineRef.current = taskEditForm;
        setTaskEditMessage("Задание сохранено.");
      }
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentOperation("saveTask", operation)) {
        setTaskEditMessage(getApiErrorMessage(error, "Не удалось сохранить задание."));
      }
    } finally {
      if (isCurrentOperation("saveTask", operation)) {
        setIsSavingTask(false);
      }
    }
  }

  async function handleTaskVisibility(task, isHidden) {
    if (!activeTest
      || activeTest.status !== "draft"
      || isCreatingTest
      || isVersionBusy
      || isSavingTest
      || isCreatingTask
      || isSavingTask
      || Boolean(busyTaskId)) {
      return;
    }

    const testId = activeTest.id;
    const versionNumber = activeTest.versionNumber;
    const contentRevision = activeTest.contentRevision;
    const operation = beginOperation("taskMutation", currentSelectedTestKey);
    setBusyTaskId(task.id);
    setTaskMessage("");

    try {
      await setTaskVisibilityRequest(
        testId,
        versionNumber,
        contentRevision,
        task.id,
        isHidden
      );
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentOperation("taskMutation", operation)) {
        setTaskMessage(getApiErrorMessage(
          error,
          "Не удалось изменить видимость задания."
        ));
      }
    } finally {
      if (isCurrentOperation("taskMutation", operation)) {
        setBusyTaskId("");
      }
    }
  }

  async function handleDeleteTask(task) {
    if (!activeTest
      || activeTest.status !== "draft"
      || !task
      || isCreatingTest
      || isVersionBusy
      || isSavingTest
      || isCreatingTask
      || isSavingTask
      || Boolean(busyTaskId)) {
      return;
    }

    const testId = activeTest.id;
    const versionNumber = activeTest.versionNumber;
    const contentRevision = activeTest.contentRevision;
    const operation = beginOperation("taskMutation", currentSelectedTestKey);
    setBusyTaskId(task.id);
    setTaskMessage("");

    try {
      await deleteTaskRequest(testId, versionNumber, contentRevision, task.id);

      if (isCurrentOperation("taskMutation", operation)) {
        if (editingTaskId === task.id) {
          const nextForm = createInitialTaskForm();
          taskEditBaselineRef.current = nextForm;
          setEditingTaskId("");
          setTaskEditForm(nextForm);
          setTaskPanelMode("list");
        }

        setDeleteTaskCandidate(null);
      }
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentOperation("taskMutation", operation)) {
        setDeleteTaskCandidate(null);
        setTaskMessage(getApiErrorMessage(error, "Не удалось удалить задание."));
      }
    } finally {
      if (isCurrentOperation("taskMutation", operation)) {
        setBusyTaskId("");
      }
    }
  }

  async function handleSelectVersion(versionNumber) {
    if (!activeTest
      || isVersionBusy
      || hasNonVersionMutationInProgress
      || versionNumber === activeTest.versionNumber) {
      return;
    }

    if (hasUnsavedDraftChanges || taskPanelMode !== "list") {
      setVersionMessage("Сначала сохраните изменения теста и заданий или отмените редактирование.");
      return;
    }

    const testId = activeTest.id;
    const operation = {
      ...beginOperation("loadVersion"),
      logicalTestId: testId
    };
    setIsVersionBusy(true);
    setVersionMessage("");

    try {
      const version = await getTestVersion(testId, versionNumber);
      if (isCurrentLogicalOperation("loadVersion", operation)) {
        setViewedTest(mergeCatalogVersionMetadata(version, catalogTestRef.current));
      }
    } catch (error) {
      if (isCurrentLogicalOperation("loadVersion", operation)) {
        setVersionMessage(getApiErrorMessage(
          error,
          "Не удалось загрузить выбранную версию."
        ));
      }
    } finally {
      if (isCurrentLogicalOperation("loadVersion", operation)) {
        setIsVersionBusy(false);
      }
    }
  }

  async function handleCreateVersion() {
    if (!activeTest
      || activeTest.status !== "published"
      || activeTest.hasDraft
      || isVersionBusy
      || hasNonVersionMutationInProgress) {
      return;
    }

    const testId = activeTest.id;
    const expectedPublishedVersionNumber = activeTest.versionNumber;
    const operation = {
      ...beginOperation("createVersion"),
      logicalTestId: testId
    };
    setIsVersionBusy(true);
    setVersionMessage("");

    try {
      const draft = await createDraftVersion(testId, expectedPublishedVersionNumber);
      if (isCurrentLogicalOperation("createVersion", operation)) {
        setViewedTest(draft);
        setVersionMessage(`Создан черновик версии ${draft.versionNumber}.`);
      }
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentLogicalOperation("createVersion", operation)) {
        setVersionMessage(getApiErrorMessage(
          error,
          error?.status === 409
            ? "У теста уже есть черновик."
            : "Не удалось создать новую версию."
        ));
      }
    } finally {
      if (isCurrentLogicalOperation("createVersion", operation)) {
        setIsVersionBusy(false);
      }
    }
  }

  async function handlePublishVersion() {
    if (!activeTest
      || activeTest.status !== "draft"
      || isVersionBusy
      || hasNonVersionMutationInProgress) {
      return true;
    }

    if (hasUnsavedDraftChanges || taskPanelMode !== "list") {
      setVersionMessage("Сначала сохраните изменения теста и заданий или отмените редактирование.");
      return true;
    }

    const testId = activeTest.id;
    const versionNumber = activeTest.versionNumber;
    const contentRevision = activeTest.contentRevision;
    const operation = {
      ...beginOperation("publishVersion"),
      logicalTestId: testId
    };
    setIsVersionBusy(true);
    setVersionMessage("");

    try {
      const published = await publishTestVersion(testId, versionNumber, contentRevision);
      if (isCurrentLogicalOperation("publishVersion", operation)) {
        setViewedTest(published);
        setVersionMessage(`Версия ${versionNumber} опубликована.`);
      }
      await onTestsChanged(testId);
      return true;
    } catch (error) {
      if (isCurrentLogicalOperation("publishVersion", operation)) {
        setVersionMessage(getApiErrorMessage(
          error,
          error?.status === 409
            ? "Версия уже опубликована или больше не является черновиком."
            : "Не удалось опубликовать версию."
        ));
      }
      return true;
    } finally {
      if (isCurrentLogicalOperation("publishVersion", operation)) {
        setIsVersionBusy(false);
      }
    }
  }

  async function handleDeleteVersion() {
    if (!activeTest
      || activeTest.status !== "draft"
      || isVersionBusy
      || hasNonVersionMutationInProgress) {
      return true;
    }

    if (hasUnsavedDraftChanges || taskPanelMode !== "list") {
      setVersionMessage("Сначала сохраните изменения теста и заданий или отмените редактирование.");
      return true;
    }

    const testId = activeTest.id;
    const versionNumber = activeTest.versionNumber;
    const contentRevision = activeTest.contentRevision;
    const publishedVersionNumber = activeTest.publishedVersionNumber;
    const operation = {
      ...beginOperation("deleteVersion"),
      logicalTestId: testId
    };
    setIsVersionBusy(true);
    setVersionMessage("");

    try {
      await deleteDraftVersion(testId, versionNumber, contentRevision);
    } catch (error) {
      if (isCurrentLogicalOperation("deleteVersion", operation)) {
        setVersionMessage(getApiErrorMessage(
          error,
          error?.status === 409
            ? "Удалить можно только черновик."
            : "Не удалось удалить черновик."
        ));
        setIsVersionBusy(false);
      }

      return true;
    }

    let fallbackVersion = null;
    if (publishedVersionNumber) {
      try {
        fallbackVersion = await getTestVersion(testId, publishedVersionNumber);
      } catch {
        // The DELETE already succeeded. The overview refresh below is the second
        // recovery path; this failure must not be reported as a failed deletion.
      }
    }

    if (fallbackVersion && isCurrentLogicalOperation("deleteVersion", operation)) {
      setViewedTest(fallbackVersion);
    }

    let refreshed;
    try {
      refreshed = await onTestsChanged(fallbackVersion ? testId : null);
    } catch {
      refreshed = false;
    }

    if (isCurrentLogicalTestOperation("deleteVersion", operation)) {
      setVersionMessage(refreshed
        ? `Черновик версии ${versionNumber} удалён.`
        : "Черновик удалён, но список версий не обновился. Обновите страницу.");
      setIsVersionBusy(false);
    }

    return true;
  }

  function startTaskEdit(task) {
    if (hasMutationInProgress) {
      return;
    }

    if (editingTaskId === task.id) {
      setTaskPanelMode("edit");
      return;
    }

    if (editingTaskId
      && hasUnsavedTaskEditChanges
      && !window.confirm("Изменения текущего задания не сохранены. Открыть другое задание?")) {
      return;
    }

    const nextForm = createTaskFormFromTask(task);
    taskEditBaselineRef.current = nextForm;
    setEditingTaskId(task.id);
    setTaskEditForm(nextForm);
    setTaskEditMessage("");
    setTaskPanelMode("edit");
  }

  function handleTaskPanelModeChange(nextMode) {
    if (nextMode === taskPanelMode || hasMutationInProgress) {
      return;
    }

    if (taskPanelMode === "create" && hasUnsavedTaskCreateChanges) {
      const shouldDiscard = window.confirm(
        "Новое задание не сохранено. Отменить создание и потерять изменения?"
      );
      if (!shouldDiscard) {
        return;
      }
    }

    if (taskPanelMode === "edit" && hasUnsavedTaskEditChanges) {
      const shouldDiscard = window.confirm(
        "Изменения задания не сохранены. Отменить редактирование?"
      );
      if (!shouldDiscard) {
        return;
      }
    }

    if (taskPanelMode === "create") {
      const nextForm = createInitialTaskForm();
      taskCreateBaselineRef.current = nextForm;
      setTaskForm(nextForm);
      setTaskMessage("");
    }

    if (taskPanelMode === "edit") {
      const nextForm = createInitialTaskForm();
      taskEditBaselineRef.current = nextForm;
      setTaskEditForm(nextForm);
      setTaskEditMessage("");
      setEditingTaskId("");
    }

    setTaskPanelMode(nextMode);
  }

  return (
    <section className="tests-layout teacher-page teacher-tests-page">
      <TestList
        form={testForm}
        hasUnsavedChanges={hasUnsavedCreateTestChanges}
        isBusy={hasMutationInProgress}
        isCreating={isCreatingTest}
        message={testMessage}
        models={models}
        onCreate={handleCreateTest}
        onFormChange={setTestForm}
        onSelectTest={onSelectTest}
        selectedTestId={selectedTestId}
        tests={tests}
      />

      <TestDetails
        areDraftControlsDisabled={areDraftControlsDisabled}
        busyTaskId={busyTaskId}
        deleteTaskCandidate={deleteTaskCandidate}
        editingTaskId={editingTaskId}
        isCreatingTask={isCreatingTask}
        isSavingTask={isSavingTask}
        isSavingTest={isSavingTest}
        isVersionBusy={hasMutationInProgress}
        models={models}
        onCreateTask={handleCreateTask}
        onCreateVersion={handleCreateVersion}
        onDeleteVersion={handleDeleteVersion}
        onDeleteTask={handleDeleteTask}
        onEditTask={startTaskEdit}
        onRequestDeleteTask={setDeleteTaskCandidate}
        onTaskCreateFormChange={setTaskForm}
        onTaskEditFormChange={setTaskEditForm}
        onTaskPanelModeChange={handleTaskPanelModeChange}
        onTestFormChange={setTestEditForm}
        onToggleTaskVisibility={handleTaskVisibility}
        onPublishVersion={handlePublishVersion}
        onSelectVersion={handleSelectVersion}
        onUpdateTask={handleUpdateTask}
        onUpdateTest={handleUpdateTest}
        selectedTest={activeTest}
        taskCreateForm={taskForm}
        taskCreateMessage={taskMessage}
        taskEditForm={taskEditForm}
        taskEditMessage={taskEditMessage}
        taskPanelMode={taskPanelMode}
        testEditForm={testEditForm}
        testEditMessage={testEditMessage}
        versionMessage={versionMessage}
      />
    </section>
  );
}

export default TeacherTests;
