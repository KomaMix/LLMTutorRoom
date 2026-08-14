import React, { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  AlignLeft,
  BarChart3,
  BookOpen,
  Bot,
  CheckCircle2,
  CheckSquare,
  ClipboardCheck,
  Clock3,
  CircleDot,
  Eye,
  EyeOff,
  FileText,
  GraduationCap,
  Layers3,
  ListChecks,
  LockKeyhole,
  LogOut,
  Loader2,
  PanelLeft,
  Pencil,
  Play,
  Plus,
  School,
  Send,
  Server,
  ShieldCheck,
  Sparkles,
  Save,
  Trash2,
  UserPlus,
  UserRoundCheck,
  UsersRound,
  X
} from "lucide-react";
import "./styles.css";

const navigation = {
  admin: [
    { id: "teachers", label: "Преподаватели", icon: UsersRound },
    { id: "model-access", label: "Доступ к моделям", icon: Server }
  ],
  teacher: [
    { id: "dashboard", label: "Панель", icon: BarChart3 },
    { id: "tests", label: "Тесты", icon: BookOpen },
    { id: "reviews", label: "Проверки", icon: ClipboardCheck },
    { id: "models", label: "Модели", icon: Server }
  ],
  student: [
    { id: "student-tests", label: "Задания", icon: FileText },
    { id: "student-review", label: "Результаты", icon: CheckCircle2 }
  ]
};

const statusText = {
  published: "Опубликован",
  draft: "Черновик",
  checked: "Проверено",
  queued: "В очереди",
  processing: "Проверяется",
  "retry-scheduled": "Повторная проверка",
  "manual-review": "Ручная проверка",
  failed: "Ошибка",
  available: "Доступна",
  standby: "Резерв",
  admin: "Администратор",
  teacher: "Преподаватель",
  student: "Ученик",
  "single-choice": "Один ответ",
  "multiple-choice": "Несколько ответов",
  "free-text": "Письменный ответ",
  hidden: "Скрыто",
  "in-progress": "Выполняется",
  submitted: "Отправлено",
  expired: "Время вышло"
};

const authTokenStorageKey = "llmtutorroom.accessToken";
const taskTypes = [
  { value: "single-choice", label: "Один ответ", icon: CircleDot },
  { value: "multiple-choice", label: "Несколько ответов", icon: CheckSquare },
  { value: "free-text", label: "Письменный ответ", icon: AlignLeft }
];

const taskCheckModes = [
  { value: "llm", label: "LLM", icon: Bot },
  { value: "manual", label: "Вручную", icon: UserRoundCheck }
];

const initialTestForm = {
  title: "",
  subject: "",
  summary: "",
  status: "draft",
  deadline: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  timeLimitMinutes: 45,
  llmModelKey: ""
};

function createInitialTaskForm() {
  return {
    type: "single-choice",
    title: "",
    prompt: "",
    checkMode: "auto",
    maxPoints: 1,
    wrongAnswerPenalty: 0,
    options: ["", "", "", ""],
    correctOptionIndexes: []
  };
}

function App() {
  const [currentUser, setCurrentUser] = useState(null);
  const [isAuthChecked, setIsAuthChecked] = useState(false);
  const [overview, setOverview] = useState(null);
  const [section, setSection] = useState("dashboard");
  const [selectedTestId, setSelectedTestId] = useState("");
  const [answers, setAnswers] = useState({});
  const [isStartingAttempt, setIsStartingAttempt] = useState(false);
  const [isSavingAttempt, setIsSavingAttempt] = useState(false);
  const [isSubmittingAttempt, setIsSubmittingAttempt] = useState(false);
  const [hasUnsavedAnswers, setHasUnsavedAnswers] = useState(false);
  const [studentMessage, setStudentMessage] = useState("");
  const [now, setNow] = useState(Date.now());
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [loadError, setLoadError] = useState("");
  const [loginError, setLoginError] = useState("");

  useEffect(() => {
    let ignore = false;

    async function loadCurrentUser() {
      try {
        const accessToken = getAccessToken();
        if (!accessToken) {
          if (!ignore) {
            setCurrentUser(null);
          }
          return;
        }

        const response = await authorizedFetch("/api/auth/me");
        if (!response.ok) {
          if (!ignore) {
            setCurrentUser(null);
          }
          return;
        }

        const user = await response.json();
        if (!ignore) {
          applyUserSession(user);
        }
      } finally {
        if (!ignore) {
          setIsAuthChecked(true);
        }
      }
    }

    loadCurrentUser();

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    if (!currentUser) {
      return;
    }

    if (normalizeRole(currentUser.role) === "admin") {
      return;
    }

    let ignore = false;
    loadOverviewData({ ignore: () => ignore });

    return () => {
      ignore = true;
    };
  }, [currentUser]);

  const selectedTest = useMemo(() => {
    const tests = overview?.tests ?? [];
    return tests.find(test => test.id === selectedTestId) ?? tests[0];
  }, [overview, selectedTestId]);

  const role = normalizeRole(currentUser?.role);

  const selectedAttempt = useMemo(() => {
    if (!overview || !selectedTest) {
      return null;
    }

    return overview.attempts.find(attempt => attempt.testId === selectedTest.id) ?? null;
  }, [overview, selectedTest]);

  const remainingSeconds = selectedAttempt?.status === "in-progress"
    ? Math.max(0, Math.ceil((new Date(selectedAttempt.endsAt).getTime() - now) / 1000))
    : 0;

  useEffect(() => {
    if (role !== "student" || !selectedAttempt || selectedAttempt.status !== "in-progress") {
      return;
    }

    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [role, selectedAttempt?.id, selectedAttempt?.status]);

  useEffect(() => {
    if (role !== "student") {
      return;
    }

    setAnswers(selectedAttempt?.answers ?? {});
    setHasUnsavedAnswers(false);
    setStudentMessage("");
  }, [role, selectedTest?.id, selectedAttempt?.id, selectedAttempt?.status]);

  function applyUserSession(user) {
    const userRole = normalizeRole(user.role);
    setCurrentUser(user);
    setSection(getDefaultSection(userRole));
    setOverview(null);
    setSelectedTestId("");
    setAnswers({});
    setHasUnsavedAnswers(false);
    setStudentMessage("");
  }

  function clearUserSession() {
    setCurrentUser(null);
    setOverview(null);
    setSelectedTestId("");
    setAnswers({});
    setHasUnsavedAnswers(false);
    setStudentMessage("");
    setSection("dashboard");
  }

  async function loadOverviewData(options = {}) {
    try {
      setLoadError("");
      const response = await authorizedFetch("/api/classroom/overview");
      if (response.status === 401) {
        if (!options.ignore?.()) {
          clearUserSession();
        }
        return;
      }

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const data = parseOverview(await response.json());
      if (options.ignore?.()) {
        return;
      }

      const preferredTestId = options.selectedTestId ?? selectedTestId;
      const hasPreferredTest = data.tests.some(test => test.id === preferredTestId);
      setOverview(data);
      setSelectedTestId(hasPreferredTest ? preferredTestId : data.tests[0]?.id ?? "");
    } catch (error) {
      if (!options.ignore?.()) {
        setLoadError("Не удалось загрузить данные.");
      }
    }
  }

  async function login(credentials) {
    setIsSigningIn(true);
    setLoginError("");

    try {
      const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(credentials)
      });

      if (!response.ok) {
        setLoginError("Неверный логин или пароль.");
        return;
      }

      const user = await response.json();
      localStorage.setItem(authTokenStorageKey, user.accessToken);
      applyUserSession(user.user);
    } finally {
      setIsSigningIn(false);
      setIsAuthChecked(true);
    }
  }

  function logout() {
    localStorage.removeItem(authTokenStorageKey);
    clearUserSession();
  }

  async function startAttempt() {
    if (!selectedTest) {
      return;
    }

    setIsStartingAttempt(true);
    setStudentMessage("");

    try {
      const response = await authorizedFetch(`/api/classroom/tests/${selectedTest.id}/attempts/start`, {
        method: "POST"
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const attempt = await response.json();
      updateAttempt(attempt);
      setAnswers(attempt.answers);
      setHasUnsavedAnswers(false);
    } catch (error) {
      setStudentMessage("Не удалось начать тест.");
    } finally {
      setIsStartingAttempt(false);
    }
  }

  async function saveAttemptAnswers(options = {}) {
    if (!selectedAttempt) {
      return;
    }

    if (!options.silent) {
      setIsSavingAttempt(true);
      setStudentMessage("");
    }

    try {
      const response = await authorizedFetch(`/api/classroom/attempts/${selectedAttempt.id}/answers`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          answers
        })
      });

      if (response.status === 409) {
        const attempt = await response.json();
        updateAttempt(attempt);
        setAnswers(attempt.answers);
        setHasUnsavedAnswers(false);
        setStudentMessage("Время выполнения истекло. Ответы больше нельзя изменить.");
        return;
      }

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const attempt = await response.json();
      updateAttempt(attempt);
      setHasUnsavedAnswers(false);

      if (!options.silent) {
        setStudentMessage("Ответы сохранены.");
      }
    } catch (error) {
      if (!options.silent) {
        setStudentMessage("Не удалось сохранить ответы.");
      }
    } finally {
      if (!options.silent) {
        setIsSavingAttempt(false);
      }
    }
  }

  async function submitAttempt() {
    if (!selectedAttempt) {
      return;
    }

    setIsSubmittingAttempt(true);
    setStudentMessage("");

    try {
      const saveResponse = await authorizedFetch(`/api/classroom/attempts/${selectedAttempt.id}/answers`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          answers
        })
      });

      if (saveResponse.status === 409) {
        const attempt = await saveResponse.json();
        updateAttempt(attempt);
        setAnswers(attempt.answers);
        setHasUnsavedAnswers(false);
        setStudentMessage("Время выполнения истекло. Завершить тест уже нельзя.");
        return;
      }

      if (!saveResponse.ok) {
        throw new Error(`HTTP ${saveResponse.status}`);
      }

      const submitResponse = await authorizedFetch(`/api/classroom/attempts/${selectedAttempt.id}/submit`, {
        method: "POST"
      });

      if (submitResponse.status === 409) {
        const attempt = await submitResponse.json();
        updateAttempt(attempt);
        setAnswers(attempt.answers);
        setHasUnsavedAnswers(false);
        setStudentMessage("Время выполнения истекло. Ответы больше нельзя изменить.");
        return;
      }

      if (!submitResponse.ok) {
        throw new Error(`HTTP ${submitResponse.status}`);
      }

      const attempt = await submitResponse.json();
      updateAttempt(attempt);
      setAnswers(attempt.answers);
      setHasUnsavedAnswers(false);
      setStudentMessage("Ответы отправлены. Результаты станут доступны позже.");
      setSection("student-review");
    } catch (error) {
      setStudentMessage("Не удалось завершить тест.");
    } finally {
      setIsSubmittingAttempt(false);
    }
  }

  function updateAttempt(attempt) {
    setOverview(current => {
      if (!current) {
        return current;
      }

      const attempts = current.attempts.filter(item => item.id !== attempt.id);
      return {
        ...current,
        attempts: [attempt, ...attempts]
      };
    });
  }

  useEffect(() => {
    if (!hasUnsavedAnswers
        || !selectedAttempt
        || selectedAttempt.status !== "in-progress") {
      return;
    }

    const timer = window.setTimeout(() => {
      saveAttemptAnswers({ silent: true });
    }, 900);

    return () => window.clearTimeout(timer);
  }, [answers, hasUnsavedAnswers, selectedAttempt?.id, selectedAttempt?.status]);

  if (!isAuthChecked) {
    return <LoadingState />;
  }

  if (!currentUser) {
    return (
      <LoginScreen
        error={loginError}
        isSigningIn={isSigningIn}
        onLogin={login}
      />
    );
  }

  if (loadError) {
    return <ErrorState message={loadError} />;
  }

  if (role !== "admin" && !overview) {
    return <LoadingState />;
  }

  const activeNavigation = navigation[role] ?? [];
  const activeReviews = overview
    ? overview.reviews.filter(item => item.status !== "checked")
    : [];

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">
            <School size={22} aria-hidden="true" />
          </div>
          <div>
            <strong>LLMTutorRoom</strong>
            <span>умная проверка знаний</span>
          </div>
        </div>

        <div className="user-card">
          <div className="user-avatar">
            {role === "admin" && <ShieldCheck size={18} aria-hidden="true" />}
            {role === "teacher" && <UserRoundCheck size={18} aria-hidden="true" />}
            {role === "student" && <GraduationCap size={18} aria-hidden="true" />}
          </div>
          <div>
            <strong>{currentUser.displayName}</strong>
            <span>{getRoleLabel(role)}</span>
          </div>
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

      </aside>

      <main className="workspace">
        <header className="topbar">
          <div>
            <span className="eyebrow">НИР prototype</span>
            <h1>{getPageTitle(role)}</h1>
          </div>
          <div className="topbar-actions">
            <StatusBadge status={role} />
            <button type="button" className="button secondary" onClick={logout}>
              <LogOut size={16} aria-hidden="true" />
              Выйти
            </button>
            <button type="button" className="icon-button" title="Свернуть меню">
              <PanelLeft size={18} aria-hidden="true" />
            </button>
          </div>
        </header>

        {role === "admin" && section === "teachers" && (
          <AdminTeachers />
        )}

        {role === "admin" && section === "model-access" && (
          <AdminModelAccess />
        )}

        {role === "teacher" && section === "dashboard" && (
          selectedTest ? (
            <TeacherDashboard
              overview={overview}
              selectedTest={selectedTest}
              activeReviews={activeReviews}
              onOpenTests={() => setSection("tests")}
            />
          ) : (
            <EmptyTeacherState onOpenTests={() => setSection("tests")} />
          )
        )}

        {role === "teacher" && section === "tests" && (
          <TeacherTests
            tests={overview.tests}
            models={overview.models}
            selectedTest={selectedTest}
            selectedTestId={selectedTestId}
            onSelectTest={setSelectedTestId}
            onTestsChanged={testId => loadOverviewData({ selectedTestId: testId })}
          />
        )}

        {role === "teacher" && section === "reviews" && (
          <ReviewQueue
            reviews={overview.reviews}
            onReviewsChanged={() => loadOverviewData({ selectedTestId })}
          />
        )}

        {role === "teacher" && section === "models" && (
          <ModelPanel models={overview.models} />
        )}

        {role === "student" && section === "student-tests" && (
          selectedTest ? (
            <StudentWorkspace
              tests={overview.tests}
              selectedTest={selectedTest}
              selectedTestId={selectedTestId}
              selectedAttempt={selectedAttempt}
              remainingSeconds={remainingSeconds}
              answers={answers}
              message={studentMessage}
              isStartingAttempt={isStartingAttempt}
              isSavingAttempt={isSavingAttempt}
              isSubmittingAttempt={isSubmittingAttempt}
              onSelectTest={setSelectedTestId}
              onAnswerChange={(taskId, value) => {
                setAnswers(current => ({ ...current, [taskId]: value }));
                setHasUnsavedAnswers(true);
              }}
              onStartAttempt={startAttempt}
              onSaveAnswers={saveAttemptAnswers}
              onSubmitAttempt={submitAttempt}
            />
          ) : (
            <section className="panel empty-state">
              <FileText size={28} aria-hidden="true" />
              <h2>Доступных тестов пока нет</h2>
            </section>
          )
        )}

        {role === "student" && section === "student-review" && (
          <StudentResults
            attempts={overview.attempts}
            reviews={overview.reviews}
            tests={overview.tests}
          />
        )}
      </main>
    </div>
  );
}

function LoginScreen({ error, isSigningIn, onLogin }) {
  const [userName, setUserName] = useState("admin");
  const [password, setPassword] = useState("admin123");

  return (
    <main className="login-screen">
      <section className="login-panel">
        <div className="brand login-brand">
          <div className="brand-mark">
            <School size={22} aria-hidden="true" />
          </div>
          <div>
            <strong>LLMTutorRoom</strong>
            <span>умная проверка знаний</span>
          </div>
        </div>

        <div>
          <span className="eyebrow">Вход</span>
          <h1>Учебный кабинет</h1>
        </div>

        <div className="preset-users">
          <div className="preset-user">
            <ShieldCheck size={17} aria-hidden="true" />
            <span>Стартовый администратор создается из конфигурации.</span>
          </div>
        </div>

        <form
          className="login-form"
          onSubmit={event => {
            event.preventDefault();
            onLogin({ userName, password });
          }}
        >
          <div className="field">
            <label htmlFor="login-user-name">Логин</label>
            <input
              id="login-user-name"
              value={userName}
              onChange={event => setUserName(event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="login-password">Пароль</label>
            <input
              id="login-password"
              type="password"
              value={password}
              onChange={event => setPassword(event.target.value)}
            />
          </div>

          {error && <p className="form-error">{error}</p>}

          <button type="submit" className="button primary" disabled={isSigningIn}>
            {isSigningIn ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <LockKeyhole size={16} aria-hidden="true" />}
            {isSigningIn ? "Вход..." : "Войти"}
          </button>
        </form>
      </section>
    </main>
  );
}

function AdminTeachers() {
  const [teachers, setTeachers] = useState([]);
  const [form, setForm] = useState({
    userName: "teacher2",
    password: "teacher123",
    displayName: "Новый преподаватель"
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [message, setMessage] = useState("");

  useEffect(() => {
    let ignore = false;

    async function loadTeachers() {
      try {
        const response = await authorizedFetch("/api/users/teachers");
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }

        const data = await response.json();
        if (!ignore) {
          setTeachers(data);
        }
      } catch (error) {
        if (!ignore) {
          setMessage("Не удалось загрузить преподавателей.");
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadTeachers();

    return () => {
      ignore = true;
    };
  }, []);

  async function createTeacher(event) {
    event.preventDefault();
    setIsCreating(true);
    setMessage("");

    try {
      const response = await authorizedFetch("/api/users/teachers", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(form)
      });

      if (response.status === 409) {
        setMessage("Пользователь с таким логином уже существует.");
        return;
      }

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const teacher = await response.json();
      setTeachers(current => [...current, teacher].sort((left, right) =>
        left.displayName.localeCompare(right.displayName, "ru")));
      setForm({
        userName: "",
        password: "",
        displayName: ""
      });
      setMessage("Преподаватель добавлен.");
    } catch (error) {
      setMessage("Не удалось добавить преподавателя.");
    } finally {
      setIsCreating(false);
    }
  }

  function updateForm(field, value) {
    setForm(current => ({ ...current, [field]: value }));
  }

  return (
    <section className="admin-layout">
      <div className="panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Доступ</span>
            <h2>Новый преподаватель</h2>
          </div>
          <UserPlus size={18} aria-hidden="true" />
        </div>

        <form className="login-form" onSubmit={createTeacher}>
          <div className="field">
            <label htmlFor="teacher-user-name">Логин</label>
            <input
              id="teacher-user-name"
              value={form.userName}
              onChange={event => updateForm("userName", event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="teacher-display-name">Отображаемое имя</label>
            <input
              id="teacher-display-name"
              value={form.displayName}
              onChange={event => updateForm("displayName", event.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="teacher-password">Пароль</label>
            <input
              id="teacher-password"
              type="password"
              value={form.password}
              onChange={event => updateForm("password", event.target.value)}
            />
          </div>

          {message && <p className="form-note">{message}</p>}

          <button type="submit" className="button primary" disabled={isCreating}>
            {isCreating ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <UserPlus size={16} aria-hidden="true" />}
            {isCreating ? "Добавление..." : "Добавить"}
          </button>
        </form>
      </div>

      <div className="panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Роли</span>
            <h2>Преподаватели</h2>
          </div>
          <UsersRound size={18} aria-hidden="true" />
        </div>

        {isLoading ? (
          <p className="muted">Загрузка преподавателей.</p>
        ) : (
          <div className="review-table">
            {teachers.map(teacher => (
              <article className="review-row" key={teacher.userName}>
                <div>
                  <strong>{teacher.displayName}</strong>
                  <span>{teacher.userName}</span>
                </div>
                <StatusBadge status="teacher" />
              </article>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

function AdminModelAccess() {
  const [teachers, setTeachers] = useState([]);
  const [models, setModels] = useState([]);
  const [accessList, setAccessList] = useState([]);
  const [selectedTeacherId, setSelectedTeacherId] = useState("");
  const [form, setForm] = useState({
    modelKey: "",
    isEnabled: true,
    periodSeconds: 30 * 24 * 60 * 60,
    maxChecks: 100
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [busyModelKey, setBusyModelKey] = useState("");
  const [message, setMessage] = useState("");

  useEffect(() => {
    let ignore = false;

    async function loadInitialData() {
      try {
        const [teachersResponse, modelsResponse] = await Promise.all([
          authorizedFetch("/api/users/teachers"),
          authorizedFetch("/api/classroom/model-access/models")
        ]);

        if (!teachersResponse.ok || !modelsResponse.ok) {
          throw new Error("Could not load model access data.");
        }

        const [teachersData, modelsData] = await Promise.all([
          teachersResponse.json(),
          modelsResponse.json()
        ]);

        if (ignore) {
          return;
        }

        setTeachers(teachersData);
        setModels(modelsData);
        setSelectedTeacherId(teachersData[0]?.id ?? "");
        setForm(current => ({
          ...current,
          modelKey: modelsData[0]?.key ?? ""
        }));
      } catch (error) {
        if (!ignore) {
          setMessage("Не удалось загрузить данные доступа.");
        }
      } finally {
        if (!ignore) {
          setIsLoading(false);
        }
      }
    }

    loadInitialData();

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    if (!selectedTeacherId) {
      setAccessList([]);
      return;
    }

    let ignore = false;

    async function loadAccess() {
      try {
        const response = await authorizedFetch(`/api/classroom/model-access/teachers/${encodeURIComponent(selectedTeacherId)}`);
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }

        const data = await response.json();
        if (!ignore) {
          setAccessList(data);
        }
      } catch (error) {
        if (!ignore) {
          setMessage("Не удалось загрузить лимиты преподавателя.");
        }
      }
    }

    loadAccess();

    return () => {
      ignore = true;
    };
  }, [selectedTeacherId]);

  async function saveAccess(event) {
    event.preventDefault();

    if (!selectedTeacherId || !form.modelKey) {
      return;
    }

    setIsSaving(true);
    setMessage("");

    try {
      const response = await authorizedFetch(
        `/api/classroom/model-access/models/${encodeURIComponent(form.modelKey)}/teachers/${encodeURIComponent(selectedTeacherId)}`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json"
          },
          body: JSON.stringify({
            isEnabled: form.isEnabled,
            periodSeconds: Number(form.periodSeconds),
            maxChecks: Number(form.maxChecks)
          })
        });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const access = await response.json();
      setAccessList(current => [
        access,
        ...current.filter(item => item.modelKey !== access.modelKey)
      ].sort((left, right) => left.modelKey.localeCompare(right.modelKey)));
      setMessage("Доступ сохранен.");
    } catch (error) {
      setMessage("Не удалось сохранить доступ.");
    } finally {
      setIsSaving(false);
    }
  }

  async function removeAccess(access) {
    if (!selectedTeacherId) {
      return;
    }

    setBusyModelKey(access.modelKey);
    setMessage("");

    try {
      const response = await authorizedFetch(
        `/api/classroom/model-access/models/${encodeURIComponent(access.modelKey)}/teachers/${encodeURIComponent(selectedTeacherId)}`,
        {
          method: "DELETE"
        });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      setAccessList(current => current.filter(item => item.modelKey !== access.modelKey));
      setMessage("Доступ удален.");
    } catch (error) {
      setMessage("Не удалось удалить доступ.");
    } finally {
      setBusyModelKey("");
    }
  }

  function updateForm(field, value) {
    setForm(current => ({ ...current, [field]: value }));
  }

  function editAccess(access) {
    setForm({
      modelKey: access.modelKey,
      isEnabled: access.isEnabled,
      periodSeconds: access.periodSeconds,
      maxChecks: access.maxChecks
    });
  }

  const selectedTeacher = teachers.find(teacher => teacher.id === selectedTeacherId);

  return (
    <section className="admin-layout">
      <div className="panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">LLMGateway</span>
            <h2>Лимит модели</h2>
          </div>
          <Server size={18} aria-hidden="true" />
        </div>

        {isLoading ? (
          <p className="muted">Загрузка моделей.</p>
        ) : (
          <form className="login-form" onSubmit={saveAccess}>
            <div className="field">
              <label htmlFor="access-teacher">Преподаватель</label>
              <select
                id="access-teacher"
                value={selectedTeacherId}
                onChange={event => setSelectedTeacherId(event.target.value)}
              >
                {teachers.map(teacher => (
                  <option key={teacher.id} value={teacher.id}>
                    {teacher.displayName}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="access-model">Модель</label>
              <select
                id="access-model"
                value={form.modelKey}
                onChange={event => updateForm("modelKey", event.target.value)}
              >
                {models.map(model => (
                  <option key={model.key} value={model.key}>
                    {model.displayName || model.key}
                  </option>
                ))}
              </select>
            </div>
            <div className="form-row">
              <div className="field">
                <label htmlFor="access-period">Период, секунд</label>
                <input
                  id="access-period"
                  min="1"
                  type="number"
                  value={form.periodSeconds}
                  onChange={event => updateForm("periodSeconds", event.target.value)}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="access-limit">Проверок</label>
                <input
                  id="access-limit"
                  min="1"
                  type="number"
                  value={form.maxChecks}
                  onChange={event => updateForm("maxChecks", event.target.value)}
                  required
                />
              </div>
            </div>
            <label className="inline-toggle">
              <input
                type="checkbox"
                checked={form.isEnabled}
                onChange={event => updateForm("isEnabled", event.target.checked)}
              />
              Доступ включен
            </label>

            {message && <p className="form-note">{message}</p>}

            <button
              type="submit"
              className="button primary"
              disabled={isSaving || !selectedTeacherId || !form.modelKey}
            >
              {isSaving ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Save size={16} aria-hidden="true" />}
              {isSaving ? "Сохранение..." : "Сохранить лимит"}
            </button>
          </form>
        )}
      </div>

      <div className="panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">{selectedTeacher?.userName ?? "teacher"}</span>
            <h2>Выданный доступ</h2>
          </div>
          <ShieldCheck size={18} aria-hidden="true" />
        </div>

        {accessList.length === 0 ? (
          <p className="muted">Доступ к моделям пока не выдан.</p>
        ) : (
          <div className="review-table">
            {accessList.map(access => (
              <article className="review-row" key={access.modelKey}>
                <div>
                  <strong>{access.displayName || access.modelKey}</strong>
                  <span>{access.remainingChecks} из {access.maxChecks} проверок, период {formatPeriod(access.periodSeconds)}</span>
                </div>
                <div className="row-actions">
                  <StatusBadge status={access.isEnabled ? "available" : "standby"} />
                  <button type="button" className="icon-button" title="Редактировать" onClick={() => editAccess(access)}>
                    <Pencil size={16} aria-hidden="true" />
                  </button>
                  <button
                    type="button"
                    className="icon-button danger"
                    title="Удалить доступ"
                    disabled={busyModelKey === access.modelKey}
                    onClick={() => removeAccess(access)}
                  >
                    {busyModelKey === access.modelKey
                      ? <Loader2 className="spin" size={16} aria-hidden="true" />
                      : <Trash2 size={16} aria-hidden="true" />}
                  </button>
                </div>
              </article>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

function EmptyTeacherState({ onOpenTests }) {
  return (
    <section className="panel empty-state">
      <FileText size={28} aria-hidden="true" />
      <h2>Тестов пока нет</h2>
      <button type="button" className="button primary" onClick={onOpenTests}>
        <Plus size={16} aria-hidden="true" />
        Создать тест
      </button>
    </section>
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

function TeacherTests({
  tests,
  models,
  selectedTest,
  selectedTestId,
  onSelectTest,
  onTestsChanged
}) {
  const [testForm, setTestForm] = useState(initialTestForm);
  const [testEditForm, setTestEditForm] = useState(createTestFormFromTest(selectedTest));
  const [taskForm, setTaskForm] = useState(createInitialTaskForm);
  const [taskEditForm, setTaskEditForm] = useState(createInitialTaskForm);
  const [editingTaskId, setEditingTaskId] = useState("");
  const [taskPanelMode, setTaskPanelMode] = useState("list");
  const [deleteTaskCandidate, setDeleteTaskCandidate] = useState(null);
  const [isCreatingTest, setIsCreatingTest] = useState(false);
  const [isCreatingTask, setIsCreatingTask] = useState(false);
  const [isSavingTest, setIsSavingTest] = useState(false);
  const [isSavingTask, setIsSavingTask] = useState(false);
  const [busyTaskId, setBusyTaskId] = useState("");
  const [testMessage, setTestMessage] = useState("");
  const [testEditMessage, setTestEditMessage] = useState("");
  const [taskMessage, setTaskMessage] = useState("");
  const [taskEditMessage, setTaskEditMessage] = useState("");

  useEffect(() => {
    setTestEditForm(createTestFormFromTest(selectedTest));
    setEditingTaskId("");
    setTaskPanelMode("list");
    setDeleteTaskCandidate(null);
    setTaskEditForm(createInitialTaskForm());
    setTestEditMessage("");
    setTaskEditMessage("");
  }, [selectedTest?.id]);

  useEffect(() => {
    if (models.length === 0) {
      return;
    }

    setTestForm(current => current.llmModelKey
      ? current
      : { ...current, llmModelKey: models[0].key });
  }, [models]);

  async function createTest(event) {
    event.preventDefault();
    setIsCreatingTest(true);
    setTestMessage("");

    try {
      const response = await authorizedFetch("/api/teaching/tests", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          title: testForm.title,
          subject: testForm.subject,
          summary: testForm.summary,
          status: testForm.status,
          deadline: `${testForm.deadline}T23:59:00.000Z`,
          timeLimitMinutes: Number(testForm.timeLimitMinutes),
          llmModelKey: testForm.llmModelKey
        })
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const test = await response.json();
      setTestForm(initialTestForm);
      setTestMessage("Тест создан.");
      await onTestsChanged(test.id);
    } catch (error) {
      setTestMessage("Не удалось создать тест.");
    } finally {
      setIsCreatingTest(false);
    }
  }

  async function updateTest(event) {
    event.preventDefault();

    if (!selectedTest) {
      return;
    }

    setIsSavingTest(true);
    setTestEditMessage("");

    try {
      const response = await authorizedFetch(`/api/teaching/tests/${selectedTest.id}`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          title: testEditForm.title,
          subject: testEditForm.subject,
          summary: testEditForm.summary,
          status: testEditForm.status,
          deadline: `${testEditForm.deadline}T23:59:00.000Z`,
          timeLimitMinutes: Number(testEditForm.timeLimitMinutes),
          llmModelKey: testEditForm.llmModelKey
        })
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      setTestEditMessage("Тест сохранен.");
      await onTestsChanged(selectedTest.id);
    } catch (error) {
      setTestEditMessage("Не удалось сохранить тест.");
    } finally {
      setIsSavingTest(false);
    }
  }

  async function createTask(event) {
    event.preventDefault();

    if (!selectedTest) {
      return;
    }

    const validationError = validateTaskForm(taskForm);
    if (validationError) {
      setTaskMessage(validationError);
      return;
    }

    setIsCreatingTask(true);
    setTaskMessage("");

    try {
      const response = await authorizedFetch(`/api/teaching/tests/${selectedTest.id}/tasks`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(createTaskPayload(taskForm, selectedTestHasLlmModel))
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      setTaskForm(createInitialTaskForm());
      setTaskMessage("Задание добавлено.");
      setTaskPanelMode("list");
      await onTestsChanged(selectedTest.id);
    } catch (error) {
      setTaskMessage("Не удалось добавить задание.");
    } finally {
      setIsCreatingTask(false);
    }
  }

  async function updateTask(event) {
    event.preventDefault();

    if (!selectedTest || !editingTaskId) {
      return;
    }

    const validationError = validateTaskForm(taskEditForm);
    if (validationError) {
      setTaskEditMessage(validationError);
      return;
    }

    setIsSavingTask(true);
    setTaskEditMessage("");

    try {
      const response = await authorizedFetch(`/api/teaching/tests/${selectedTest.id}/tasks/${editingTaskId}`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(createTaskPayload(taskEditForm, selectedTestHasLlmModel))
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      setTaskEditMessage("Задание сохранено.");
      await onTestsChanged(selectedTest.id);
    } catch (error) {
      setTaskEditMessage("Не удалось сохранить задание.");
    } finally {
      setIsSavingTask(false);
    }
  }

  async function setTaskVisibility(task, isHidden) {
    if (!selectedTest) {
      return;
    }

    setBusyTaskId(task.id);

    try {
      const response = await authorizedFetch(`/api/teaching/tests/${selectedTest.id}/tasks/${task.id}/visibility`, {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({ isHidden })
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      await onTestsChanged(selectedTest.id);
    } finally {
      setBusyTaskId("");
    }
  }

  async function deleteTask(task) {
    if (!selectedTest || !task) {
      return;
    }

    setBusyTaskId(task.id);

    try {
      const response = await authorizedFetch(`/api/teaching/tests/${selectedTest.id}/tasks/${task.id}`, {
        method: "DELETE"
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      if (editingTaskId === task.id) {
        setEditingTaskId("");
        setTaskEditForm(createInitialTaskForm());
        setTaskPanelMode("list");
      }

      setDeleteTaskCandidate(null);
      await onTestsChanged(selectedTest.id);
    } catch (error) {
      setTaskMessage("Не удалось удалить задание.");
    } finally {
      setBusyTaskId("");
    }
  }

  function updateTestForm(field, value) {
    setTestForm(current => ({ ...current, [field]: value }));
  }

  function updateTestEditForm(field, value) {
    setTestEditForm(current => ({ ...current, [field]: value }));
  }

  function startTaskEdit(task) {
    setEditingTaskId(task.id);
    setTaskEditForm(createTaskFormFromTask(task));
    setTaskEditMessage("");
    setTaskPanelMode("edit");
  }

  const createModelOptions = getModelOptions(models, testForm.llmModelKey);
  const editModelOptions = getModelOptions(models, testEditForm.llmModelKey);
  const selectedTestHasLlmModel = Boolean(selectedTest?.llmModelKey);

  return (
    <section className="tests-layout">
      <div className="list-panel">
        <div className="panel-header">
          <div>
            <span className="eyebrow">Учебные материалы</span>
            <h2>Тесты</h2>
          </div>
          <FileText size={18} aria-hidden="true" />
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
              <small>{createTestListSummary(test)}</small>
            </button>
          ))}
        </div>

        <form className="editor-form test-create-form" onSubmit={createTest}>
          <h3>Новый тест</h3>
          <div className="field">
            <label htmlFor="test-title">Название</label>
            <input
              id="test-title"
              value={testForm.title}
              onChange={event => updateTestForm("title", event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="test-subject">Предмет</label>
            <input
              id="test-subject"
              value={testForm.subject}
              onChange={event => updateTestForm("subject", event.target.value)}
              required
            />
          </div>
          <div className="form-row">
            <div className="field">
              <label htmlFor="test-time-limit">Время, мин</label>
              <input
                id="test-time-limit"
                min="1"
                type="number"
                value={testForm.timeLimitMinutes}
                onChange={event => updateTestForm("timeLimitMinutes", event.target.value)}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="test-status">Статус</label>
              <select
                id="test-status"
                value={testForm.status}
                onChange={event => updateTestForm("status", event.target.value)}
              >
                <option value="draft">Черновик</option>
                <option value="published">Опубликован</option>
              </select>
            </div>
          </div>
          <div className="field">
            <label htmlFor="test-deadline">Дедлайн</label>
            <input
              id="test-deadline"
              type="date"
              value={testForm.deadline}
              onChange={event => updateTestForm("deadline", event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="test-llm-model">LLM-модель проверки</label>
            <select
              id="test-llm-model"
              value={testForm.llmModelKey}
              onChange={event => updateTestForm("llmModelKey", event.target.value)}
            >
              <option value="">Без LLM-модели</option>
              {createModelOptions.map(model => (
                <option key={model.key} value={model.key}>
                  {getModelOptionLabel(model)}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="test-summary">Краткое описание</label>
            <textarea
              id="test-summary"
              className="compact-textarea"
              value={testForm.summary}
              onChange={event => updateTestForm("summary", event.target.value)}
            />
          </div>

          {testMessage && <p className="form-note">{testMessage}</p>}

          <button type="submit" className="button primary" disabled={isCreatingTest}>
            {isCreatingTest ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Plus size={16} aria-hidden="true" />}
            {isCreatingTest ? "Создание..." : "Создать тест"}
          </button>
        </form>
      </div>

      <div className="detail-panel">
        {selectedTest ? (
          <>
            <div className="panel-header">
              <div>
                <span className="eyebrow">{selectedTest.subject}</span>
                <h2>{selectedTest.title}</h2>
              </div>
              <StatusBadge status={selectedTest.status} />
            </div>

            <p className="muted">{selectedTest.summary || "Описание пока не добавлено."}</p>

            <div className="compact-grid test-meta-grid">
              <InfoTile label="Время" value={`${selectedTest.timeLimitMinutes} мин`} />
              <InfoTile label="Задания" value={createTaskCountSummary(selectedTest)} />
              <InfoTile label="Баллы" value={selectedTest.totalPoints} />
              <InfoTile label="Дедлайн" value={formatDate(selectedTest.deadline)} />
              <InfoTile label="LLM" value={selectedTest.llmModelKey || "не выбрана"} />
            </div>

            <form className="editor-form test-edit-form" onSubmit={updateTest}>
              <div className="panel-header">
                <div>
                  <span className="eyebrow">Настройки</span>
                  <h3>Редактирование теста</h3>
                </div>
                <Save size={18} aria-hidden="true" />
              </div>

              <div className="form-row">
                <div className="field">
                  <label htmlFor="edit-test-title">Название</label>
                  <input
                    id="edit-test-title"
                    value={testEditForm.title}
                    onChange={event => updateTestEditForm("title", event.target.value)}
                    required
                  />
                </div>
                <div className="field">
                  <label htmlFor="edit-test-subject">Предмет</label>
                  <input
                    id="edit-test-subject"
                    value={testEditForm.subject}
                    onChange={event => updateTestEditForm("subject", event.target.value)}
                    required
                  />
                </div>
              </div>

              <div className="form-row">
                <div className="field">
                  <label htmlFor="edit-test-time-limit">Время, мин</label>
                  <input
                    id="edit-test-time-limit"
                    min="1"
                    type="number"
                    value={testEditForm.timeLimitMinutes}
                    onChange={event => updateTestEditForm("timeLimitMinutes", event.target.value)}
                    required
                  />
                </div>
                <div className="field">
                  <label htmlFor="edit-test-status">Статус</label>
                  <select
                    id="edit-test-status"
                    value={testEditForm.status}
                    onChange={event => updateTestEditForm("status", event.target.value)}
                  >
                    <option value="draft">Черновик</option>
                    <option value="published">Опубликован</option>
                  </select>
                </div>
              </div>

              <div className="field">
                <label htmlFor="edit-test-deadline">Дедлайн</label>
                <input
                  id="edit-test-deadline"
                  type="date"
                  value={testEditForm.deadline}
                  onChange={event => updateTestEditForm("deadline", event.target.value)}
                  required
                />
              </div>

              <div className="field">
                <label htmlFor="edit-test-llm-model">LLM-модель проверки</label>
                <select
                  id="edit-test-llm-model"
                  value={testEditForm.llmModelKey}
                  onChange={event => updateTestEditForm("llmModelKey", event.target.value)}
                >
                  <option value="">Без LLM-модели</option>
                  {editModelOptions.map(model => (
                    <option key={model.key} value={model.key}>
                      {getModelOptionLabel(model)}
                    </option>
                  ))}
                </select>
              </div>

              <div className="field">
                <label htmlFor="edit-test-summary">Краткое описание</label>
                <textarea
                  id="edit-test-summary"
                  className="compact-textarea"
                  value={testEditForm.summary}
                  onChange={event => updateTestEditForm("summary", event.target.value)}
                />
              </div>

              {testEditMessage && <p className="form-note">{testEditMessage}</p>}

              <button type="submit" className="button primary" disabled={isSavingTest}>
                {isSavingTest ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Save size={16} aria-hidden="true" />}
                {isSavingTest ? "Сохранение..." : "Сохранить тест"}
              </button>
            </form>

            <div className="panel-section">
              <div className="panel-header">
                <div>
                  <span className="eyebrow">Содержание</span>
                  <h3>Задания</h3>
                </div>
                <div className="task-panel-actions" aria-label="Режим работы с заданиями">
                  <button
                    type="button"
                    className={taskPanelMode === "list" ? "active" : ""}
                    onClick={() => setTaskPanelMode("list")}
                  >
                    <ListChecks size={16} aria-hidden="true" />
                    Список
                  </button>
                  <button
                    type="button"
                    className={taskPanelMode === "create" ? "active" : ""}
                    onClick={() => setTaskPanelMode("create")}
                  >
                    <Plus size={16} aria-hidden="true" />
                    Новое
                  </button>
                </div>
              </div>

              {taskPanelMode === "list" && taskMessage && <p className="form-note">{taskMessage}</p>}

              {taskPanelMode === "list" && (
                selectedTest.tasks.length === 0 ? (
                  <section className="empty-state compact-empty-state">
                    <FileText size={24} aria-hidden="true" />
                    <h2>Заданий пока нет</h2>
                    <button
                      type="button"
                      className="button primary"
                      onClick={() => setTaskPanelMode("create")}
                    >
                      <Plus size={16} aria-hidden="true" />
                      Добавить задание
                    </button>
                  </section>
                ) : (
                  <div className="task-list">
                    {selectedTest.tasks.map((task, index) => (
                      <article className={task.isHidden ? "task-card hidden-task" : "task-card"} key={task.id}>
                        <div className="task-card-header">
                          <div className="task-card-title">
                            <strong>{index + 1}. {task.title}</strong>
                            <span>{task.maxPoints} баллов</span>
                          </div>
                          <div className="task-actions">
                            <button
                              type="button"
                              className="icon-button"
                              title="Редактировать"
                              onClick={() => startTaskEdit(task)}
                            >
                              <Pencil size={16} aria-hidden="true" />
                            </button>
                            <button
                              type="button"
                              className="icon-button"
                              title={task.isHidden ? "Показать задание" : "Скрыть задание"}
                              disabled={busyTaskId === task.id}
                              onClick={() => setTaskVisibility(task, !task.isHidden)}
                            >
                              {task.isHidden
                                ? <Eye size={16} aria-hidden="true" />
                                : <EyeOff size={16} aria-hidden="true" />}
                            </button>
                            <button
                              type="button"
                              className="icon-button danger"
                              title="Удалить задание"
                              disabled={busyTaskId === task.id}
                              onClick={() => setDeleteTaskCandidate(task)}
                            >
                              <Trash2 size={16} aria-hidden="true" />
                            </button>
                          </div>
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
                )
              )}

              {taskPanelMode === "create" && (
                <TaskEditorForm
                  form={taskForm}
                  formId="new-task"
                  title="Новое задание"
                  submitLabel="Добавить задание"
                  submittingLabel="Добавление..."
                  message={taskMessage}
                  isSubmitting={isCreatingTask}
                  hasLlmModel={selectedTestHasLlmModel}
                  onChange={setTaskForm}
                  onSubmit={createTask}
                />
              )}

              {taskPanelMode === "edit" && (
                <div className="task-edit-panel">
                  <TaskEditorForm
                    form={taskEditForm}
                    formId={`edit-task-${editingTaskId}`}
                    title="Редактирование задания"
                    submitLabel="Сохранить задание"
                    submittingLabel="Сохранение..."
                    message={taskEditMessage}
                    isSubmitting={isSavingTask}
                    hasLlmModel={selectedTestHasLlmModel}
                    onChange={setTaskEditForm}
                    onSubmit={updateTask}
                  />
                  <button
                    type="button"
                    className="button secondary"
                    onClick={() => setTaskPanelMode("list")}
                  >
                    Вернуться к списку
                  </button>
                </div>
              )}
            </div>

            {deleteTaskCandidate && (
              <ConfirmDialog
                title="Удалить задание?"
                description={`Задание "${deleteTaskCandidate.title}" будет полностью удалено из теста. Если нужно временно убрать его из выдачи ученикам, лучше использовать скрытие.`}
                confirmLabel="Удалить"
                isBusy={busyTaskId === deleteTaskCandidate.id}
                onCancel={() => setDeleteTaskCandidate(null)}
                onConfirm={() => deleteTask(deleteTaskCandidate)}
              />
            )}
          </>
        ) : (
          <section className="empty-state">
            <FileText size={28} aria-hidden="true" />
            <h2>Создай первый тест</h2>
          </section>
        )}
      </div>
    </section>
  );
}

function TaskEditorForm({
  form,
  formId,
  title,
  submitLabel,
  submittingLabel,
  message,
  isSubmitting,
  hasLlmModel,
  onChange,
  onSubmit
}) {
  const isChoiceTask = form.type !== "free-text";

  function updateForm(field, value) {
    onChange({
      ...form,
      [field]: value
    });
  }

  function changeTaskType(type) {
    const options = type === "free-text"
      ? form.options
      : ensureChoiceOptions(form.options);

    onChange({
      ...form,
      type,
      checkMode: type === "free-text" && hasLlmModel ? "llm" : type === "free-text" ? "manual" : "auto",
      options,
      correctOptionIndexes: type === "free-text"
        ? []
        : form.correctOptionIndexes.slice(0, type === "single-choice" ? 1 : form.correctOptionIndexes.length)
    });
  }

  function updateOption(index, value) {
    onChange({
      ...form,
      options: form.options.map((option, optionIndex) =>
        optionIndex === index ? value : option)
    });
  }

  function addOption() {
    onChange({
      ...form,
      options: [...form.options, ""]
    });
  }

  function removeOption(index) {
    onChange({
      ...form,
      options: form.options.filter((_, optionIndex) => optionIndex !== index),
      correctOptionIndexes: form.correctOptionIndexes
        .filter(optionIndex => optionIndex !== index)
        .map(optionIndex => optionIndex > index ? optionIndex - 1 : optionIndex)
    });
  }

  function toggleCorrectOption(index, isChecked) {
    if (form.type === "single-choice") {
      onChange({
        ...form,
        correctOptionIndexes: [index]
      });
      return;
    }

    onChange({
      ...form,
      correctOptionIndexes: isChecked
        ? [...form.correctOptionIndexes, index].sort((left, right) => left - right)
        : form.correctOptionIndexes.filter(optionIndex => optionIndex !== index)
    });
  }

  return (
    <form className="editor-form task-create-form" onSubmit={onSubmit}>
      <div className="panel-header">
        <div>
          <span className="eyebrow">Конструктор</span>
          <h3>{title}</h3>
        </div>
        <Save size={18} aria-hidden="true" />
      </div>

      <div className="task-type-grid" aria-label="Тип задания">
        {taskTypes.map(type => {
          const Icon = type.icon;
          return (
            <button
              key={type.value}
              type="button"
              className={form.type === type.value ? "active" : ""}
              onClick={() => changeTaskType(type.value)}
            >
              <Icon size={17} aria-hidden="true" />
              {type.label}
            </button>
          );
        })}
      </div>

      <div className="form-row">
        <div className="field">
          <label htmlFor={`${formId}-title`}>Название задания</label>
          <input
            id={`${formId}-title`}
            value={form.title}
            onChange={event => updateForm("title", event.target.value)}
            required
          />
        </div>
        <div className="field">
          <label htmlFor={`${formId}-points`}>Максимальный балл</label>
          <input
            id={`${formId}-points`}
            min="0.5"
            step="0.5"
            type="number"
            value={form.maxPoints}
            onChange={event => updateForm("maxPoints", event.target.value)}
            required
          />
        </div>
      </div>

      {form.type === "multiple-choice" && (
        <div className="field">
          <label htmlFor={`${formId}-wrong-answer-penalty`}>Штраф за неверный вариант</label>
          <input
            id={`${formId}-wrong-answer-penalty`}
            min="0"
            step="0.1"
            type="number"
            value={form.wrongAnswerPenalty}
            onChange={event => updateForm("wrongAnswerPenalty", event.target.value)}
          />
        </div>
      )}

      <div className="field">
        <label htmlFor={`${formId}-prompt`}>Текст задания</label>
        <textarea
          id={`${formId}-prompt`}
          value={form.prompt}
          onChange={event => updateForm("prompt", event.target.value)}
          required
        />
      </div>

      {form.type === "free-text" && (
        <div className="field">
          <label>Проверка</label>
          <div className="task-type-grid check-mode-grid" aria-label="Режим проверки">
            {taskCheckModes.map(mode => {
              const Icon = mode.icon;
              const isDisabled = mode.value === "llm" && !hasLlmModel;
              return (
                <button
                  key={mode.value}
                  type="button"
                  disabled={isDisabled}
                  className={form.checkMode === mode.value && !isDisabled ? "active" : ""}
                  onClick={() => updateForm("checkMode", mode.value)}
                >
                  <Icon size={17} aria-hidden="true" />
                  {mode.label}
                </button>
              );
            })}
          </div>
        </div>
      )}

      {isChoiceTask && (
        <div className="option-editor">
          <div className="panel-header">
            <div>
              <span className="eyebrow">Ответы</span>
              <h3>Варианты</h3>
            </div>
            <button type="button" className="button secondary" onClick={addOption}>
              <Plus size={16} aria-hidden="true" />
              Добавить
            </button>
          </div>

          <div className="option-editor-list">
            {form.options.map((option, index) => (
              <div className="option-editor-row" key={index}>
                <input
                  aria-label="Правильный ответ"
                  checked={form.correctOptionIndexes.includes(index)}
                  className="option-control"
                  name={`${formId}-correct-option`}
                  type={form.type === "single-choice" ? "radio" : "checkbox"}
                  onChange={event => toggleCorrectOption(index, event.target.checked)}
                />
                <input
                  aria-label={`Вариант ${index + 1}`}
                  value={option}
                  onChange={event => updateOption(index, event.target.value)}
                  placeholder={`Вариант ${index + 1}`}
                />
                <button
                  type="button"
                  className="icon-button"
                  title="Удалить вариант"
                  disabled={form.options.length <= 2}
                  onClick={() => removeOption(index)}
                >
                  <X size={16} aria-hidden="true" />
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      {message && <p className="form-note">{message}</p>}

      <button type="submit" className="button primary" disabled={isSubmitting}>
        {isSubmitting ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Save size={16} aria-hidden="true" />}
        {isSubmitting ? submittingLabel : submitLabel}
      </button>
    </form>
  );
}

function ConfirmDialog({
  title,
  description,
  confirmLabel,
  isBusy,
  onCancel,
  onConfirm
}) {
  return (
    <div className="modal-backdrop" role="presentation">
      <section className="confirm-dialog" role="dialog" aria-modal="true" aria-labelledby="confirm-dialog-title">
        <div className="confirm-dialog-icon">
          <Trash2 size={20} aria-hidden="true" />
        </div>
        <div>
          <h2 id="confirm-dialog-title">{title}</h2>
          <p>{description}</p>
        </div>
        <div className="confirm-dialog-actions">
          <button type="button" className="button secondary" onClick={onCancel} disabled={isBusy}>
            Отмена
          </button>
          <button type="button" className="button danger" onClick={onConfirm} disabled={isBusy}>
            {isBusy ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Trash2 size={16} aria-hidden="true" />}
            {isBusy ? "Удаление..." : confirmLabel}
          </button>
        </div>
      </section>
    </div>
  );
}

function createTestFormFromTest(test) {
  if (!test) {
    return { ...initialTestForm };
  }

  return {
    title: test.title,
    subject: test.subject,
    summary: test.summary,
    status: test.status,
    deadline: toInputDate(test.deadline),
    timeLimitMinutes: test.timeLimitMinutes,
    llmModelKey: test.llmModelKey ?? ""
  };
}

function createTaskFormFromTask(task) {
  return {
    type: task.type,
    title: task.title,
    prompt: task.prompt,
    checkMode: task.checkMode ?? (task.type === "free-text" ? "llm" : "auto"),
    maxPoints: task.maxPoints,
    wrongAnswerPenalty: task.wrongAnswerPenalty ?? 0,
    options: task.options.length === 0 ? ["", ""] : task.options.map(option => option.text),
    correctOptionIndexes: task.options
      .map((option, index) => task.correctOptionIds.includes(option.id) ? index : -1)
      .filter(index => index >= 0)
  };
}

function createTaskPayload(form, hasLlmModel = true) {
  const isChoiceTask = form.type !== "free-text";
  const choiceData = getChoiceTaskData(form);

  return {
    type: form.type,
    checkMode: isChoiceTask ? "auto" : hasLlmModel ? form.checkMode : "manual",
    title: form.title,
    prompt: form.prompt,
    maxPoints: Number(form.maxPoints),
    wrongAnswerPenalty: form.type === "multiple-choice"
      ? Number(form.wrongAnswerPenalty || 0)
      : 0,
    options: isChoiceTask ? choiceData.options : [],
    correctOptionIndexes: isChoiceTask ? choiceData.correctOptionIndexes : []
  };
}

function getModelOptions(models, currentModelKey) {
  if (!currentModelKey || models.some(model => model.key === currentModelKey)) {
    return models;
  }

  return [
    ...models,
    {
      key: currentModelKey,
      displayName: currentModelKey,
      remainingChecks: 0,
      maxChecks: 0
    }
  ];
}

function getModelOptionLabel(model) {
  const name = model.displayName || model.key;
  if (typeof model.remainingChecks !== "number" || typeof model.maxChecks !== "number") {
    return name;
  }

  return `${name} (${model.remainingChecks}/${model.maxChecks})`;
}

function validateTaskForm(form) {
  const choiceData = getChoiceTaskData(form);

  if (!form.title.trim()) {
    return "Укажи название задания.";
  }

  if (!form.prompt.trim()) {
    return "Укажи текст задания.";
  }

  if (Number(form.maxPoints) <= 0) {
    return "Максимальный балл должен быть больше нуля.";
  }

  if (Number(form.wrongAnswerPenalty || 0) < 0) {
    return "Штраф не может быть отрицательным.";
  }

  if (form.type === "free-text") {
    return "";
  }

  if (choiceData.options.length < 2) {
    return "Добавь минимум два варианта ответа.";
  }

  if (form.type === "single-choice" && choiceData.correctOptionIndexes.length !== 1) {
    return "Для задания с одним ответом выбери один правильный вариант.";
  }

  if (form.type === "multiple-choice" && choiceData.correctOptionIndexes.length === 0) {
    return "Для задания с несколькими ответами выбери хотя бы один правильный вариант.";
  }

  return "";
}

function getChoiceTaskData(taskForm) {
  const optionIndexMap = new Map();
  const options = [];

  taskForm.options.forEach((option, index) => {
    const text = option.trim();
    if (!text) {
      return;
    }

    optionIndexMap.set(index, options.length);
    options.push(text);
  });

  return {
    options,
    correctOptionIndexes: [...new Set(taskForm.correctOptionIndexes
      .map(index => optionIndexMap.get(index))
      .filter(index => index !== undefined))]
  };
}

function ensureChoiceOptions(options) {
  if (options.length >= 2) {
    return options;
  }

  return [...options, ...Array.from({ length: 2 - options.length }, () => "")];
}

function createTestListSummary(test) {
  const visibleTaskCount = test.tasks.filter(task => !task.isHidden).length;
  const hiddenTaskCount = test.tasks.length - visibleTaskCount;
  const hiddenText = hiddenTaskCount > 0
    ? ` · скрыто ${hiddenTaskCount}`
    : "";

  return `${test.subject} · ${visibleTaskCount} заданий${hiddenText} · ${test.timeLimitMinutes} мин`;
}

function createTaskCountSummary(test) {
  const visibleTaskCount = test.tasks.filter(task => !task.isHidden).length;
  const hiddenTaskCount = test.tasks.length - visibleTaskCount;

  if (hiddenTaskCount === 0) {
    return visibleTaskCount;
  }

  return `${visibleTaskCount} + ${hiddenTaskCount} скрыто`;
}

function ReviewQueue({ reviews, onReviewsChanged }) {
  const [selectedReviewId, setSelectedReviewId] = useState("");
  const selectedReview = reviews.find(review => review.id === selectedReviewId)
    ?? reviews.find(review => review.status === "manual-review")
    ?? null;

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Submissions</span>
          <h2>Проверки учеников</h2>
        </div>
        <ClipboardCheck size={18} aria-hidden="true" />
      </div>
      <ReviewRows reviews={reviews} onSelectReview={setSelectedReviewId} />
      {selectedReview?.status === "manual-review" && (
        <ManualReviewPanel review={selectedReview} onSaved={onReviewsChanged} />
      )}
    </section>
  );
}

function ReviewRows({ reviews, compact = false, onSelectReview = null }) {
  if (reviews.length === 0) {
    return <p className="muted">Нет проверок в этом списке.</p>;
  }

  return (
    <div className="review-table">
      {reviews.map(reviewItem => (
        <article className="review-row" key={reviewItem.id}>
          <div>
            <strong>{reviewItem.studentName || reviewItem.studentUserId || "Студент"}</strong>
            <span>{reviewItem.testTitle}</span>
          </div>
          {!compact && <span>{formatDate(reviewItem.submittedAt)}</span>}
          <StatusBadge status={reviewItem.status} />
          <strong>{reviewItem.status === "checked" ? `${reviewItem.score}/${reviewItem.maxScore}` : `-/${reviewItem.maxScore}`}</strong>
          {!compact && reviewItem.status === "manual-review" && (
            <button
              type="button"
              className="icon-button"
              title="Открыть проверку"
              onClick={() => onSelectReview?.(reviewItem.id)}
            >
              <Eye size={16} aria-hidden="true" />
            </button>
          )}
        </article>
      ))}
    </div>
  );
}

function ManualReviewPanel({ review, onSaved }) {
  const manualResults = review.taskResults.filter(result => result.status === "manual-review");

  if (manualResults.length === 0) {
    return null;
  }

  return (
    <div className="manual-review-panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">Ручная проверка</span>
          <h3>{review.testTitle}</h3>
        </div>
        <UserRoundCheck size={18} aria-hidden="true" />
      </div>
      <div className="result-list">
        {manualResults.map(result => (
          <ManualTaskReviewForm
            key={result.taskId}
            reviewId={review.id}
            result={result}
            onSaved={onSaved}
          />
        ))}
      </div>
    </div>
  );
}

function ManualTaskReviewForm({ reviewId, result, onSaved }) {
  const [score, setScore] = useState(result.score ?? 0);
  const [feedback, setFeedback] = useState(result.feedback ?? "");
  const [findings, setFindings] = useState((result.findings ?? []).join("\n"));
  const [message, setMessage] = useState("");
  const [isSaving, setIsSaving] = useState(false);

  async function saveManualReview(event) {
    event.preventDefault();
    setIsSaving(true);
    setMessage("");

    try {
      const response = await authorizedFetch(`/api/classroom/reviews/${reviewId}/tasks/${result.taskId}/manual`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          score: Number(score),
          feedback,
          findings: findings
            .split("\n")
            .map(item => item.trim())
            .filter(Boolean)
        })
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      setMessage("Оценка сохранена.");
      await onSaved?.();
    } catch (error) {
      setMessage("Не удалось сохранить оценку.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <form className="result-card manual-review-form" onSubmit={saveManualReview}>
      <div>
        <strong>{result.taskTitle}</strong>
        <span>{result.maxScore} баллов</span>
      </div>
      <div className="form-row">
        <div className="field">
          <label htmlFor={`manual-score-${reviewId}-${result.taskId}`}>Балл</label>
          <input
            id={`manual-score-${reviewId}-${result.taskId}`}
            min="0"
            max={result.maxScore}
            step="0.1"
            type="number"
            value={score}
            onChange={event => setScore(event.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor={`manual-feedback-${reviewId}-${result.taskId}`}>Комментарий</label>
          <input
            id={`manual-feedback-${reviewId}-${result.taskId}`}
            value={feedback}
            onChange={event => setFeedback(event.target.value)}
          />
        </div>
      </div>
      <div className="field">
        <label htmlFor={`manual-findings-${reviewId}-${result.taskId}`}>Выводы</label>
        <textarea
          id={`manual-findings-${reviewId}-${result.taskId}`}
          className="compact-textarea"
          value={findings}
          onChange={event => setFindings(event.target.value)}
        />
      </div>
      <button type="submit" className="button primary" disabled={isSaving}>
        {isSaving ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Save size={16} aria-hidden="true" />}
        {isSaving ? "Сохранение..." : "Сохранить оценку"}
      </button>
      {message && <p className="form-note">{message}</p>}
    </form>
  );
}

function ModelPanel({ models }) {
  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <span className="eyebrow">LLMGateway</span>
          <h2>Модели проверки</h2>
        </div>
        <Bot size={18} aria-hidden="true" />
      </div>

      {models.length === 0 && (
        <p className="muted">Доступные LLM пока не подключены.</p>
      )}

      <div className="model-grid">
        {models.map(model => (
          <article className="model-card" key={model.key}>
            <div className="model-icon">
              <Sparkles size={18} aria-hidden="true" />
            </div>
            <div>
              <strong>{model.displayName || model.key}</strong>
              <span>{model.provider}</span>
            </div>
            <StatusBadge status={model.status} />
            <dl>
              <div>
                <dt>лимит</dt>
                <dd>{model.remainingChecks}/{model.maxChecks}</dd>
              </div>
              <div>
                <dt>период</dt>
                <dd>{formatPeriod(model.periodSeconds)}</dd>
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
  selectedAttempt,
  remainingSeconds,
  answers,
  message,
  isStartingAttempt,
  isSavingAttempt,
  isSubmittingAttempt,
  onSelectTest,
  onAnswerChange,
  onStartAttempt,
  onSaveAnswers,
  onSubmitAttempt
}) {
  const effectiveAttemptStatus = getEffectiveAttemptStatus(selectedAttempt, remainingSeconds);
  const canEditAnswers = effectiveAttemptStatus === "in-progress" && remainingSeconds > 0;

  function toggleMultipleChoiceOption(taskId, optionId, isChecked) {
    const selectedOptionIds = (answers[taskId] ?? "")
      .split("|")
      .filter(Boolean);
    const nextOptionIds = isChecked
      ? [...new Set([...selectedOptionIds, optionId])]
      : selectedOptionIds.filter(selectedOptionId => selectedOptionId !== optionId);

    onAnswerChange(taskId, nextOptionIds.join("|"));
  }

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
              <small>{test.totalPoints} баллов · {test.timeLimitMinutes} мин</small>
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
          <StatusBadge status={effectiveAttemptStatus ?? selectedTest.status} />
        </div>

        <p className="muted">{selectedTest.summary}</p>

        <div className="attempt-panel">
          <div>
            <span>{selectedAttempt ? "Состояние" : "Тест не начат"}</span>
            <strong>{selectedAttempt ? getAttemptStatusText(effectiveAttemptStatus) : `${selectedTest.timeLimitMinutes} мин`}</strong>
          </div>
          {selectedAttempt && (
            <div>
              <span>Осталось</span>
              <strong>{formatDuration(remainingSeconds)}</strong>
            </div>
          )}
          {!selectedAttempt && (
            <button type="button" className="button primary" onClick={onStartAttempt} disabled={isStartingAttempt}>
              {isStartingAttempt ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Play size={16} aria-hidden="true" />}
              {isStartingAttempt ? "Запуск..." : "Начать тест"}
            </button>
          )}
        </div>

        {message && <p className="form-note">{message}</p>}

        {!selectedAttempt && (
          <section className="empty-state compact-empty-state">
            <Clock3 size={24} aria-hidden="true" />
            <h2>Начни тест, чтобы открыть ответы</h2>
          </section>
        )}

        {selectedAttempt && (
          <>
            <div className="answer-stack">
              {selectedTest.tasks.map(task => (
                <article className="answer-card" key={task.id}>
                  <div>
                    <strong>{task.title}</strong>
                    <span>{task.maxPoints} баллов</span>
                  </div>
                  <p>{task.prompt}</p>
                  {task.type === "free-text" ? (
                    <textarea
                      value={answers[task.id] ?? ""}
                      disabled={!canEditAnswers}
                      onChange={event => onAnswerChange(task.id, event.target.value)}
                      placeholder="Введите решение..."
                      rows={7}
                    />
                  ) : (
                    <div className="student-option-list">
                      {task.options.map(option => (
                        <label className="student-option" key={option.id}>
                          <input
                            checked={(answers[task.id] ?? "").split("|").includes(option.id)}
                            disabled={!canEditAnswers}
                            name={`student-answer-${task.id}`}
                            type={task.type === "single-choice" ? "radio" : "checkbox"}
                            onChange={event => {
                              if (task.type === "single-choice") {
                                onAnswerChange(task.id, option.id);
                                return;
                              }

                              toggleMultipleChoiceOption(task.id, option.id, event.target.checked);
                            }}
                          />
                          <span>{option.text}</span>
                        </label>
                      ))}
                    </div>
                  )}
                </article>
              ))}
            </div>

            <div className="attempt-actions">
              <button
                type="button"
                className="button secondary"
                onClick={onSaveAnswers}
                disabled={!canEditAnswers || isSavingAttempt || isSubmittingAttempt}
              >
                {isSavingAttempt ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Save size={16} aria-hidden="true" />}
                {isSavingAttempt ? "Сохранение..." : "Сохранить ответы"}
              </button>
              <button
                type="button"
                className="button primary"
                onClick={onSubmitAttempt}
                disabled={!canEditAnswers || isSavingAttempt || isSubmittingAttempt}
              >
                {isSubmittingAttempt ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <Send size={16} aria-hidden="true" />}
                {isSubmittingAttempt ? "Завершение..." : "Завершить тест"}
              </button>
            </div>

            {effectiveAttemptStatus === "expired" && (
              <p className="form-note">Время выполнения истекло. Ответы заблокированы.</p>
            )}
            {effectiveAttemptStatus === "submitted" && (
              <p className="form-note">Ответы отправлены. Результаты станут доступны позже.</p>
            )}
          </>
        )}
      </div>
    </section>
  );
}

function StudentResults({ attempts, reviews, tests }) {
  const currentTime = Date.now();
  const completedAttempts = attempts.filter(attempt =>
    attempt.status !== "in-progress" || new Date(attempt.endsAt).getTime() <= currentTime);

  if (completedAttempts.length === 0 && reviews.length === 0) {
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
          <span className="eyebrow">История</span>
          <h2>Результаты</h2>
        </div>
        <CheckCircle2 size={18} aria-hidden="true" />
      </div>

      {completedAttempts.length > 0 && (
        <div className="result-list">
          {completedAttempts.map(attempt => {
            const test = tests.find(item => item.id === attempt.testId);
            const attemptStatus = getEffectiveAttemptStatus(
              attempt,
              Math.ceil((new Date(attempt.endsAt).getTime() - currentTime) / 1000));
            return (
              <article className="result-card" key={attempt.id}>
                <div>
                  <strong>{test?.title ?? "Тест"}</strong>
                  <StatusBadge status={attemptStatus} />
                </div>
                <p>{attemptStatus === "submitted"
                  ? "Ответы отправлены. Результаты станут доступны позже."
                  : "Время выполнения истекло. Ответы больше нельзя изменить."}</p>
                <div className="result-meta">
                  <span>Начало: {formatDate(attempt.startedAt)}</span>
                  <span>Окончание: {formatDate(attempt.submittedAt ?? attempt.endsAt)}</span>
                </div>
              </article>
            );
          })}
        </div>
      )}

      {reviews.length > 0 && (
        <>
          <h3>Проверки</h3>
          <div className="result-list">
            {reviews.map(review => (
              <article className="result-card" key={review.id}>
                <div>
                  <strong>{review.testTitle}</strong>
                  <StatusBadge status={review.status} />
                </div>
                <p>{review.status === "checked" ? review.summary : getReviewStatusMessage(review.status)}</p>
                {review.status === "checked" && (
                  <>
                    <div className="result-meta">
                      <span>{review.score}/{review.maxScore}</span>
                    </div>
                    <ul>
                      {review.taskResults.flatMap(result => result.findings).map(finding => (
                        <li key={finding}>{finding}</li>
                      ))}
                    </ul>
                  </>
                )}
              </article>
            ))}
          </div>
        </>
      )}
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

function parseOverview(data) {
  if (!data || typeof data !== "object") {
    throw new Error("Overview response must be an object.");
  }

  requireArray(data.tests, "tests");
  requireArray(data.models, "models");
  requireArray(data.reviews, "reviews");
  requireArray(data.attempts, "attempts");

  if (!data.metrics || typeof data.metrics !== "object") {
    throw new Error("Overview response must contain metrics.");
  }

  data.tests.forEach((test, testIndex) => {
    if (typeof test.llmModelKey !== "string") {
      throw new Error(`tests[${testIndex}].llmModelKey must be a string.`);
    }

    requireArray(test.tasks, `tests[${testIndex}].tasks`);

    test.tasks.forEach((task, taskIndex) => {
      if (typeof task.isHidden !== "boolean") {
        throw new Error(`tests[${testIndex}].tasks[${taskIndex}].isHidden must be a boolean.`);
      }

      if (typeof task.wrongAnswerPenalty !== "number") {
        throw new Error(`tests[${testIndex}].tasks[${taskIndex}].wrongAnswerPenalty must be a number.`);
      }

      if (typeof task.createdAt !== "string") {
        throw new Error(`tests[${testIndex}].tasks[${taskIndex}].createdAt must be a string.`);
      }

      if (typeof task.checkMode !== "string") {
        throw new Error(`tests[${testIndex}].tasks[${taskIndex}].checkMode must be a string.`);
      }

      requireArray(task.options, `tests[${testIndex}].tasks[${taskIndex}].options`);
      requireArray(task.correctOptionIds, `tests[${testIndex}].tasks[${taskIndex}].correctOptionIds`);
    });
  });

  data.reviews.forEach((review, reviewIndex) => {
    requireArray(review.taskResults, `reviews[${reviewIndex}].taskResults`);

    review.taskResults.forEach((taskResult, taskResultIndex) => {
      requireArray(taskResult.findings, `reviews[${reviewIndex}].taskResults[${taskResultIndex}].findings`);
    });
  });

  data.attempts.forEach((attempt, attemptIndex) => {
    if (typeof attempt.id !== "number") {
      throw new Error(`attempts[${attemptIndex}].id must be a number.`);
    }

    if (typeof attempt.testId !== "string") {
      throw new Error(`attempts[${attemptIndex}].testId must be a string.`);
    }

    if (typeof attempt.status !== "string") {
      throw new Error(`attempts[${attemptIndex}].status must be a string.`);
    }

    if (typeof attempt.startedAt !== "string") {
      throw new Error(`attempts[${attemptIndex}].startedAt must be a string.`);
    }

    if (typeof attempt.endsAt !== "string") {
      throw new Error(`attempts[${attemptIndex}].endsAt must be a string.`);
    }

    if (!attempt.answers || typeof attempt.answers !== "object" || Array.isArray(attempt.answers)) {
      throw new Error(`attempts[${attemptIndex}].answers must be an object.`);
    }
  });

  return data;
}

function requireArray(value, fieldName) {
  if (!Array.isArray(value)) {
    throw new Error(`${fieldName} must be an array.`);
  }
}

function getEffectiveAttemptStatus(attempt, remainingSeconds) {
  if (!attempt) {
    return null;
  }

  if (attempt.status === "in-progress" && remainingSeconds <= 0) {
    return "expired";
  }

  return attempt.status;
}

function getAttemptStatusText(status) {
  return statusText[status] ?? status;
}

function getReviewStatusMessage(status) {
  if (status === "manual-review") {
    return "Проверка ожидает преподавателя.";
  }

  if (status === "failed") {
    return "Проверка остановлена и требует внимания преподавателя.";
  }

  return "Проверка выполняется. Результат появится после завершения.";
}

function formatDuration(totalSeconds) {
  const safeSeconds = Math.max(0, totalSeconds);
  const minutes = Math.floor(safeSeconds / 60);
  const seconds = safeSeconds % 60;

  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

function formatDate(value) {
  return new Intl.DateTimeFormat("ru-RU", {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}

function formatPeriod(seconds) {
  if (!seconds || seconds <= 0) {
    return "не задан";
  }

  const days = seconds / 86400;
  if (Number.isInteger(days) && days >= 1) {
    return `${days} дн.`;
  }

  const hours = seconds / 3600;
  if (Number.isInteger(hours) && hours >= 1) {
    return `${hours} ч.`;
  }

  const minutes = seconds / 60;
  if (Number.isInteger(minutes) && minutes >= 1) {
    return `${minutes} мин.`;
  }

  return `${seconds} сек.`;
}

function toInputDate(value) {
  return new Date(value).toISOString().slice(0, 10);
}

function normalizeRole(role) {
  if (role === "Admin" || role === "admin")
    return "admin";

  if (role === "Student" || role === "student")
    return "student";

  return "teacher";
}

function getDefaultSection(role) {
  if (role === "admin")
    return "teachers";

  return role === "student" ? "student-tests" : "dashboard";
}

function getRoleLabel(role) {
  return statusText[role] ?? role;
}

function getPageTitle(role) {
  if (role === "admin")
    return "Администрирование";

  return role === "teacher" ? "Рабочее место преподавателя" : "Кабинет ученика";
}

function getAccessToken() {
  return localStorage.getItem(authTokenStorageKey);
}

function authorizedFetch(url, options = {}) {
  const accessToken = getAccessToken();
  const headers = new Headers(options.headers ?? {});

  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  return fetch(url, {
    ...options,
    headers
  });
}

createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
