import { ListChecks, Plus } from "lucide-react";
import { TaskEditorForm } from "./TaskEditorForm.jsx";
import { TaskList } from "./TaskList.jsx";

export function TaskPanel({
  busyTaskId,
  createForm,
  createMessage,
  editForm,
  editMessage,
  editingTaskId,
  hasLlmModel,
  isDisabled = false,
  isCreating,
  isSaving,
  mode,
  onCreate,
  onCreateFormChange,
  onDelete,
  onEdit,
  onEditFormChange,
  onModeChange,
  onToggleVisibility,
  onUpdate,
  readOnly = false,
  tasks
}) {
  const isEditorSubmitting = (mode === "create" && isCreating)
    || (mode === "edit" && isSaving);
  const controlsDisabled = isDisabled || isEditorSubmitting;

  return (
    <div className="panel-section">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Содержание</span>
          <h3>Задания</h3>
        </div>
        <div className="task-panel-actions" aria-label="Режим работы с заданиями">
          <button
            type="button"
            className={mode === "list" ? "active" : ""}
            disabled={controlsDisabled}
            onClick={() => onModeChange("list")}
          >
            <ListChecks size={16} aria-hidden="true" />
            Список
          </button>
          {!readOnly && (
            <button
              type="button"
              className={mode === "create" ? "active" : ""}
              disabled={controlsDisabled}
              onClick={() => onModeChange("create")}
            >
              <Plus size={16} aria-hidden="true" />
              Новое
            </button>
          )}
        </div>
      </div>

      {mode === "list" && (
        <div role="status" aria-atomic="true" aria-live="polite">
          {createMessage && <p className="form-note">{createMessage}</p>}
        </div>
      )}

      {mode === "list" && (
        <TaskList
          tasks={tasks}
          busyTaskId={busyTaskId}
          disabled={isDisabled}
          onCreate={() => onModeChange("create")}
          onDelete={onDelete}
          onEdit={onEdit}
          onToggleVisibility={onToggleVisibility}
          readOnly={readOnly}
        />
      )}

      {!readOnly && mode === "create" && (
        <TaskEditorForm
          key="new-task"
          form={createForm}
          formId="new-task"
          title="Новое задание"
          submitLabel="Добавить задание"
          submittingLabel="Добавление..."
          message={createMessage}
          isDisabled={isDisabled}
          isSubmitting={isCreating}
          hasLlmModel={hasLlmModel}
          onChange={onCreateFormChange}
          onSubmit={onCreate}
        />
      )}

      {!readOnly && mode === "edit" && (
        <div className="task-edit-panel">
          <TaskEditorForm
            key={editingTaskId}
            form={editForm}
            formId={`edit-task-${editingTaskId}`}
            title="Редактирование задания"
            submitLabel="Сохранить задание"
            submittingLabel="Сохранение..."
            message={editMessage}
            isDisabled={isDisabled}
            isSubmitting={isSaving}
            hasLlmModel={hasLlmModel}
            onChange={onEditFormChange}
            onSubmit={onUpdate}
          />
          <button
            type="button"
            className="button secondary"
            disabled={isDisabled || isSaving}
            onClick={() => onModeChange("list")}
          >
            Вернуться к списку
          </button>
        </div>
      )}
    </div>
  );
}

export default TaskPanel;
