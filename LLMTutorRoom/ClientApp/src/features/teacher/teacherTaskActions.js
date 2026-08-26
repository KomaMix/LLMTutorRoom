import {
  createTask as createTaskRequest,
  deleteTask as deleteTaskRequest,
  setTaskVisibility as setTaskVisibilityRequest,
  updateTask as updateTaskRequest
} from "../../api/teachingApi.js";
import {
  createInitialTaskForm,
  createTaskFormFromTask,
  createTaskPayload,
  validateTaskForm
} from "./teacherTestHelpers.js";
import { getApiErrorMessage } from "./teacherTestControllerHelpers.js";

export function createTeacherTaskActions({
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
}) {
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

  return {
    handleCreateTask,
    handleDeleteTask,
    handleTaskPanelModeChange,
    handleTaskVisibility,
    handleUpdateTask,
    startTaskEdit
  };
}
