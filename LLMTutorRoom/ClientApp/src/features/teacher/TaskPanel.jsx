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
  tasks
}) {
  const isEditorSubmitting = (mode === "create" && isCreating)
    || (mode === "edit" && isSaving);

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
            disabled={isEditorSubmitting}
            onClick={() => onModeChange("list")}
          >
            <ListChecks size={16} aria-hidden="true" />
            Список
          </button>
          <button
            type="button"
            className={mode === "create" ? "active" : ""}
            disabled={isEditorSubmitting}
            onClick={() => onModeChange("create")}
          >
            <Plus size={16} aria-hidden="true" />
            Новое
          </button>
        </div>
      </div>

      {mode === "list" && createMessage && <p className="form-note">{createMessage}</p>}

      {mode === "list" && (
        <TaskList
          tasks={tasks}
          busyTaskId={busyTaskId}
          onCreate={() => onModeChange("create")}
          onDelete={onDelete}
          onEdit={onEdit}
          onToggleVisibility={onToggleVisibility}
        />
      )}

      {mode === "create" && (
        <TaskEditorForm
          key="new-task"
          form={createForm}
          formId="new-task"
          title="Новое задание"
          submitLabel="Добавить задание"
          submittingLabel="Добавление..."
          message={createMessage}
          isSubmitting={isCreating}
          hasLlmModel={hasLlmModel}
          onChange={onCreateFormChange}
          onSubmit={onCreate}
        />
      )}

      {mode === "edit" && (
        <div className="task-edit-panel">
          <TaskEditorForm
            key={editingTaskId}
            form={editForm}
            formId={`edit-task-${editingTaskId}`}
            title="Редактирование задания"
            submitLabel="Сохранить задание"
            submittingLabel="Сохранение..."
            message={editMessage}
            isSubmitting={isSaving}
            hasLlmModel={hasLlmModel}
            onChange={onEditFormChange}
            onSubmit={onUpdate}
          />
          <button
            type="button"
            className="button secondary"
            disabled={isSaving}
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
