import {
  BarChart3,
  BookOpen,
  Clock3,
  Layers3,
  ListChecks,
  Play,
  UsersRound
} from "lucide-react";
import { formatDate } from "../../shared/lib/dates.js";
import { InfoTile } from "../../shared/ui/InfoTile.jsx";
import { ReviewRows } from "./ReviewRows.jsx";

export function TeacherDashboard({ overview, selectedTest, activeReviews, onOpenTests }) {
  const metricItems = [
    { label: "Активные тесты", value: overview.metrics.activeTests, icon: BookOpen },
    { label: "Задания", value: overview.metrics.tasks, icon: ListChecks },
    { label: "Ожидают разбора", value: overview.metrics.pendingReviews, icon: Clock3 },
    { label: "Средний балл", value: `${overview.metrics.averageScore}%`, icon: BarChart3 }
  ];

  return (
    <div className="stack">
      <section className="metric-grid">
        {metricItems.map(item => {
          const Icon = item.icon;
          return (
            <article className="metric-card" key={item.label}>
              <Icon size={20} aria-hidden="true" />
              <span>{item.label}</span>
              <strong>{item.value}</strong>
            </article>
          );
        })}
      </section>

      <section className="two-column">
        <div className="panel">
          <div className="panel-header">
            <div>
              <span className="eyebrow">Текущий тест</span>
              <h2>{selectedTest.title}</h2>
            </div>
            <button type="button" className="button secondary" onClick={onOpenTests}>
              <Layers3 size={16} aria-hidden="true" />
              Открыть
            </button>
          </div>

          <p className="muted">{selectedTest.summary}</p>

          <div className="compact-grid">
            <InfoTile label="Время" value={`${selectedTest.timeLimitMinutes} мин`} />
            <InfoTile label="Задачи" value={selectedTest.tasks.length} />
            <InfoTile label="Баллы" value={selectedTest.totalPoints} />
            <InfoTile label="Дедлайн" value={formatDate(selectedTest.deadline)} />
          </div>
        </div>

        <div className="panel">
          <div className="panel-header">
            <div>
              <span className="eyebrow">Пайплайн</span>
              <h2>Проверка решения</h2>
            </div>
            <Play size={18} aria-hidden="true" />
          </div>

          <div className="pipeline">
            {["Ответ", "Рубрика", "Примеры", "LLM", "Вердикт"].map((step, index) => (
              <div className="pipeline-step" key={step}>
                <span>{index + 1}</span>
                <strong>{step}</strong>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Очередь</span>
            <h2>Требуют внимания</h2>
          </div>
          <UsersRound size={18} aria-hidden="true" />
        </div>
        <ReviewRows reviews={activeReviews} compact />
      </section>
    </div>
  );
}

export default TeacherDashboard;
