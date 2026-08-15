import { useEffect, useRef, useState } from "react";
import {
  createTask as createTaskRequest,
  createTest as createTestRequest,
  deleteTask as deleteTaskRequest,
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

export function TeacherTests({
  tests,
  models,
  selectedTest,
  selectedTestId,
  onSelectTest,
  onTestsChanged
}) {
  const currentSelectedTestId = selectedTest?.id ?? "";
  const selectedTestRef = useRef(selectedTest);
  const currentSelectedTestIdRef = useRef(currentSelectedTestId);
  const selectionGeneration = useRef(0);
  const operationGenerations = useRef({});
  const hasInitializedDefaultModel = useRef(models.length > 0);
  selectedTestRef.current = selectedTest;
  currentSelectedTestIdRef.current = currentSelectedTestId;

  const [testForm, setTestForm] = useState(() =>
    createInitialTestForm(models[0]?.key ?? ""));
  const [testEditForm, setTestEditForm] = useState(() => createTestFormFromTest(selectedTest));
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
  const [testMessage, setTestMessage] = useState("");
  const [testEditMessage, setTestEditMessage] = useState("");
  const [taskMessage, setTaskMessage] = useState("");
  const [taskEditMessage, setTaskEditMessage] = useState("");

  function beginOperation(name, testId = null) {
    const generation = (operationGenerations.current[name] ?? 0) + 1;
    operationGenerations.current[name] = generation;

    return {
      generation,
      selectionGeneration: selectionGeneration.current,
      testId
    };
  }

  function isCurrentOperation(name, operation) {
    if (operationGenerations.current[name] !== operation.generation) {
      return false;
    }

    if (operation.testId === null) {
      return true;
    }

    return selectionGeneration.current === operation.selectionGeneration
      && currentSelectedTestIdRef.current === operation.testId;
  }

  useEffect(() => {
    selectionGeneration.current += 1;
    setTestEditForm(createTestFormFromTest(selectedTestRef.current));
    setTaskForm(createInitialTaskForm());
    setTaskEditForm(createInitialTaskForm());
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
  }, [currentSelectedTestId]);

  useEffect(() => {
    if (hasInitializedDefaultModel.current || models.length === 0) {
      return;
    }

    hasInitializedDefaultModel.current = true;
    setTestForm(current => current.llmModelKey
      ? current
      : { ...current, llmModelKey: models[0].key });
  }, [models]);

  async function handleCreateTest(event) {
    event.preventDefault();
    const operation = beginOperation("createTest");
    setIsCreatingTest(true);
    setTestMessage("");

    try {
      const test = await createTestRequest(createTestPayload(testForm));
      if (isCurrentOperation("createTest", operation)) {
        setTestForm(createInitialTestForm(models[0]?.key ?? ""));
        setTestMessage("Тест создан.");
      }
      await onTestsChanged(test.id);
    } catch (error) {
      if (isCurrentOperation("createTest", operation)) {
        setTestMessage("Не удалось создать тест.");
      }
    } finally {
      if (isCurrentOperation("createTest", operation)) {
        setIsCreatingTest(false);
      }
    }
  }

  async function handleUpdateTest(event) {
    event.preventDefault();

    if (!selectedTest) {
      return;
    }

    const testId = selectedTest.id;
    const operation = beginOperation("saveTest", testId);
    setIsSavingTest(true);
    setTestEditMessage("");

    try {
      await updateTestRequest(testId, createTestPayload(testEditForm));
      if (isCurrentOperation("saveTest", operation)) {
        setTestEditMessage("Тест сохранен.");
      }
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentOperation("saveTest", operation)) {
        setTestEditMessage("Не удалось сохранить тест.");
      }
    } finally {
      if (isCurrentOperation("saveTest", operation)) {
        setIsSavingTest(false);
      }
    }
  }

  async function handleCreateTask(event) {
    event.preventDefault();

    if (!selectedTest) {
      return;
    }

    const validationError = validateTaskForm(taskForm);
    if (validationError) {
      setTaskMessage(validationError);
      return;
    }

    const testId = selectedTest.id;
    const operation = beginOperation("createTask", testId);
    setIsCreatingTask(true);
    setTaskMessage("");

    try {
      await createTaskRequest(testId, createTaskPayload(taskForm, Boolean(selectedTest.llmModelKey)));
      if (isCurrentOperation("createTask", operation)) {
        setTaskForm(createInitialTaskForm());
        setTaskMessage("Задание добавлено.");
        setTaskPanelMode("list");
      }
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentOperation("createTask", operation)) {
        setTaskMessage("Не удалось добавить задание.");
      }
    } finally {
      if (isCurrentOperation("createTask", operation)) {
        setIsCreatingTask(false);
      }
    }
  }

  async function handleUpdateTask(event) {
    event.preventDefault();

    if (!selectedTest || !editingTaskId) {
      return;
    }

    const validationError = validateTaskForm(taskEditForm);
    if (validationError) {
      setTaskEditMessage(validationError);
      return;
    }

    const testId = selectedTest.id;
    const taskId = editingTaskId;
    const operation = beginOperation("saveTask", testId);
    setIsSavingTask(true);
    setTaskEditMessage("");

    try {
      await updateTaskRequest(
        testId,
        taskId,
        createTaskPayload(taskEditForm, Boolean(selectedTest.llmModelKey))
      );
      if (isCurrentOperation("saveTask", operation)) {
        setTaskEditMessage("Задание сохранено.");
      }
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentOperation("saveTask", operation)) {
        setTaskEditMessage("Не удалось сохранить задание.");
      }
    } finally {
      if (isCurrentOperation("saveTask", operation)) {
        setIsSavingTask(false);
      }
    }
  }

  async function handleTaskVisibility(task, isHidden) {
    if (!selectedTest) {
      return;
    }

    const testId = selectedTest.id;
    const operation = beginOperation("taskMutation", testId);
    setBusyTaskId(task.id);
    setTaskMessage("");

    try {
      await setTaskVisibilityRequest(testId, task.id, isHidden);
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentOperation("taskMutation", operation)) {
        setTaskMessage("Не удалось изменить видимость задания.");
      }
    } finally {
      if (isCurrentOperation("taskMutation", operation)) {
        setBusyTaskId("");
      }
    }
  }

  async function handleDeleteTask(task) {
    if (!selectedTest || !task) {
      return;
    }

    const testId = selectedTest.id;
    const operation = beginOperation("taskMutation", testId);
    setBusyTaskId(task.id);
    setTaskMessage("");

    try {
      await deleteTaskRequest(testId, task.id);

      if (isCurrentOperation("taskMutation", operation)) {
        if (editingTaskId === task.id) {
          setEditingTaskId("");
          setTaskEditForm(createInitialTaskForm());
          setTaskPanelMode("list");
        }

        setDeleteTaskCandidate(null);
      }
      await onTestsChanged(testId);
    } catch (error) {
      if (isCurrentOperation("taskMutation", operation)) {
        setDeleteTaskCandidate(null);
        setTaskMessage("Не удалось удалить задание.");
      }
    } finally {
      if (isCurrentOperation("taskMutation", operation)) {
        setBusyTaskId("");
      }
    }
  }

  function startTaskEdit(task) {
    setEditingTaskId(task.id);
    setTaskEditForm(createTaskFormFromTask(task));
    setTaskEditMessage("");
    setTaskPanelMode("edit");
  }

  return (
    <section className="tests-layout">
      <TestList
        form={testForm}
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
        busyTaskId={busyTaskId}
        deleteTaskCandidate={deleteTaskCandidate}
        editingTaskId={editingTaskId}
        isCreatingTask={isCreatingTask}
        isSavingTask={isSavingTask}
        isSavingTest={isSavingTest}
        models={models}
        onCreateTask={handleCreateTask}
        onDeleteTask={handleDeleteTask}
        onEditTask={startTaskEdit}
        onRequestDeleteTask={setDeleteTaskCandidate}
        onTaskCreateFormChange={setTaskForm}
        onTaskEditFormChange={setTaskEditForm}
        onTaskPanelModeChange={setTaskPanelMode}
        onTestFormChange={setTestEditForm}
        onToggleTaskVisibility={handleTaskVisibility}
        onUpdateTask={handleUpdateTask}
        onUpdateTest={handleUpdateTest}
        selectedTest={selectedTest}
        taskCreateForm={taskForm}
        taskCreateMessage={taskMessage}
        taskEditForm={taskEditForm}
        taskEditMessage={taskEditMessage}
        taskPanelMode={taskPanelMode}
        testEditForm={testEditForm}
        testEditMessage={testEditMessage}
      />
    </section>
  );
}

export default TeacherTests;
