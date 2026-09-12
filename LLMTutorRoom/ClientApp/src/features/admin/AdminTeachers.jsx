import { useEffect, useState } from "react";
import { Loader2, Search, UserPlus, UsersRound } from "lucide-react";
import {
  createTeacher as createTeacherRequest,
  getTeachers
} from "../../api/usersApi.js";
import { useUnsavedChangesGuard } from "../../shared/hooks/useUnsavedChangesGuard.js";
import { SearchField } from "../../shared/ui/SearchField.jsx";
import { getRequestErrorMessage } from "./requestError.js";

const emptyTeacherForm = {
  userName: "",
  email: "",
  password: ""
};

function compareTeachers(left, right) {
  return left.userName.localeCompare(right.userName, "ru");
}

export function AdminTeachers() {
  const [teachers, setTeachers] = useState([]);
  const [form, setForm] = useState(emptyTeacherForm);
  const [query, setQuery] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [loadError, setLoadError] = useState("");
  const [actionMessage, setActionMessage] = useState(null);
  const [loadVersion, setLoadVersion] = useState(0);
  const isCreateFormDisabled = isCreating || isLoading || Boolean(loadError);
  const hasUnsavedTeacher = Object.values(form)
    .some(value => String(value).length > 0);
  useUnsavedChangesGuard(
    hasUnsavedTeacher,
    "Есть несохранённые данные нового преподавателя. Покинуть страницу?"
  );

  useEffect(() => {
    const controller = new AbortController();

    async function loadTeachers() {
      setIsLoading(true);
      setLoadError("");

      try {
        const data = await getTeachers({ signal: controller.signal });

        if (!Array.isArray(data)) {
          throw new Error("Teachers response must be an array.");
        }

        if (!controller.signal.aborted) {
          setTeachers([...data].sort(compareTeachers));
        }
      } catch (error) {
        if (!controller.signal.aborted) {
          setLoadError(getRequestErrorMessage(error, "Не удалось загрузить преподавателей."));
        }
      } finally {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      }
    }

    loadTeachers();

    return () => controller.abort();
  }, [loadVersion]);

  async function handleCreateTeacher(event) {
    event.preventDefault();

    if (isLoading || loadError || isCreating) {
      return;
    }

    setIsCreating(true);
    setActionMessage(null);

    try {
      const teacher = await createTeacherRequest(form);
      setTeachers(current => [...current, teacher].sort(compareTeachers));
      setForm(emptyTeacherForm);
      setActionMessage({ type: "success", text: "Преподаватель добавлен." });
    } catch (error) {
      setActionMessage({
        type: "error",
        text: getRequestErrorMessage(error, "Не удалось добавить преподавателя.", {
          409: "Это имя пользователя или email уже заняты."
        })
      });
    } finally {
      setIsCreating(false);
    }
  }

  function updateForm(field, value) {
    setForm(current => ({ ...current, [field]: value }));
    setActionMessage(null);
  }

  const normalizedQuery = query.trim().toLocaleLowerCase("ru-RU");
  const filteredTeachers = normalizedQuery
    ? teachers.filter(teacher => (
      String(teacher.userName ?? "").toLocaleLowerCase("ru-RU").includes(normalizedQuery)
      || String(teacher.email ?? "").toLocaleLowerCase("ru-RU").includes(normalizedQuery)
    ))
    : teachers;

  return (
    <section className="admin-page admin-teachers-page">
      <div className="admin-teachers-layout">
        <section className="panel admin-panel admin-teacher-directory" aria-busy={isLoading}>
          <header className="admin-section-header admin-directory-header">
            <div>
              <h2>Все преподаватели</h2>
              <p>Учётные записи с доступом к кабинету преподавателя.</p>
            </div>
            {!isLoading && !loadError && (
              <span className="count-badge">{filteredTeachers.length}</span>
            )}
          </header>

          {!isLoading && !loadError && teachers.length > 0 && (
            <SearchField
              className="admin-directory-search"
              id="admin-teacher-search"
              aria-label="Найти преподавателя"
              value={query}
              placeholder="Поиск по имени пользователя или email"
              onChange={event => setQuery(event.target.value)}
              onClear={() => setQuery("")}
            />
          )}

          {isLoading ? (
            <div className="admin-state" role="status">
              <Loader2 className="spin" size={22} aria-hidden="true" />
              <strong>Загружаем преподавателей</strong>
              <span>Это займёт несколько секунд.</span>
            </div>
          ) : loadError ? (
            <div className="admin-state error">
              <UsersRound size={22} aria-hidden="true" />
              <strong>Список не загрузился</strong>
              <p className="form-error" role="alert">{loadError}</p>
              <button
                type="button"
                className="button secondary"
                onClick={() => setLoadVersion(current => current + 1)}
              >
                Повторить загрузку
              </button>
            </div>
          ) : teachers.length === 0 ? (
            <div className="admin-state empty">
              <UsersRound size={24} aria-hidden="true" />
              <strong>Преподавателей пока нет</strong>
              <span>Первая созданная учётная запись появится здесь.</span>
            </div>
          ) : filteredTeachers.length === 0 ? (
            <div className="admin-state empty compact">
              <Search size={22} aria-hidden="true" />
              <strong>Ничего не найдено</strong>
              <span>Попробуйте изменить запрос.</span>
            </div>
          ) : (
            <div className="admin-teacher-list">
              {filteredTeachers.map(teacher => (
                <article className="admin-teacher-row" key={teacher.id ?? teacher.userName}>
                  <div className="admin-teacher-identity">
                    <strong>{teacher.userName}</strong>
                    <span>{teacher.email}</span>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        <aside className="panel admin-panel admin-create-teacher">
          <header className="admin-section-header">
            <span className="admin-section-icon">
              <UserPlus size={19} aria-hidden="true" />
            </span>
            <div>
              <h2>Добавить преподавателя</h2>
              <p>Новая учётная запись сразу получит роль преподавателя.</p>
            </div>
          </header>

          <form className="admin-form" onSubmit={handleCreateTeacher}>
            <div className="field">
              <label htmlFor="teacher-user-name">Имя пользователя</label>
              <input
                id="teacher-user-name"
                value={form.userName}
                maxLength={64}
                pattern="\S+"
                placeholder="Например, AnnaSmirnova"
                onChange={event => updateForm("userName", event.target.value)}
                disabled={isCreateFormDisabled}
                autoComplete="off"
                required
              />
              <small>До 64 символов, без пробелов.</small>
            </div>
            <div className="field">
              <label htmlFor="teacher-email">Email</label>
              <input
                id="teacher-email"
                type="email"
                value={form.email}
                maxLength={256}
                placeholder="anna@example.com"
                onChange={event => updateForm("email", event.target.value)}
                disabled={isCreateFormDisabled}
                autoComplete="email"
                autoCapitalize="none"
                spellCheck={false}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="teacher-password">Пароль</label>
              <input
                id="teacher-password"
                type="password"
                value={form.password}
                minLength={6}
                placeholder="Не менее 6 символов"
                onChange={event => updateForm("password", event.target.value)}
                autoComplete="new-password"
                disabled={isCreateFormDisabled}
                required
              />
              <small>Минимум 6 символов.</small>
            </div>

            <div className="admin-action-message" aria-live="polite">
              {actionMessage && (
                <p
                  className={actionMessage.type === "error" ? "form-error" : "form-note"}
                  role={actionMessage.type === "error" ? "alert" : "status"}
                >
                  {actionMessage.text}
                </p>
              )}
            </div>

            <button
              type="submit"
              className="button primary admin-submit"
              disabled={isCreateFormDisabled}
            >
              {isCreating
                ? <Loader2 className="spin" size={16} aria-hidden="true" />
                : <UserPlus size={16} aria-hidden="true" />}
              {isCreating
                ? "Добавление..."
                : isLoading
                  ? "Загрузка списка..."
                  : loadError
                    ? "Сначала обновите список"
                    : "Создать учётную запись"}
            </button>
          </form>
        </aside>
      </div>
    </section>
  );
}

export default AdminTeachers;
