import {
  CheckCircle2,
  Eye,
  EyeOff,
  FileText,
  Pencil,
  Plus,
  Trash2
} from "lucide-react";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";

export function TaskList({
  tasks,
  busyTaskId,
  onCreate,
  onDelete,
  onEdit,
  onToggleVisibility,
  disabled = false,
  readOnly = false
}) {
  const isTaskMutationBusy = disabled || Boolean(busyTaskId);

  if (tasks.length === 0) {
    return (
      <section className="empty-state compact-empty-state">
        <FileText size={24} aria-hidden="true" />
        <h2>Заданий пока нет</h2>
        {!readOnly && (
          <button
            type="button"
            className="button primary"
            disabled={isTaskMutationBusy}
            onClick={onCreate}
          >
            <Plus size={16} aria-hidden="true" />
            Добавить задание
          </button>
        )}
      </section>
    );
  }

  return (
    <div className="task-list">
      {tasks.map((task, index) => (
        <article className={task.isHidden ? "task-card hidden-task" : "task-card"} key={task.id}>
          <div className="task-card-header">
            <div className="task-card-title">
              <strong>{index + 1}. {task.title}</strong>
              <span>{task.maxPoints} баллов</span>
            </div>
            {!readOnly && (
              <div className="task-actions">
                <button
                  type="button"
                  className="icon-button"
                  title="Редактировать"
                  disabled={isTaskMutationBusy}
                  onClick={() => onEdit(task)}
                >
                  <Pencil size={16} aria-hidden="true" />
                </button>
                <button
                  type="button"
                  className="icon-button"
                  title={task.isHidden ? "Показать задание" : "Скрыть задание"}
                  disabled={isTaskMutationBusy}
                  onClick={() => onToggleVisibility(task, !task.isHidden)}
                >
                  {task.isHidden
                    ? <Eye size={16} aria-hidden="true" />
                    : <EyeOff size={16} aria-hidden="true" />}
                </button>
                <button
                  type="button"
                  className="icon-button danger"
                  title="Удалить задание"
                  disabled={isTaskMutationBusy}
                  onClick={() => onDelete(task)}
                >
                  <Trash2 size={16} aria-hidden="true" />
                </button>
              </div>
            )}
          </div>
          <div className="task-subline">
            <StatusBadge status={task.type} />
            {task.isHidden && <StatusBadge status="hidden" />}
            {task.type === "multiple-choice" && task.wrongAnswerPenalty > 0 && (
              <span className="task-penalty">Штраф: {task.wrongAnswerPenalty}</span>
            )}
          </div>
          <p>{task.prompt}</p>
          {task.options.length > 0 && (
            <div className="answer-option-list">
              {task.options.map(option => (
                <span
                  className={task.correctOptionIds.includes(option.id) ? "correct" : ""}
                  key={option.id}
                >
                  {task.correctOptionIds.includes(option.id) && <CheckCircle2 size={14} aria-hidden="true" />}
                  {option.text}
                </span>
              ))}
            </div>
          )}
        </article>
      ))}
    </div>
  );
}

export default TaskList;
