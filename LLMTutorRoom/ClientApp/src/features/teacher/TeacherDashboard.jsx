import {
  ArrowRight,
  BarChart3,
  BookOpen,
  Clock3,
  ListChecks,
  UsersRound
} from "lucide-react";
import { formatDate } from "../../shared/lib/dates.js";
import { InfoTile } from "../../shared/ui/InfoTile.jsx";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";
import { ReviewRows } from "./ReviewRows.jsx";

export function TeacherDashboard({
  overview,
  selectedTest,
  activeReviews,
  onOpenTests,
  onOpenReviews
}) {
  const metricItems = [
    {
      label: "Активные тесты",
      value: overview.metrics.activeTests,
      caption: "доступны ученикам",
      icon: BookOpen,
      tone: "mint"
    },
    {
      label: "Задания",
      value: overview.metrics.tasks,
      caption: "в опубликованных тестах",
      icon: ListChecks,
      tone: "blue"
    },
    {
      label: "Ожидают разбора",
      value: overview.metrics.pendingReviews,
      caption: "требуют внимания",
      icon: Clock3,
      tone: "amber"
    },
    {
      label: "Средний балл",
      value: `${overview.metrics.averageScore}%`,
      caption: "по завершённым работам",
      icon: BarChart3,
      tone: "violet"
    }
  ];
  return (
    <div className="teacher-page teacher-dashboard">
      <section className="metric-grid teacher-metric-grid" aria-label="Сводка">
        {metricItems.map(item => {
          const Icon = item.icon;
          return (
            <article className={`metric-card teacher-metric-card ${item.tone}`} key={item.label}>
              <span className="teacher-metric-icon">
                <Icon size={19} aria-hidden="true" />
              </span>
              <div>
                <span>{item.label}</span>
                <strong>{item.value}</strong>
                <small>{item.caption}</small>
              </div>
            </article>
          );
        })}
      </section>

      <section className="teacher-dashboard-main">
        <article className="panel teacher-current-test">
          <header className="teacher-section-header">
            <div>
              <h2>{selectedTest.title}</h2>
            </div>
            <div className="teacher-current-test-status">
              <span>Версия {selectedTest.versionNumber}</span>
              <StatusBadge status={selectedTest.status} />
            </div>
          </header>

          <p className="muted teacher-current-test-summary">
            {selectedTest.summary || "Добавьте краткое описание, чтобы коллегам было проще ориентироваться в тесте."}
          </p>

          <div className="compact-grid teacher-current-test-meta">
            <InfoTile label="Время" value={`${selectedTest.timeLimitMinutes} мин`} />
            <InfoTile label="Задачи" value={selectedTest.tasks.length} />
            <InfoTile label="Баллы" value={selectedTest.totalPoints} />
            <InfoTile label="Дедлайн" value={formatDate(selectedTest.deadline)} />
          </div>

          <button type="button" className="button primary teacher-current-test-action" onClick={onOpenTests}>
            Открыть тест
            <ArrowRight size={16} aria-hidden="true" />
          </button>
        </article>

      </section>

      <section className="panel teacher-review-overview">
        <header className="teacher-section-header">
          <div>
            <span className="eyebrow">Работы учеников</span>
            <h2>Требуют внимания</h2>
          </div>
          <button type="button" className="button secondary" onClick={onOpenReviews}>
            <UsersRound size={17} aria-hidden="true" />
            Все проверки
          </button>
        </header>
        <ReviewRows reviews={activeReviews} compact />
      </section>
    </div>
  );
}

export default TeacherDashboard;
