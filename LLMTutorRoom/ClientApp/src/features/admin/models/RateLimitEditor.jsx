import { useEffect, useState } from "react";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { formatPeriod } from "../../../shared/lib/dates.js";

export function RateLimitEditor({ deployment, busy, onSave, onRemove }) {
  const [editingId, setEditingId] = useState(null);
  const [form, setForm] = useState({ windowSeconds: 60, maxRequests: 10 });
  function reset() { setEditingId(null); setForm({ windowSeconds: 60, maxRequests: 10 }); }
  useEffect(() => {
    if (editingId && !deployment.rateLimitRules.some(rule => rule.id === editingId)) {
      setEditingId(null);
      setForm({ windowSeconds: 60, maxRequests: 10 });
    }
  }, [deployment.rateLimitRules, editingId]);
  async function submit(event) {
    event.preventDefault();
    if (await onSave(deployment.id, editingId, { windowSeconds: Number(form.windowSeconds), maxRequests: Number(form.maxRequests) })) reset();
  }
  return (
    <section className="admin-model-limits" aria-label="Ограничения запросов">
      <h3>Лимиты запросов</h3>
      <p className="form-note">Все правила действуют одновременно. Без правил частота запросов не ограничена.</p>
      {deployment.rateLimitRules.map(rule => (
        <div className="admin-model-rule" key={rule.id}>
          <span>{rule.maxRequests} запросов за {formatPeriod(rule.windowSeconds)}</span>
          <div className="admin-model-actions">
            <button type="button" className="icon-button" aria-label={`Изменить лимит ${rule.id}`} disabled={busy}
              onClick={() => { setEditingId(rule.id); setForm({ windowSeconds: rule.windowSeconds, maxRequests: rule.maxRequests }); }}>
              <Pencil size={16} aria-hidden="true" /></button>
            <button type="button" className="icon-button danger" aria-label={`Удалить лимит ${rule.id}`} disabled={busy}
              onClick={() => onRemove(rule)}><Trash2 size={16} aria-hidden="true" /></button>
          </div>
        </div>
      ))}
      <form className="admin-form" onSubmit={submit}>
        <fieldset className="admin-model-fields" disabled={busy}>
          <div className="admin-model-field-row">
            <label className="field">Запросов<input type="number" min={1} max={1000000} step={1} required value={form.maxRequests}
              onChange={event => setForm(current => ({ ...current, maxRequests: event.target.value }))} /></label>
            <label className="field">За период, секунд<input type="number" min={1} max={86400} step={1} required value={form.windowSeconds}
              onChange={event => setForm(current => ({ ...current, windowSeconds: event.target.value }))} /></label>
          </div>
          <div className="admin-model-actions">
            <button type="submit" className="button secondary"><Plus size={16} aria-hidden="true" />
              {editingId ? "Сохранить лимит" : "Добавить лимит"}</button>
            {editingId && <button type="button" className="button secondary" onClick={reset}>Отмена</button>}
          </div>
          {editingId && <small className="form-note">Изменение длительности периода начинает новый счетчик этого правила.</small>}
        </fieldset>
      </form>
    </section>
  );
}
