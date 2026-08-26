import {
  createDraftVersion,
  deleteDraftVersion,
  getTestVersion,
  publishTestVersion
} from "../../api/teachingApi.js";
import {
  getApiErrorMessage,
  mergeCatalogVersionMetadata
} from "./teacherTestControllerHelpers.js";

const unsavedDraftMessage = "Сначала сохраните изменения теста и заданий или отмените редактирование.";

export function createTeacherVersionActions({
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
}) {
  async function handleSelectVersion(versionNumber) {
    if (!activeTest
      || isVersionBusy
      || hasNonVersionMutationInProgress
      || versionNumber === activeTest.versionNumber) {
      return;
    }

    if (hasUnsavedDraftChanges || taskPanelMode !== "list") {
      setVersionMessage(unsavedDraftMessage);
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
      setVersionMessage(unsavedDraftMessage);
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
      setVersionMessage(unsavedDraftMessage);
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

  return {
    handleCreateVersion,
    handleDeleteVersion,
    handlePublishVersion,
    handleSelectVersion
  };
}
