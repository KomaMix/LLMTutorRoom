import { useEffect, useState } from "react";
import { ChevronDown, FileText, Plus } from "lucide-react";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";
import { TestForm } from "./TestForm.jsx";
import { createTestListSummary } from "./teacherTestHelpers.js";

export function TestList({
  form,
  hasUnsavedChanges,
  isBusy,
  isCreating,
  message,
  models,
  onCreate,
  onFormChange,
  onSelectTest,
  selectedTestId,
  tests
}) {
  const [isCreateOpen, setIsCreateOpen] = useState(tests.length === 0);

  useEffect(() => {
    if (message === "Тест создан.") {
      setIsCreateOpen(false);
    }
  }, [message]);

  return (
    <aside className="list-panel teacher-test-sidebar">
      <header className="teacher-section-header teacher-test-list-header">
        <div>
          <span className="eyebrow">Учебные материалы</span>
          <h2>Тесты</h2>
        </div>
        <span className="teacher-list-count" aria-label={`Тестов: ${tests.length}`}>
          {tests.length}
        </span>
      </header>

      <div className="select-list teacher-test-list">
        {tests.map(test => (
          <button
            key={test.id}
            type="button"
            className={test.id === selectedTestId ? "active" : ""}
            aria-current={test.id === selectedTestId ? "page" : undefined}
            disabled={isBusy}
            onClick={() => onSelectTest(test.id)}
          >
            <span className="teacher-test-list-primary">
              <strong>{test.title}</strong>
              <StatusBadge status={test.status} />
            </span>
            <small>{createTestListSummary(test)}</small>
          </button>
        ))}
      </div>

      {tests.length === 0 && (
        <div className="teacher-test-list-empty">
          <FileText size={20} aria-hidden="true" />
          <span>Создайте первый тест и добавьте в него задания.</span>
        </div>
      )}

      <section className={`teacher-create-test${isCreateOpen ? " open" : ""}`}>
        <button
          type="button"
          className="teacher-create-test-toggle"
          aria-expanded={isCreateOpen}
          onClick={() => setIsCreateOpen(current => !current)}
        >
          <span className="teacher-create-test-icon">
            <Plus size={17} aria-hidden="true" />
          </span>
          <span>
            <strong>Новый тест</strong>
            <small>
              {isCreateOpen
                ? "Свернуть форму"
                : hasUnsavedChanges
                  ? "Есть несохранённые данные"
                  : "Открыть форму создания"}
            </small>
          </span>
          <ChevronDown size={17} aria-hidden="true" />
        </button>

        {isCreateOpen && (
          <div className="teacher-create-test-body">
            <TestForm
              form={form}
              isDisabled={isBusy}
              isSubmitting={isCreating}
              message={message === "Тест создан." ? "" : message}
              mode="create"
              models={models}
              onChange={onFormChange}
              onSubmit={onCreate}
            />
          </div>
        )}
      </section>
    </aside>
  );
}

export default TestList;
