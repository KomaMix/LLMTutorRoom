import {
  BookOpen,
  Clock3,
  UsersRound
} from "lucide-react";
import { ReviewRows } from "./ReviewRows.jsx";

export function TeacherDashboard({
  overview,
  activeReviews,
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
      label: "Ожидают разбора",
      value: overview.metrics.pendingReviews,
      caption: "требуют внимания",
      icon: Clock3,
      tone: "amber"
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
