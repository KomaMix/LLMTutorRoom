import { useEffect, useState } from "react";
import { Loader2, UserPlus, UsersRound } from "lucide-react";
import {
  createTeacher as createTeacherRequest,
  getTeachers
} from "../../api/usersApi.js";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";
import { getRequestErrorMessage } from "./requestError.js";

const emptyTeacherForm = {
  userName: "",
  password: "",
  displayName: ""
};

export function AdminTeachers() {
  const [teachers, setTeachers] = useState([]);
  const [form, setForm] = useState(emptyTeacherForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [loadError, setLoadError] = useState("");
  const [actionMessage, setActionMessage] = useState(null);
  const [loadVersion, setLoadVersion] = useState(0);
  const isCreateFormDisabled = isCreating || isLoading || Boolean(loadError);

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
          setTeachers(data);
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
      setTeachers(current => [...current, teacher].sort((left, right) =>
        left.displayName.localeCompare(right.displayName, "ru")));
      setForm(emptyTeacherForm);
      setActionMessage({ type: "success", text: "Преподаватель добавлен." });
    } catch (error) {
      setActionMessage({
        type: "error",
        text: getRequestErrorMessage(error, "Не удалось добавить преподавателя.", {
          409: "Пользователь с таким логином уже существует."
        })
      });
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

        <form className="login-form" onSubmit={handleCreateTeacher}>
          <div className="field">
            <label htmlFor="teacher-user-name">Логин</label>
            <input
              id="teacher-user-name"
              value={form.userName}
              onChange={event => updateForm("userName", event.target.value)}
              disabled={isCreateFormDisabled}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="teacher-display-name">Отображаемое имя</label>
            <input
              id="teacher-display-name"
              value={form.displayName}
              onChange={event => updateForm("displayName", event.target.value)}
              disabled={isCreateFormDisabled}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="teacher-password">Пароль</label>
            <input
              id="teacher-password"
              type="password"
              value={form.password}
              onChange={event => updateForm("password", event.target.value)}
              autoComplete="new-password"
              disabled={isCreateFormDisabled}
              required
            />
          </div>

          {actionMessage && (
            <p
              className={actionMessage.type === "error" ? "form-error" : "form-note"}
              role={actionMessage.type === "error" ? "alert" : "status"}
            >
              {actionMessage.text}
            </p>
          )}

          <button
            type="submit"
            className="button primary"
            disabled={isCreateFormDisabled}
          >
            {isCreating ? <Loader2 className="spin" size={16} aria-hidden="true" /> : <UserPlus size={16} aria-hidden="true" />}
            {isCreating
              ? "Добавление..."
              : isLoading
                ? "Загрузка списка..."
                : loadError
                  ? "Повторите загрузку списка"
                  : "Добавить"}
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
        ) : loadError ? (
          <div className="stack">
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
          <p className="muted">Преподавателей пока нет.</p>
        ) : (
          <div className="review-table">
            {teachers.map(teacher => (
              <article className="review-row" key={teacher.id ?? teacher.userName}>
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

export default AdminTeachers;
