import { UserRound } from "lucide-react";

export function TeacherPicker({
  isMutationLocked,
  onSelect,
  selectedTeacher,
  selectedTeacherId,
  teachers
}) {
  return (
    <section className="panel admin-panel admin-teacher-picker">
      <div className="admin-teacher-picker-copy">
        <span className="admin-section-icon">
          <UserRound size={19} aria-hidden="true" />
        </span>
        <div>
          <h2>Преподаватель</h2>
          <p>
            {selectedTeacher
              ? `${selectedTeacher.userName} · ${selectedTeacher.email}`
              : "Сначала создайте учётную запись преподавателя."}
          </p>
        </div>
      </div>
      <div className="field admin-teacher-select">
        <label htmlFor="access-teacher">Выбрать преподавателя</label>
        <select
          id="access-teacher"
          value={selectedTeacherId}
          onChange={event => onSelect(event.target.value)}
          disabled={teachers.length === 0 || isMutationLocked}
        >
          {teachers.length === 0 && <option value="">Нет преподавателей</option>}
          {teachers.map(teacher => (
            <option key={teacher.id} value={teacher.id}>
              {teacher.userName} · {teacher.email}
            </option>
          ))}
        </select>
      </div>
    </section>
  );
}
