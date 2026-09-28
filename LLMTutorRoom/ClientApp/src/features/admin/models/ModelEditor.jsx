import { useState } from "react";
import { Save } from "lucide-react";

export function ModelEditor({ model, busy, onSave }) {
  const [form, setForm] = useState({ key: model?.key ?? "", displayName: model?.displayName ?? "",
    description: model?.description ?? "", isArchived: model?.isArchived ?? false });
  const update = (field, value) => setForm(current => ({ ...current, [field]: value }));
  return (
    <section className="panel admin-panel admin-model-section">
      <header className="admin-section-header"><div>
        <h2>{model ? model.displayName : "Новая модель"}</h2>
        <p>{model ? `ID модели: ${model.id}` : "После сохранения можно добавить подключения к провайдерам."}</p>
      </div></header>
      <form className="admin-form" onSubmit={event => { event.preventDefault(); onSave(form); }}>
        <fieldset className="admin-model-fields" disabled={busy}>
          <label className="field">Название<input value={form.displayName} required maxLength={200}
            onChange={event => update("displayName", event.target.value)} /></label>
          <label className="field">Ключ модели<input value={form.key} required maxLength={200}
            placeholder="gemma-12b" onChange={event => update("key", event.target.value)} />
            <small>Уникальное обозначение в каталоге. Его можно менять, ссылки на модель сохранятся.</small></label>
          <label className="field">Описание<textarea value={form.description} rows={3} maxLength={2000}
            onChange={event => update("description", event.target.value)} /></label>
          <label className="admin-switch"><input type="checkbox" checked={form.isArchived}
            onChange={event => update("isArchived", event.target.checked)} /><span aria-hidden="true" /><div>
            <strong>Модель в архиве</strong><small>Скрывает модель из нового выбора. Уже назначенные проверки продолжают работать.</small>
          </div></label>
          <button className="button primary" type="submit"><Save size={16} aria-hidden="true" />
            {busy ? "Сохранение..." : model ? "Сохранить модель" : "Создать модель"}</button>
        </fieldset>
      </form>
    </section>
  );
}
