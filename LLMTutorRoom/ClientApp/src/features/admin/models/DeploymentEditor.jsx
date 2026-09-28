import { useState } from "react";
import { PlugZap, Save, Trash2 } from "lucide-react";
import { formatDate } from "../../../shared/lib/dates.js";
import { RateLimitEditor } from "./RateLimitEditor.jsx";

export function DeploymentEditor({ deployment, busy, onSave, onRemove, onCheck, onSaveLimit, onRemoveLimit, onCancel }) {
  const [form, setForm] = useState({ name: deployment?.name ?? "", endpoint: deployment?.endpoint ?? "",
    providerModelId: deployment?.providerModelId ?? "", isEnabled: deployment?.isEnabled ?? true,
    priority: deployment?.priority ?? 0, maxConcurrentRequests: deployment?.maxConcurrentRequests ?? "", apiKey: "", secretAction: "keep" });
  const [check, setCheck] = useState(null);
  const update = (field, value) => setForm(current => ({ ...current, [field]: value }));
  async function submit(event) {
    event.preventDefault();
    const payload = { name: form.name, endpoint: form.endpoint, providerModelId: form.providerModelId,
      isEnabled: form.isEnabled, priority: Number(form.priority),
      maxConcurrentRequests: form.maxConcurrentRequests === "" ? null : Number(form.maxConcurrentRequests),
      apiKey: (!deployment || form.secretAction === "replace") && form.apiKey ? form.apiKey : null,
      clearApiKey: Boolean(deployment) && form.secretAction === "clear" };
    if (await onSave(deployment?.id, payload)) {
      update("apiKey", ""); update("secretAction", "keep"); setCheck(null);
    }
  }
  return (
    <article className="panel admin-panel admin-model-section">
      <header className="admin-section-header"><div>
        <h2>{deployment ? deployment.name : "Новый деплоймент"}</h2>
        <p>{deployment ? `ID: ${deployment.id} · ${deployment.isEnabled ? "Прием запросов включен" : "Прием запросов выключен"}` : "Подключение к API, совместимому с OpenAI."}</p>
      </div></header>
      <form className="admin-form" onSubmit={submit}>
        <fieldset className="admin-model-fields" disabled={busy}>
          <label className="field">Название подключения<input value={form.name} required maxLength={200}
            placeholder="Локальный Ollama" onChange={event => update("name", event.target.value)} /></label>
          <label className="field">Адрес API<input type="url" value={form.endpoint} required maxLength={2000}
            placeholder="http://localhost:11434/v1" onChange={event => update("endpoint", event.target.value)} /></label>
          <label className="field">ID модели у провайдера<input value={form.providerModelId} required maxLength={300}
            placeholder="gemma3:12b" onChange={event => update("providerModelId", event.target.value)} />
            <small>Точное имя в каталоге провайдера. Для другой модели создайте отдельную запись в каталоге.</small></label>
          {deployment && <label className="field">API-ключ
            <select value={form.secretAction} onChange={event => update("secretAction", event.target.value)}>
              <option value="keep">{deployment.hasApiKey ? "Оставить сохраненный ключ" : "Оставить без ключа"}</option>
              <option value="replace">Задать новый ключ</option><option value="clear">Удалить ключ</option>
            </select></label>}
          {(!deployment || form.secretAction === "replace") && <label className="field">{deployment ? "Новый API-ключ" : "API-ключ (если нужен)"}
            <input type="password" autoComplete="new-password" maxLength={4096} required={Boolean(deployment)} value={form.apiKey}
              onChange={event => update("apiKey", event.target.value)} /></label>}
          <div className="admin-model-field-row">
            <label className="field">Приоритет<input type="number" min={0} max={2147483647} step={1} required value={form.priority}
              onChange={event => update("priority", event.target.value)} /><small>Меньшее число выбирается первым.</small></label>
            <label className="field">Одновременных запросов<input type="number" min={1} max={2147483647} step={1} value={form.maxConcurrentRequests}
              placeholder="Без ограничения" onChange={event => update("maxConcurrentRequests", event.target.value)} /><small>Пустое поле означает отсутствие лимита.</small></label>
          </div>
          <label className="admin-switch"><input type="checkbox" checked={form.isEnabled}
            onChange={event => update("isEnabled", event.target.checked)} /><span aria-hidden="true" /><div>
            <strong>Принимать новые запросы</strong><small>Отключение не прерывает уже начатую генерацию.</small>
          </div></label>
          <div className="admin-model-actions">
            <button type="submit" className="button primary"><Save size={16} aria-hidden="true" />{deployment ? "Сохранить деплоймент" : "Добавить деплоймент"}</button>
            {onCancel && <button type="button" className="button secondary" onClick={onCancel}>Отмена</button>}
          </div>
        </fieldset>
      </form>
      {deployment && <>
        <div className="admin-model-actions admin-model-divider">
          <button type="button" className="button secondary" disabled={busy} onClick={async () => setCheck(await onCheck(deployment.id))}>
            <PlugZap size={16} aria-hidden="true" />Проверить подключение</button>
          <button type="button" className="button danger" disabled={busy} onClick={() => onRemove(deployment)}>
            <Trash2 size={16} aria-hidden="true" />Удалить</button>
        </div>
        <p className="form-note">Проверка использует сохраненные настройки и запрашивает каталог моделей без генерации текста.</p>
        {check && <div className={`admin-action-banner ${check.isAvailable ? "success" : "error"}`} role="status">
          <p>{check.isAvailable ? "Подключение доступно, модель найдена." : `Подключение недоступно: ${check.message}`}
            <br />{formatDate(check.checkedAt)} · {check.elapsedMilliseconds} мс</p>
        </div>}
        <RateLimitEditor deployment={deployment} busy={busy} onSave={onSaveLimit} onRemove={onRemoveLimit} />
      </>}
    </article>
  );
}
