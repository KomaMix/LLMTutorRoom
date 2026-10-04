import { useCallback, useEffect, useMemo, useState } from "react";
import {
  BookOpen,
  CalendarClock,
  Loader2,
  Plus,
  RefreshCw,
  Save,
  Search,
  UsersRound
} from "lucide-react";
import {
  createStudyGroup,
  createTestAssignment,
  getAssignableTests,
  getStudyGroups,
  updateStudyGroup
} from "../../api/teachingApi.js";
import { getStudents, getTeachers } from "../../api/usersApi.js";
import { ApiError } from "../../api/httpClient.js";
import {
  formatDate,
  localDateTimeToIso,
  toInputDateTime
} from "../../shared/lib/dates.js";

function createGroupForm(group = null, isAdmin = false, teachers = []) {
  return {
    name: group?.name ?? "",
    teacherUserId: group?.teacherUserId ?? (isAdmin ? teachers[0]?.id ?? "" : ""),
    studentUserIds: group?.studentUserIds ?? [],
    revision: group?.revision ?? 0
  };
}

function createAssignmentForm() {
  const now = new Date();
  const deadline = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
  return {
    testVersion: "",
    availableFrom: toInputDateTime(now),
    deadline: toInputDateTime(deadline)
  };
}

function getUserLabel(user) {
  return user ? `${user.userName} · ${user.email}` : "Неизвестный пользователь";
}

function getActionError(error, fallback) {
  if (error instanceof ApiError && error.status === 409) {
    return "Данные изменились в другом окне. Обновите список и повторите действие.";
  }

  if (error instanceof ApiError && error.data?.detail) {
    return error.data.detail;
  }

  return fallback;
}

export function StudyGroupsPage({ attempts = [], isAdmin = false, onChanged = null }) {
  const [groups, setGroups] = useState([]);
  const [students, setStudents] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [selectedGroupId, setSelectedGroupId] = useState("");
  const [isCreating, setIsCreating] = useState(false);
  const [form, setForm] = useState(() => createGroupForm(null, isAdmin));
  const [studentSearch, setStudentSearch] = useState("");
  const [assignableTests, setAssignableTests] = useState([]);
  const [assignmentForm, setAssignmentForm] = useState(createAssignmentForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isAssigning, setIsAssigning] = useState(false);
  const [message, setMessage] = useState(null);

  const selectedGroup = groups.find(group => group.id === selectedGroupId) ?? null;
  const usersById = useMemo(() => new Map(
    [...students, ...teachers].map(user => [user.id, user])),
  [students, teachers]);
  const filteredStudents = useMemo(() => {
    const query = studentSearch.trim().toLocaleLowerCase("ru");
    if (!query) {
      return students;
    }

    return students.filter(student =>
      `${student.userName} ${student.email}`.toLocaleLowerCase("ru").includes(query));
  }, [studentSearch, students]);

  const loadData = useCallback(async preferredGroupId => {
    setIsLoading(true);
    setMessage(null);
    try {
      const [nextGroups, nextStudents, nextTeachers] = await Promise.all([
        getStudyGroups(),
        getStudents(),
        isAdmin ? getTeachers() : Promise.resolve([])
      ]);
      setGroups(nextGroups);
      setStudents(nextStudents);
      setTeachers(nextTeachers);

      const nextSelected = nextGroups.find(group => group.id === preferredGroupId)
        ?? nextGroups[0]
        ?? null;
      setSelectedGroupId(nextSelected?.id ?? "");
      setIsCreating(!nextSelected);
      setForm(createGroupForm(nextSelected, isAdmin, nextTeachers));
    } catch (error) {
      setMessage({
        type: "error",
        text: getActionError(error, "Не удалось загрузить учебные группы.")
      });
    } finally {
      setIsLoading(false);
    }
  }, [isAdmin]);

  useEffect(() => {
    loadData("");
  }, [loadData]);

  useEffect(() => {
    if (!selectedGroupId || isCreating) {
      setAssignableTests([]);
      return;
    }

    const controller = new AbortController();
    getAssignableTests(selectedGroupId, { signal: controller.signal })
      .then(tests => {
        setAssignableTests(tests);
        setAssignmentForm(current => ({
          ...current,
          testVersion: tests.some(test =>
            `${test.id}:${test.versionNumber}` === current.testVersion)
            ? current.testVersion
            : tests[0] ? `${tests[0].id}:${tests[0].versionNumber}` : ""
        }));
      })
      .catch(error => {
        if (error?.name !== "AbortError") {
          setMessage({
            type: "error",
            text: getActionError(error, "Не удалось загрузить опубликованные версии тестов.")
          });
        }
      });

    return () => controller.abort();
  }, [isCreating, selectedGroupId]);

  function selectGroup(group) {
    setSelectedGroupId(group.id);
    setIsCreating(false);
    setForm(createGroupForm(group, isAdmin, teachers));
    setStudentSearch("");
    setAssignmentForm(createAssignmentForm());
    setMessage(null);
  }

  function beginCreate() {
    setSelectedGroupId("");
    setIsCreating(true);
    setForm(createGroupForm(null, isAdmin, teachers));
    setStudentSearch("");
    setAssignableTests([]);
    setMessage(null);
  }

  async function refreshPage() {
    await Promise.all([
      loadData(selectedGroupId),
      onChanged?.()
    ]);
  }

  function toggleStudent(studentUserId) {
    setForm(current => ({
      ...current,
      studentUserIds: current.studentUserIds.includes(studentUserId)
        ? current.studentUserIds.filter(id => id !== studentUserId)
        : [...current.studentUserIds, studentUserId]
    }));
  }

  async function saveGroup(event) {
    event.preventDefault();
    if (!form.name.trim()) {
      setMessage({ type: "error", text: "Укажите название группы." });
      return;
    }

    if (isAdmin && isCreating && !form.teacherUserId) {
      setMessage({ type: "error", text: "Выберите преподавателя группы." });
      return;
    }

    setIsSaving(true);
    setMessage(null);
    try {
      const savedGroup = isCreating
        ? await createStudyGroup({
            name: form.name,
            teacherUserId: isAdmin ? form.teacherUserId : null,
            studentUserIds: form.studentUserIds
          })
        : await updateStudyGroup(selectedGroupId, {
            name: form.name,
            studentUserIds: form.studentUserIds,
            revision: form.revision
          });
      await loadData(savedGroup.id);
      await onChanged?.();
      setMessage({ type: "success", text: isCreating ? "Группа создана." : "Группа сохранена." });
    } catch (error) {
      setMessage({
        type: "error",
        text: getActionError(error, "Не удалось сохранить группу.")
      });
    } finally {
      setIsSaving(false);
    }
  }

  async function assignTest(event) {
    event.preventDefault();
    const [testId, revisionValue] = assignmentForm.testVersion.split(":");
    const testRevision = Number(revisionValue);
    if (!testId || !Number.isInteger(testRevision)) {
      setMessage({ type: "error", text: "Выберите опубликованную версию теста." });
      return;
    }

    const availableFromTime = new Date(assignmentForm.availableFrom).getTime();
    const deadlineTime = new Date(assignmentForm.deadline).getTime();
    if (!Number.isFinite(availableFromTime) || !Number.isFinite(deadlineTime)) {
      setMessage({ type: "error", text: "Укажите корректные дату открытия и дедлайн." });
      return;
    }

    if (deadlineTime <= availableFromTime) {
      setMessage({ type: "error", text: "Дедлайн должен быть позже даты открытия." });
      return;
    }

    if (deadlineTime <= Date.now()) {
      setMessage({ type: "error", text: "Дедлайн должен быть в будущем." });
      return;
    }

    setIsAssigning(true);
    setMessage(null);
    try {
      await createTestAssignment(selectedGroupId, {
        testId,
        testRevision,
        availableFrom: localDateTimeToIso(assignmentForm.availableFrom),
        deadline: localDateTimeToIso(assignmentForm.deadline)
      });
      await loadData(selectedGroupId);
      await onChanged?.();
      setAssignmentForm(createAssignmentForm());
      setMessage({ type: "success", text: "Тест назначен группе." });
    } catch (error) {
      setMessage({
        type: "error",
        text: getActionError(error, "Не удалось назначить тест.")
      });
    } finally {
      setIsAssigning(false);
    }
  }

  return (
    <section className={`groups-page ${isAdmin ? "admin-page" : "teacher-page"}`}>
      <header className="groups-page-heading panel">
        <div className="groups-heading-copy">
          <span className="groups-heading-icon"><UsersRound size={22} aria-hidden="true" /></span>
          <div>
            <span className="eyebrow">Учебный процесс</span>
            <h2>Группы и назначения</h2>
            <p>Формируйте состав групп и назначайте им зафиксированные версии тестов.</p>
          </div>
        </div>
        <div className="groups-heading-actions">
          <button type="button" className="button secondary" onClick={refreshPage} disabled={isLoading}>
            <RefreshCw className={isLoading ? "spin" : ""} size={16} aria-hidden="true" />
            Обновить
          </button>
          <button type="button" className="button primary" onClick={beginCreate}>
            <Plus size={16} aria-hidden="true" />
            Новая группа
          </button>
        </div>
      </header>

      {message && (
        <p className={message.type === "error" ? "form-error groups-message" : "form-note groups-message"}
          role={message.type === "error" ? "alert" : "status"}>
          {message.text}
        </p>
      )}

      <div className="groups-layout">
        <aside className="panel groups-directory">
          <div className="groups-directory-heading">
            <div>
              <span className="eyebrow">Список</span>
              <h3>Учебные группы</h3>
            </div>
            <strong>{groups.length}</strong>
          </div>
          {isLoading ? (
            <div className="groups-loading"><Loader2 className="spin" size={20} /> Загрузка</div>
          ) : groups.length === 0 ? (
            <p className="muted">Групп пока нет.</p>
          ) : (
            <div className="groups-list">
              {groups.map(group => (
                <button
                  type="button"
                  className={group.id === selectedGroupId && !isCreating ? "groups-list-item active" : "groups-list-item"}
                  key={group.id}
                  onClick={() => selectGroup(group)}
                >
                  <strong>{group.name}</strong>
                  <span>{group.studentUserIds.length} учеников · {group.assignments.length} назначений</span>
                  {isAdmin && <small>{getUserLabel(usersById.get(group.teacherUserId))}</small>}
                </button>
              ))}
            </div>
          )}
        </aside>

        <main className="groups-content">
          <form className="panel groups-editor" onSubmit={saveGroup}>
            <header className="groups-section-heading">
              <div>
                <span className="eyebrow">{isCreating ? "Создание" : "Настройка"}</span>
                <h3>{isCreating ? "Новая группа" : selectedGroup?.name}</h3>
              </div>
              <button type="submit" className="button primary" disabled={isSaving || isLoading}>
                {isSaving ? <Loader2 className="spin" size={16} /> : <Save size={16} />}
                {isSaving ? "Сохранение..." : "Сохранить"}
              </button>
            </header>

            <div className="form-row groups-main-fields">
              <label className="field">
                <span>Название группы</span>
                <input value={form.name} maxLength={200} required
                  onChange={event => setForm(current => ({ ...current, name: event.target.value }))} />
              </label>
              {isAdmin && (
                <label className="field">
                  <span>Преподаватель</span>
                  {isCreating ? (
                    <select value={form.teacherUserId} required
                      onChange={event => setForm(current => ({ ...current, teacherUserId: event.target.value }))}>
                      <option value="">Выберите преподавателя</option>
                      {teachers.map(teacher => (
                        <option key={teacher.id} value={teacher.id}>{getUserLabel(teacher)}</option>
                      ))}
                    </select>
                  ) : (
                    <input value={getUserLabel(usersById.get(selectedGroup?.teacherUserId))} disabled />
                  )}
                </label>
              )}
            </div>

            <section className="groups-members">
              <header className="groups-members-heading">
                <div>
                  <h4>Состав группы</h4>
                  <span>Выбрано: {form.studentUserIds.length}</span>
                </div>
                <label className="groups-search">
                  <Search size={16} aria-hidden="true" />
                  <input value={studentSearch} placeholder="Имя или email"
                    onChange={event => setStudentSearch(event.target.value)} />
                </label>
              </header>
              <div className="groups-student-list">
                {filteredStudents.length === 0 ? (
                  <p className="muted">Ученики не найдены.</p>
                ) : filteredStudents.map(student => (
                  <label className="groups-student-row" key={student.id}>
                    <input type="checkbox" checked={form.studentUserIds.includes(student.id)}
                      onChange={() => toggleStudent(student.id)} />
                    <span><strong>{student.userName}</strong><small>{student.email}</small></span>
                  </label>
                ))}
              </div>
            </section>
          </form>

          {!isCreating && selectedGroup && (
            <section className="panel groups-assignments">
              <header className="groups-section-heading">
                <div>
                  <span className="eyebrow">Тестирование</span>
                  <h3>Назначения группы</h3>
                </div>
                <strong className="groups-count">{selectedGroup.assignments.length}</strong>
              </header>

              <form className="groups-assignment-form" onSubmit={assignTest}>
                <label className="field groups-test-select">
                  <span>Опубликованная версия</span>
                  <select value={assignmentForm.testVersion} required
                    onChange={event => setAssignmentForm(current => ({ ...current, testVersion: event.target.value }))}>
                    <option value="">Выберите тест</option>
                    {assignableTests.map(test => (
                      <option key={`${test.id}:${test.versionNumber}`} value={`${test.id}:${test.versionNumber}`}>
                        {test.title} · версия {test.versionNumber}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span>Доступен с</span>
                  <input type="datetime-local" value={assignmentForm.availableFrom} required
                    onChange={event => setAssignmentForm(current => ({ ...current, availableFrom: event.target.value }))} />
                </label>
                <label className="field">
                  <span>Дедлайн</span>
                  <input type="datetime-local" value={assignmentForm.deadline} required
                    onChange={event => setAssignmentForm(current => ({ ...current, deadline: event.target.value }))} />
                </label>
                <button type="submit" className="button primary" disabled={isAssigning || assignableTests.length === 0}>
                  {isAssigning ? <Loader2 className="spin" size={16} /> : <CalendarClock size={16} />}
                  {isAssigning ? "Назначение..." : "Назначить"}
                </button>
              </form>

              {assignableTests.length === 0 && (
                <p className="muted groups-assignment-hint">У преподавателя нет опубликованных версий тестов.</p>
              )}

              <div className="groups-assignment-list">
                {selectedGroup.assignments.length === 0 ? (
                  <div className="groups-empty-assignment">
                    <BookOpen size={22} aria-hidden="true" />
                    <span>Назначений пока нет.</span>
                  </div>
                ) : selectedGroup.assignments.map(assignment => {
                  const assignmentAttempts = attempts.filter(attempt =>
                    attempt.assignmentId === assignment.id);
                  const submittedCount = assignmentAttempts.filter(attempt =>
                    attempt.status === "submitted").length;
                  const inProgressCount = assignmentAttempts.filter(attempt =>
                    attempt.status === "in-progress").length;
                  const attemptedStudentIds = new Set(assignmentAttempts.map(attempt =>
                    attempt.studentUserId));
                  const notStartedCount = selectedGroup.studentUserIds.filter(studentUserId =>
                    !attemptedStudentIds.has(studentUserId)).length;
                  return (
                  <article className="groups-assignment-card" key={assignment.id}>
                    <div>
                      <span className="eyebrow">{assignment.test.subject}</span>
                      <h4>{assignment.test.title}</h4>
                      <p>Версия {assignment.test.versionNumber} · {assignment.test.timeLimitMinutes} мин · {assignment.test.totalPoints} баллов</p>
                      {!isAdmin && (
                        <p>Сдано: {submittedCount} · выполняют: {inProgressCount} · не начинали: {notStartedCount}</p>
                      )}
                    </div>
                    <dl>
                      <div><dt>Открытие</dt><dd>{formatDate(assignment.availableFrom)}</dd></div>
                      <div><dt>Дедлайн</dt><dd>{formatDate(assignment.deadline)}</dd></div>
                    </dl>
                  </article>
                  );
                })}
              </div>
            </section>
          )}
        </main>
      </div>
    </section>
  );
}

export default StudyGroupsPage;
