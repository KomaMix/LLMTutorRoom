import { FileText } from "lucide-react";
import { formatDate } from "../../shared/lib/dates.js";
import { ConfirmDialog } from "../../shared/ui/ConfirmDialog.jsx";
import { InfoTile } from "../../shared/ui/InfoTile.jsx";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";
import { TaskPanel } from "./TaskPanel.jsx";
import { TestForm } from "./TestForm.jsx";
import { TestVersionPanel } from "./TestVersionPanel.jsx";
import { createTaskCountSummary } from "./teacherTestHelpers.js";

export function TestDetails({
  busyTaskId,
  deleteTaskCandidate,
  editingTaskId,
  areDraftControlsDisabled,
  isCreatingTask,
  isSavingTask,
  isSavingTest,
  isVersionBusy,
  models,
  onCreateTask,
  onCreateVersion,
  onDeleteVersion,
  onDeleteTask,
  onEditTask,
  onRequestDeleteTask,
  onTaskCreateFormChange,
  onTaskEditFormChange,
  onTaskPanelModeChange,
  onTestFormChange,
  onToggleTaskVisibility,
  onPublishVersion,
  onSelectVersion,
  onUpdateTask,
  onUpdateTest,
  selectedTest,
  taskCreateForm,
  taskCreateMessage,
  taskEditForm,
  taskEditMessage,
  taskPanelMode,
  testEditForm,
  testEditMessage,
  versionMessage
}) {
  if (!selectedTest) {
    return (
      <div className="detail-panel">
        <section className="empty-state">
          <FileText size={28} aria-hidden="true" />
          <h2>Создай первый тест</h2>
        </section>
      </div>
    );
  }

  const isDraft = selectedTest.status === "draft";

  return (
    <div className="detail-panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">{selectedTest.subject}</span>
          <h2>{selectedTest.title}</h2>
        </div>
        <StatusBadge status={selectedTest.status} />
      </div>

      <p className="muted">{selectedTest.summary || "Описание пока не добавлено."}</p>

      <TestVersionPanel
        isBusy={isVersionBusy}
        message={versionMessage}
        onCreateDraft={onCreateVersion}
        onDeleteDraft={onDeleteVersion}
        onPublish={onPublishVersion}
        onSelectVersion={onSelectVersion}
        test={selectedTest}
      />

      <div className="compact-grid test-meta-grid">
        <InfoTile label="Время" value={`${selectedTest.timeLimitMinutes} мин`} />
        <InfoTile label="Задания" value={createTaskCountSummary(selectedTest)} />
        <InfoTile label="Баллы" value={selectedTest.totalPoints} />
        <InfoTile label="Дедлайн" value={formatDate(selectedTest.deadline)} />
        <InfoTile label="LLM" value={selectedTest.llmModelKey || "не выбрана"} />
      </div>

      {isDraft && (
        <TestForm
          form={testEditForm}
          isDisabled={areDraftControlsDisabled}
          isSubmitting={isSavingTest}
          message={testEditMessage}
          mode="edit"
          models={models}
          onChange={onTestFormChange}
          onSubmit={onUpdateTest}
        />
      )}

      <TaskPanel
        busyTaskId={busyTaskId}
        createForm={taskCreateForm}
        createMessage={taskCreateMessage}
        editForm={taskEditForm}
        editMessage={taskEditMessage}
        editingTaskId={editingTaskId}
        hasLlmModel={Boolean(selectedTest.llmModelKey)}
        isDisabled={areDraftControlsDisabled}
        isCreating={isCreatingTask}
        isSaving={isSavingTask}
        mode={taskPanelMode}
        onCreate={onCreateTask}
        onCreateFormChange={onTaskCreateFormChange}
        onDelete={onRequestDeleteTask}
        onEdit={onEditTask}
        onEditFormChange={onTaskEditFormChange}
        onModeChange={onTaskPanelModeChange}
        onToggleVisibility={onToggleTaskVisibility}
        onUpdate={onUpdateTask}
        readOnly={!isDraft}
        tasks={selectedTest.tasks}
      />

      {deleteTaskCandidate && (
        <ConfirmDialog
          title="Удалить задание?"
          description={`Задание "${deleteTaskCandidate.title}" будет полностью удалено из теста. Если нужно временно убрать его из выдачи ученикам, лучше использовать скрытие.`}
          confirmLabel="Удалить"
          isBusy={busyTaskId === deleteTaskCandidate.id}
          onCancel={() => onRequestDeleteTask(null)}
          onConfirm={() => onDeleteTask(deleteTaskCandidate)}
        />
      )}
    </div>
  );
}

export default TestDetails;
