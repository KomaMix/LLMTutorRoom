import React, { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  BarChart3,
  BookOpen,
  Bot,
  CheckCircle2,
  ClipboardCheck,
  Clock3,
  FileText,
  GraduationCap,
  Layers3,
  ListChecks,
  Loader2,
  PanelLeft,
  Play,
  School,
  Send,
  Server,
  ShieldCheck,
  Sparkles,
  UserRoundCheck,
  UsersRound
} from "lucide-react";
import "./styles.css";

const navigation = {
  teacher: [
    { id: "dashboard", label: "Панель", icon: BarChart3 },
    { id: "tests", label: "Тесты", icon: BookOpen },
    { id: "reviews", label: "Проверки", icon: ClipboardCheck },
    { id: "models", label: "Модели", icon: Server }
  ],
  student: [
    { id: "student-tests", label: "Задания", icon: FileText },
    { id: "student-review", label: "Результат", icon: CheckCircle2 }
  ]
};

const statusText = {
  published: "Опубликован",
  draft: "Черновик",
  checked: "Проверено",
  queued: "В очереди",
  "manual-review": "Ручная проверка",
  available: "Доступна",
  standby: "Резерв"
};

function App() {
  const [overview, setOverview] = useState(null);
  const [role, setRole] = useState("teacher");
  const [section, setSection] = useState("dashboard");
  const [selectedTestId, setSelectedTestId] = useState("");
  const [studentName, setStudentName] = useState("Демо студент");
  const [answers, setAnswers] = useState({});
  const [review, setReview] = useState(null);
  const [isChecking, setIsChecking] = useState(false);
  const [loadError, setLoadError] = useState("");

  useEffect(() => {
    let ignore = false;

    async function loadOverview() {
      try {
        const response = await fetch("/api/classroom/overview");
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }

        const data = await response.json();
        if (!ignore) {
          setOverview(data);
          setSelectedTestId(data.tests[0]?.id ?? "");
        }
      } catch (error) {
        if (!ignore) {
          setLoadError("Не удалось загрузить демо-данные.");
        }
      }
    }

    loadOverview();

    return () => {
      ignore = true;
    };
  }, []);

  const selectedTest = useMemo(() => {
    return overview?.tests.find(test => test.id === selectedTestId) ?? overview?.tests[0];
  }, [overview, selectedTestId]);

  function changeRole(nextRole) {
    setRole(nextRole);
    setSection(nextRole === "teacher" ? "dashboard" : "student-tests");

    if (nextRole === "student") {
      const firstPublishedTest = overview?.tests.find(test => test.status === "published");
      if (firstPublishedTest) {
        setSelectedTestId(firstPublishedTest.id);
      }
    }
  }

  async function submitForReview() {
    if (!selectedTest) {
      return;
    }

    setIsChecking(true);
    setReview(null);

    try {
      const response = await fetch("/api/classroom/reviews", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          testId: selectedTest.id,
          studentName,
          answers
        })
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const createdReview = await response.json();
      setReview(createdReview);
      setOverview(current => ({
        ...current,
        reviews: [createdReview, ...(current?.reviews ?? [])]
      }));
      setSection("student-review");
    } finally {
      setIsChecking(false);
    }
  }

  if (loadError) {
    return <ErrorState message={loadError} />;
  }

  if (!overview || !selectedTest) {
    return <LoadingState />;
  }

  const activeNavigation = navigation[role];
  const activeReviews = overview.reviews.filter(item => item.status !== "checked");

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">
            <School size={22} aria-hidden="true" />
          </div>
          <div>
            <strong>LLMTutorRoom</strong>
            <span>adaptive assessment</span>
          </div>
        </div>

        <div className="role-switch" aria-label="Роль">
          <button
            className={role === "teacher" ? "active" : ""}
            type="button"
            onClick={() => changeRole("teacher")}
          >
            <UserRoundCheck size={16} aria-hidden="true" />
            Преподаватель
          </button>
          <button
            className={role === "student" ? "active" : ""}
            type="button"
            onClick={() => changeRole("student")}
          >
            <GraduationCap size={16} aria-hidden="true" />
            Ученик
          </button>
        </div>

        <nav className="nav-list" aria-label="Разделы">
          {activeNavigation.map(item => {
            const Icon = item.icon;
            return (
              <button
                key={item.id}
                type="button"
                className={section === item.id ? "active" : ""}
                onClick={() => setSection(item.id)}
              >
                <Icon size={18} aria-hidden="true" />
                {item.label}
              </button>
            );
          })}
        </nav>

        <div className="sidebar-footer">
          <Bot size={18} aria-hidden="true" />
          <div>
            <strong>{selectedTest.llmModelKey}</strong>
            <span>модель проверки</span>
          </div>
        </div>
      </aside>

      <main className="workspace">
        <header className="topbar">
          <div>
            <span className="eyebrow">НИР prototype</span>
            <h1>{role === "teacher" ? "Рабочее место преподавателя" : "Кабинет ученика"}</h1>
          </div>
          <div className="topbar-actions">
            <StatusBadge status="available" />
            <button type="button" className="icon-button" title="Свернуть меню">
              <PanelLeft size={18} aria-hidden="true" />
            </button>
          </div>
        </header>

        {role === "teacher" && section === "dashboard" && (
          <TeacherDashboard
            overview={overview}
            selectedTest={selectedTest}
            activeReviews={activeReviews}
            onOpenTests={() => setSection("tests")}
          />
        )}

        {role === "teacher" && section === "tests" && (
          <TeacherTests
            tests={overview.tests}
            selectedTest={selectedTest}
            selectedTestId={selectedTestId}
            onSelectTest={setSelectedTestId}
          />
        )}

        {role === "teacher" && section === "reviews" && (
          <ReviewQueue reviews={overview.reviews} />
        )}

        {role === "teacher" && section === "models" && (
          <ModelPanel models={overview.models} selectedModelKey={selectedTest.llmModelKey} />
        )}

        {role === "student" && section === "student-tests" && (
          <StudentWorkspace
            tests={overview.tests}
            selectedTest={selectedTest}
            selectedTestId={selectedTestId}
            studentName={studentName}
            answers={answers}
            isChecking={isChecking}
            onSelectTest={setSelectedTestId}
            onStudentNameChange={setStudentName}
            onAnswerChange={(taskId, value) =>
              setAnswers(current => ({ ...current, [taskId]: value }))}
            onSubmit={submitForReview}
          />
        )}

        {role === "student" && section === "student-review" && (
          <StudentReview review={review} reviews={overview.reviews} />
        )}
      </main>
    </div>
  );
}

function TeacherDashboard({ overview, selectedTest, activeReviews, onOpenTests }) {
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
            <InfoTile label="Модель" value={selectedTest.llmModelKey} />
            <InfoTile label="Задачи" value={selectedTest.tasks.length} />
            <InfoTile label="Баллы" value={selectedTest.totalPoints} />
            <InfoTile label="Критерии" value={selectedTest.criteria.length} />
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

function TeacherTests({ tests, selectedTest, selectedTestId, onSelectTest }) {
  return (
    <section className="split-view">
      <div className="list-panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Материалы</span>
            <h2>Тесты</h2>
          </div>
          <button type="button" className="icon-button" title="Добавить тест">
            <FileText size={18} aria-hidden="true" />
          </button>
        </div>

        <div className="select-list">
          {tests.map(test => (
            <button
              key={test.id}
              type="button"
              className={test.id === selectedTestId ? "active" : ""}
              onClick={() => onSelectTest(test.id)}
            >
              <span>{test.title}</span>
              <small>{test.subject}</small>
            </button>
          ))}
        </div>
      </div>

      <div className="detail-panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">{selectedTest.subject}</span>
            <h2>{selectedTest.title}</h2>
          </div>
          <StatusBadge status={selectedTest.status} />
        </div>

        <p className="muted">{selectedTest.summary}</p>

        <div className="form-row">
          <label>
            Модель проверки
            <select defaultValue={selectedTest.llmModelKey}>
              <option>{selectedTest.llmModelKey}</option>
              <option>gemma3:12b</option>
              <option>mixtral:8x7b-instruct</option>
            </select>
          </label>
          <label>
            Дедлайн
            <input type="date" defaultValue={toInputDate(selectedTest.deadline)} />
          </label>
        </div>

        <h3>Задания</h3>
        <div className="task-list">
          {selectedTest.tasks.map(task => (
            <article className="task-card" key={task.id}>
              <div>
                <strong>{task.title}</strong>
                <span>{task.maxPoints} баллов</span>
              </div>
              <p>{task.prompt}</p>
              <div className="tag-list">
                {task.keywords.map(keyword => (
                  <span key={keyword}>{keyword}</span>
                ))}
              </div>
            </article>
          ))}
        </div>

        <h3>Критерии</h3>
        <div className="criteria-grid">
          {selectedTest.criteria.map(criterion => (
            <article className="criterion" key={criterion.id}>
              <strong>{criterion.title}</strong>
              <span>{criterion.maxPoints} балла</span>
              <p>{criterion.description}</p>
            </article>
          ))}
        </div>

        <h3>Эталонные проверки</h3>
        <div className="reference-list">
          {selectedTest.referenceAnswers.length === 0 && (
            <p className="muted">Эталонные решения еще не добавлены.</p>
          )}
          {selectedTest.referenceAnswers.map(answer => (
            <article className="reference-row" key={answer.id}>
              <ShieldCheck size={18} aria-hidden="true" />
              <div>
                <strong>{answer.studentAlias}</strong>
                <p>{answer.comment}</p>
              </div>
              <span>{answer.score}</span>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

function ReviewQueue({ reviews }) {
  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Submissions</span>
          <h2>Проверки учеников</h2>
        </div>
        <ClipboardCheck size={18} aria-hidden="true" />
      </div>
      <ReviewRows reviews={reviews} />
    </section>
  );
}

function ReviewRows({ reviews, compact = false }) {
  if (reviews.length === 0) {
    return <p className="muted">Нет проверок в этом списке.</p>;
  }

  return (
    <div className="review-table">
      {reviews.map(reviewItem => (
        <article className="review-row" key={reviewItem.id}>
          <div>
            <strong>{reviewItem.studentName}</strong>
            <span>{reviewItem.testTitle}</span>
          </div>
          {!compact && <span>{formatDate(reviewItem.submittedAt)}</span>}
          <StatusBadge status={reviewItem.status} />
          <strong>{reviewItem.score}/{reviewItem.maxScore}</strong>
        </article>
      ))}
    </div>
  );
}

function ModelPanel({ models, selectedModelKey }) {
  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">LLMGateway</span>
          <h2>Модели проверки</h2>
        </div>
        <Bot size={18} aria-hidden="true" />
      </div>

      <div className="model-grid">
        {models.map(model => (
          <article className={model.key === selectedModelKey ? "model-card selected" : "model-card"} key={model.key}>
            <div className="model-icon">
              <Sparkles size={18} aria-hidden="true" />
            </div>
            <div>
              <strong>{model.key}</strong>
              <span>{model.provider}</span>
            </div>
            <StatusBadge status={model.status} />
            <dl>
              <div>
                <dt>priority</dt>
                <dd>{model.priority}</dd>
              </div>
              <div>
                <dt>concurrency</dt>
                <dd>{model.maxConcurrentRequests}</dd>
              </div>
            </dl>
          </article>
        ))}
      </div>
    </section>
  );
}

function StudentWorkspace({
  tests,
  selectedTest,
  selectedTestId,
  studentName,
  answers,
  isChecking,
  onSelectTest,
  onStudentNameChange,
  onAnswerChange,
  onSubmit
}) {
  return (
    <section className="student-layout">
      <div className="list-panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Доступно</span>
            <h2>Тесты</h2>
          </div>
          <GraduationCap size={18} aria-hidden="true" />
        </div>

        <div className="select-list">
          {tests.filter(test => test.status === "published").map(test => (
            <button
              key={test.id}
              type="button"
              className={test.id === selectedTestId ? "active" : ""}
              onClick={() => onSelectTest(test.id)}
            >
              <span>{test.title}</span>
              <small>{test.totalPoints} баллов</small>
            </button>
          ))}
        </div>
      </div>

      <div className="detail-panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">{selectedTest.subject}</span>
            <h2>{selectedTest.title}</h2>
          </div>
          <StatusBadge status={selectedTest.status} />
        </div>

        <p className="muted">{selectedTest.summary}</p>

        <label className="student-name">
          Имя ученика
          <input value={studentName} onChange={event => onStudentNameChange(event.target.value)} />
        </label>

        <div className="answer-stack">
          {selectedTest.tasks.map(task => (
            <article className="answer-card" key={task.id}>
              <div>
                <strong>{task.title}</strong>
                <span>{task.maxPoints} баллов</span>
              </div>
              <p>{task.prompt}</p>
              <textarea
                value={answers[task.id] ?? ""}
                onChange={event => onAnswerChange(task.id, event.target.value)}
                placeholder="Введите решение..."
                rows={7}
              />
            </article>
          ))}
        </div>

        <button type="button" className="button primary" onClick={onSubmit} disabled={isChecking}>
          {isChecking ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Send size={16} aria-hidden="true" />}
          {isChecking ? "Проверка..." : "Отправить на проверку"}
        </button>
      </div>
    </section>
  );
}

function StudentReview({ review, reviews }) {
  const currentReview = review ?? reviews.find(item => item.status === "checked");

  if (!currentReview) {
    return (
      <section className="panel empty-state">
        <CheckCircle2 size={28} aria-hidden="true" />
        <h2>Результатов пока нет</h2>
      </section>
    );
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">{currentReview.testTitle}</span>
          <h2>{currentReview.score}/{currentReview.maxScore} баллов</h2>
        </div>
        <StatusBadge status={currentReview.status} />
      </div>

      <p className="review-summary">{currentReview.summary}</p>

      <div className="result-list">
        {currentReview.taskResults.map(result => (
          <article className="result-card" key={result.taskId}>
            <div>
              <strong>{result.taskTitle}</strong>
              <span>{result.score}/{result.maxScore}</span>
            </div>
            <p>{result.feedback}</p>
            <ul>
              {result.findings.map(finding => (
                <li key={finding}>{finding}</li>
              ))}
            </ul>
          </article>
        ))}
      </div>
    </section>
  );
}

function InfoTile({ label, value }) {
  return (
    <div className="info-tile">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function StatusBadge({ status }) {
  return <span className={`status ${status}`}>{statusText[status] ?? status}</span>;
}

function LoadingState() {
  return (
    <div className="screen-state">
      <Loader2 className="spin" size={26} aria-hidden="true" />
      <span>Загрузка</span>
    </div>
  );
}

function ErrorState({ message }) {
  return (
    <div className="screen-state">
      <span>{message}</span>
    </div>
  );
}

function formatDate(value) {
  return new Intl.DateTimeFormat("ru-RU", {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}

function toInputDate(value) {
  return new Date(value).toISOString().slice(0, 10);
}

createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
