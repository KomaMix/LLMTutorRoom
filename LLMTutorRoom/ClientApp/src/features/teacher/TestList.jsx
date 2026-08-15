import { FileText } from "lucide-react";
import { TestForm } from "./TestForm.jsx";
import { createTestListSummary } from "./teacherTestHelpers.js";

export function TestList({
  form,
  isCreating,
  message,
  models,
  onCreate,
  onFormChange,
  onSelectTest,
  selectedTestId,
  tests
}) {
  return (
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

      <TestForm
        form={form}
        isSubmitting={isCreating}
        message={message}
        mode="create"
        models={models}
        onChange={onFormChange}
        onSubmit={onCreate}
      />
    </div>
  );
}

export default TestList;
