import {
  createTest as createTestRequest,
  updateTest as updateTestRequest
} from "../../api/teachingApi.js";
import {
  createInitialTestForm,
  createTestPayload
} from "./teacherTestHelpers.js";
import { getApiErrorMessage } from "./teacherTestControllerHelpers.js";

export function createTeacherTestActions({
  activeTest,
  beginOperation,
  createTestBaselineRef,
  currentSelectedTestKey,
  hasMutationInProgress,
  busyTaskId,
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
}) {
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

  return {
    handleCreateTest,
    handleUpdateTest
  };
}
