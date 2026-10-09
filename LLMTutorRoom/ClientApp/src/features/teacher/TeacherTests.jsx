import { TestDetails } from "./TestDetails.jsx";
import { TestList } from "./TestList.jsx";
import { useTeacherTestsController } from "./useTeacherTestsController.js";

export function TeacherTests({
  feedbackTemplates = [],
  tests,
  models,
  selectedTest,
  selectedTestId,
  onSelectTest,
  onTestsChanged
}) {
  const controller = useTeacherTestsController({
    feedbackTemplates,
    models,
    onTestsChanged,
    selectedTest
  });

  return (
    <section className="tests-layout teacher-page teacher-tests-page">
      <TestList
        form={controller.testForm}
        hasUnsavedChanges={controller.hasUnsavedCreateTestChanges}
        isBusy={controller.hasMutationInProgress}
        isCreating={controller.isCreatingTest}
        message={controller.testMessage}
        models={models}
        feedbackTemplates={feedbackTemplates}
        onCreate={controller.handleCreateTest}
        onFormChange={controller.setTestForm}
        onSelectTest={onSelectTest}
        selectedTestId={selectedTestId}
        tests={tests}
      />

      <TestDetails
        areDraftControlsDisabled={controller.areDraftControlsDisabled}
        busyTaskId={controller.busyTaskId}
        deleteTaskCandidate={controller.deleteTaskCandidate}
        editingTaskId={controller.editingTaskId}
        isCreatingTask={controller.isCreatingTask}
        isSavingTask={controller.isSavingTask}
        isSavingTest={controller.isSavingTest}
        isVersionBusy={controller.hasMutationInProgress}
        models={models}
        feedbackTemplates={feedbackTemplates}
        onCreateTask={controller.handleCreateTask}
        onCreateVersion={controller.handleCreateVersion}
        onDeleteVersion={controller.handleDeleteVersion}
        onDeleteTask={controller.handleDeleteTask}
        onEditTask={controller.startTaskEdit}
        onRequestDeleteTask={controller.setDeleteTaskCandidate}
        onTaskCreateFormChange={controller.setTaskForm}
        onTaskEditFormChange={controller.setTaskEditForm}
        onTaskPanelModeChange={controller.handleTaskPanelModeChange}
        onTestFormChange={controller.setTestEditForm}
        onToggleTaskVisibility={controller.handleTaskVisibility}
        onPublishVersion={controller.handlePublishVersion}
        onSelectVersion={controller.handleSelectVersion}
        onUpdateTask={controller.handleUpdateTask}
        onUpdateTest={controller.handleUpdateTest}
        selectedTest={controller.activeTest}
        taskCreateForm={controller.taskForm}
        taskCreateMessage={controller.taskMessage}
        taskEditForm={controller.taskEditForm}
        taskEditMessage={controller.taskEditMessage}
        taskPanelMode={controller.taskPanelMode}
        testEditForm={controller.testEditForm}
        testEditMessage={controller.testEditMessage}
        versionMessage={controller.versionMessage}
      />
    </section>
  );
}

export default TeacherTests;
