import { Server } from "lucide-react";
import { getRussianPluralForm } from "../../../shared/lib/russianPlural.js";

export function AdminModelAccessHeader({ isLoading, modelCount, teacherCount }) {
  return (
    <header className="admin-page-heading">
      <div className="admin-page-heading-copy">
        <span className="admin-page-icon">
          <Server size={21} aria-hidden="true" />
        </span>
        <div>
          <h2>Доступ и квоты</h2>
          <p>Назначайте преподавателям модели и ограничивайте количество автоматических проверок.</p>
        </div>
      </div>
      <div className="admin-page-summary-group">
        <div className="admin-page-summary">
          <strong>{isLoading ? "—" : teacherCount}</strong>
          <span>{getRussianPluralForm(
            teacherCount,
            "преподаватель",
            "преподавателя",
            "преподавателей"
          )}</span>
        </div>
        <div className="admin-page-summary">
          <strong>{isLoading ? "—" : modelCount}</strong>
          <span>{getRussianPluralForm(modelCount, "модель", "модели", "моделей")}</span>
        </div>
      </div>
    </header>
  );
}
