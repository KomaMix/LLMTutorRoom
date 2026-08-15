import { FileText, Plus } from "lucide-react";

export function EmptyTeacherState({ onOpenTests }) {
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

export default EmptyTeacherState;
