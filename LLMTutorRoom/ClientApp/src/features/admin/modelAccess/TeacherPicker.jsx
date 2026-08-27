import { useEffect, useId, useRef, useState } from "react";
import { Check, Search, UserRound, X } from "lucide-react";

function matchesQuery(teacher, normalizedQuery) {
  return String(teacher.userName ?? "").toLocaleLowerCase("ru-RU").includes(normalizedQuery)
    || String(teacher.email ?? "").toLocaleLowerCase("ru-RU").includes(normalizedQuery);
}

export function TeacherPicker({
  isMutationLocked,
  onSelect,
  selectedTeacher,
  selectedTeacherId,
  teachers
}) {
  const [query, setQuery] = useState("");
  const [isOpen, setIsOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRef = useRef(null);
  const activeOptionRef = useRef(null);
  const listboxId = useId();
  const normalizedQuery = query.trim().toLocaleLowerCase("ru-RU");
  const filteredTeachers = normalizedQuery
    ? teachers.filter(teacher => matchesQuery(teacher, normalizedQuery))
    : teachers;
  const isDisabled = teachers.length === 0 || isMutationLocked;
  const isListOpen = isOpen && !isDisabled;
  const highlightedIndex = Math.min(activeIndex, Math.max(filteredTeachers.length - 1, 0));

  useEffect(() => {
    if (isListOpen) {
      activeOptionRef.current?.scrollIntoView({ block: "nearest" });
    }
  }, [highlightedIndex, isListOpen, normalizedQuery]);

  function updateQuery(value) {
    setQuery(value);
    setActiveIndex(0);
    setIsOpen(true);
  }

  function chooseTeacher(teacher) {
    if (teacher.id === selectedTeacherId) {
      setQuery("");
      setIsOpen(false);
      setActiveIndex(0);
      inputRef.current?.focus();
      return;
    }

    if (onSelect(teacher.id) === false) {
      inputRef.current?.focus();
      return;
    }

    setQuery("");
    setIsOpen(false);
    setActiveIndex(0);
    inputRef.current?.focus();
  }

  function handleKeyDown(event) {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setIsOpen(true);
      if (filteredTeachers.length > 0) {
        setActiveIndex(current => Math.min(current + 1, filteredTeachers.length - 1));
      }
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      setIsOpen(true);
      setActiveIndex(current => Math.max(current - 1, 0));
      return;
    }

    if (event.key === "Enter" && isListOpen && filteredTeachers[highlightedIndex]) {
      event.preventDefault();
      chooseTeacher(filteredTeachers[highlightedIndex]);
      return;
    }

    if (event.key === "Escape") {
      setQuery("");
      setIsOpen(false);
      setActiveIndex(0);
    }
  }

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
        <label htmlFor="access-teacher-search">Найти преподавателя</label>
        <div
          className="admin-teacher-combobox"
          onBlur={event => {
            if (!event.currentTarget.contains(event.relatedTarget)) {
              setIsOpen(false);
            }
          }}
        >
          <div className="admin-search admin-teacher-search">
            <Search size={17} aria-hidden="true" />
            <input
              id="access-teacher-search"
              ref={inputRef}
              type="search"
              role="combobox"
              aria-autocomplete="list"
              aria-controls={isListOpen ? listboxId : undefined}
              aria-expanded={isListOpen}
              aria-activedescendant={
                isListOpen && filteredTeachers.length > 0
                  ? `${listboxId}-option-${highlightedIndex}`
                  : undefined
              }
              value={query}
              placeholder={teachers.length === 0 ? "Нет преподавателей" : "Имя пользователя или email"}
              autoComplete="off"
              spellCheck={false}
              disabled={isDisabled}
              onChange={event => updateQuery(event.target.value)}
              onFocus={() => {
                const selectedIndex = filteredTeachers
                  .findIndex(teacher => teacher.id === selectedTeacherId);
                setActiveIndex(Math.max(selectedIndex, 0));
                setIsOpen(true);
              }}
              onKeyDown={handleKeyDown}
            />
            {query && !isDisabled && (
              <button
                type="button"
                className="admin-search-clear"
                aria-label="Очистить поиск"
                title="Очистить поиск"
                onClick={() => {
                  updateQuery("");
                  inputRef.current?.focus();
                }}
              >
                <X size={18} aria-hidden="true" />
              </button>
            )}
          </div>

          {isListOpen && (
            <div
              className="admin-teacher-options"
              id={listboxId}
              role={filteredTeachers.length > 0 ? "listbox" : undefined}
            >
              {filteredTeachers.length === 0 ? (
                <p className="admin-teacher-options-empty" role="status">
                  Преподаватель не найден
                </p>
              ) : (
                filteredTeachers.map((teacher, index) => (
                  <div
                    id={`${listboxId}-option-${index}`}
                    ref={index === highlightedIndex ? activeOptionRef : undefined}
                    key={teacher.id}
                    className={`admin-teacher-option${index === highlightedIndex ? " highlighted" : ""}`}
                    role="option"
                    aria-selected={teacher.id === selectedTeacherId}
                    onMouseDown={event => event.preventDefault()}
                    onMouseEnter={() => setActiveIndex(index)}
                    onClick={() => chooseTeacher(teacher)}
                  >
                    <span>
                      <strong>{teacher.userName}</strong>
                      <small>{teacher.email}</small>
                    </span>
                    {teacher.id === selectedTeacherId && <Check size={17} aria-hidden="true" />}
                  </div>
                ))
              )}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
