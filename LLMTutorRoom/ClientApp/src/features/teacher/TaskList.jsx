import {
  Bot,
  ChevronDown,
  Eye,
  EyeOff,
  FileText,
  Pencil,
  Plus,
  Trash2,
  UserRoundCheck,
  WandSparkles
} from "lucide-react";
import { formatPoints } from "../../shared/lib/points.js";
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

  function getCheckMode(task) {
    if (task.type !== "free-text") {
      return { label: "Автопроверка", icon: WandSparkles };
    }

    return task.checkMode === "llm"
      ? { label: "Проверка LLM", icon: Bot }
      : { label: "Проверяет преподаватель", icon: UserRoundCheck };
  }

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
    <div className="task-list teacher-task-list">
      {tasks.map((task, index) => {
        const checkMode = getCheckMode(task);
        const CheckModeIcon = checkMode.icon;
        return (
          <article
            className={`task-card teacher-task-card${task.isHidden ? " hidden-task" : ""}`}
            key={task.id}
          >
            <header className="teacher-task-card-header">
              <div className="task-heading teacher-task-card-identity">
                <h4>Задание {index + 1}</h4>
                <span>{task.title}</span>
              </div>
              {!readOnly && (
                <div className="task-actions">
                  <button
                    type="button"
                    className="icon-button"
                    title="Редактировать"
                    aria-label={`Редактировать задание «${task.title}»`}
                    disabled={isTaskMutationBusy}
                    onClick={() => onEdit(task)}
                  >
                    <Pencil size={16} aria-hidden="true" />
                  </button>
                  <button
                    type="button"
                    className="icon-button"
                    title={task.isHidden ? "Показать задание" : "Скрыть задание"}
                    aria-label={`${task.isHidden ? "Показать" : "Скрыть"} задание «${task.title}»`}
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
                    aria-label={`Удалить задание «${task.title}»`}
                    disabled={isTaskMutationBusy}
                    onClick={() => onDelete(task)}
                  >
                    <Trash2 size={16} aria-hidden="true" />
                  </button>
                </div>
              )}
            </header>

            <div className="task-subline teacher-task-subline">
              <span className="task-points">{formatPoints(task.maxPoints)}</span>
              <StatusBadge status={task.type} />
              <span className="status teacher-task-check-mode">
                <CheckModeIcon size={14} aria-hidden="true" />
                {checkMode.label}
              </span>
              {task.isHidden && <StatusBadge status="hidden" />}
              {task.type === "multiple-choice" && task.wrongAnswerPenalty > 0 && (
                <span className="task-penalty">Штраф: {task.wrongAnswerPenalty}</span>
              )}
            </div>

            <details className="teacher-task-disclosure">
              <summary>
                <span>Показать содержание</span>
                <ChevronDown size={16} aria-hidden="true" />
              </summary>
              <div className="teacher-task-content">
                <p>{task.prompt}</p>
                {task.options.length > 0 && (
                  <div className="answer-option-list">
                    {task.options.map(option => (
                      <span
                        className={task.correctOptionIds.includes(option.id) ? "correct" : ""}
                        key={option.id}
                      >
                        {task.correctOptionIds.includes(option.id) && (
                          <small className="visually-hidden">Правильный ответ: </small>
                        )}
                        {option.text}
                      </span>
                    ))}
                  </div>
                )}
              </div>
            </details>
          </article>
        );
      })}
    </div>
  );
}

export default TaskList;
