import { useEffect, useRef, useState } from "react";
import { Loader2, Plus, Server } from "lucide-react";
import * as api from "../../api/modelsApi.js";
import { ConfirmDialog } from "../../shared/ui/ConfirmDialog.jsx";
import { ActionBanner } from "./modelAccess/ActionBanner.jsx";
import { getRequestErrorMessage } from "./requestError.js";
import { ModelEditor } from "./models/ModelEditor.jsx";
import { DeploymentEditor } from "./models/DeploymentEditor.jsx";

export function AdminModels() {
  const [models, setModels] = useState([]);
  const [selectedId, setSelectedId] = useState(null);
  const [creating, setCreating] = useState(false);
  const [addingDeployment, setAddingDeployment] = useState(false);
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [loadVersion, setLoadVersion] = useState(0);
  const [loadError, setLoadError] = useState("");
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const [message, setMessage] = useState(null);
  const [deleting, setDeleting] = useState(null);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setLoadError("");
    api.getModels({ signal: controller.signal }).then(data => {
      if (controller.signal.aborted) return;
      setModels(data); setSelectedId(data[0]?.id ?? null); setCreating(data.length === 0);
    }).catch(error => {
      if (!controller.signal.aborted) setLoadError(getRequestErrorMessage(error, "Не удалось загрузить модели."));
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [loadVersion]);

  const selected = models.find(model => model.id === selectedId);
  const visibleModels = models.filter(model => `${model.displayName} ${model.key}`.toLocaleLowerCase("ru").includes(query.toLocaleLowerCase("ru")));
  function updateModel(model) {
    setModels(current => [...current.filter(item => item.id !== model.id), model]
      .sort((left, right) => left.displayName.localeCompare(right.displayName, "ru")));
  }
  function updateDeployment(deployment) {
    setModels(current => current.map(model => model.id !== deployment.modelId ? model : { ...model,
      deployments: [...model.deployments.filter(item => item.id !== deployment.id), deployment]
        .sort((left, right) => left.priority - right.priority || left.id - right.id) }));
  }
  async function execute(action, successText) {
    if (busyRef.current) return null;
    busyRef.current = true; setBusy(true); setMessage(null);
    try {
      const result = await action();
      if (successText) setMessage({ type: "success", text: successText });
      return result ?? true;
    } catch (error) {
      setMessage({ type: "error", text: getRequestErrorMessage(error, "Не удалось выполнить операцию.", {
        400: "Проверьте заполнение полей и настройки API-ключа.", 404: "Запись больше не существует. Обновите страницу.",
        409: "Этот ключ модели уже используется. Укажите другой ключ." }) });
      return null;
    } finally { busyRef.current = false; setBusy(false); }
  }
  async function saveModel(form) {
    await execute(async () => {
      const model = await api.saveModel(creating ? null : selectedId, form);
      updateModel(model); setSelectedId(model.id); setCreating(false);
    }, "Модель сохранена.");
  }
  async function saveDeployment(id, form) {
    return execute(async () => {
      const deployment = await api.saveDeployment(selectedId, id, form);
      updateDeployment(deployment); setAddingDeployment(false);
      return true;
    }, "Деплоймент сохранен.");
  }
  async function saveLimit(deploymentId, id, form) {
    return execute(async () => {
      const rule = await api.saveRateLimit(deploymentId, id, form);
      setModels(current => current.map(model => ({ ...model, deployments: model.deployments.map(deployment =>
        deployment.id !== deploymentId ? deployment : { ...deployment, rateLimitRules:
          [...deployment.rateLimitRules.filter(item => item.id !== rule.id), rule].sort((a, b) => a.id - b.id) }) })));
      return true;
    }, "Лимит сохранен.");
  }
  async function confirmDelete() {
    const target = deleting;
    await execute(async () => {
      if (target.type === "deployment") await api.deleteDeployment(target.id);
      else await api.deleteRateLimit(target.id);
      setModels(current => current.map(model => ({ ...model, deployments: model.deployments
        .filter(deployment => target.type !== "deployment" || deployment.id !== target.id)
        .map(deployment => ({ ...deployment, rateLimitRules: deployment.rateLimitRules
          .filter(rule => target.type !== "rule" || rule.id !== target.id) })) })));
      return true;
    }, target.type === "deployment" ? "Деплоймент удален." : "Лимит удален.");
    setDeleting(null);
  }
  function selectModel(id) { setSelectedId(id); setCreating(false); setAddingDeployment(false); setMessage(null); }
  const locked = busy || Boolean(deleting);
  return (
    <section className="admin-page admin-models-page">
      <header className="admin-page-heading">
        <div className="admin-page-heading-copy"><span className="admin-page-icon"><Server size={24} aria-hidden="true" /></span>
          <div><h2>Модели и подключения</h2><p>Каталог моделей, деплойменты провайдеров и ограничения запросов.</p></div></div>
        <button type="button" className="button primary" disabled={loading || locked || Boolean(loadError)}
          onClick={() => { setCreating(true); setAddingDeployment(false); setMessage(null); }}><Plus size={16} aria-hidden="true" />Добавить модель</button>
      </header>
      <ActionBanner message={message} />
      {loading ? <div className="panel admin-state"><Loader2 className="spin" aria-hidden="true" />Загрузка моделей...</div>
        : loadError ? <div className="panel admin-state"><p className="form-error" role="alert">{loadError}</p>
          <button className="button secondary" onClick={() => setLoadVersion(value => value + 1)}>Повторить</button></div>
        : <div className="admin-models-layout">
          <aside className="panel admin-panel admin-model-section">
            <label className="field">Поиск модели<input type="search" value={query} onChange={event => setQuery(event.target.value)} /></label>
            <div className="admin-model-directory">
              {visibleModels.map(model => <button type="button" key={model.id} disabled={locked}
                className={`admin-model-row ${!creating && model.id === selectedId ? "selected" : ""}`}
                aria-pressed={!creating && model.id === selectedId} onClick={() => selectModel(model.id)}>
                <strong>{model.displayName}</strong><span>{model.key}</span>
                <small>{model.isArchived ? "В архиве" : `${model.deployments.filter(item => item.isEnabled).length} активных подключений`}</small>
              </button>)}
              {visibleModels.length === 0 && <p className="form-note">{models.length ? "Ничего не найдено." : "Добавьте первую модель."}</p>}
            </div>
          </aside>
          <div className="admin-model-details">
            {(creating || selected) && <ModelEditor key={creating ? "new" : `${selected.id}:${selected.key}:${selected.displayName}:${selected.isArchived}`}
              model={creating ? null : selected} busy={locked} onSave={saveModel} />}
            {!creating && selected && <>
              <div className="admin-model-actions"><h2>Деплойменты</h2>
                <button type="button" className="button secondary" disabled={locked || addingDeployment} onClick={() => setAddingDeployment(true)}>
                  <Plus size={16} aria-hidden="true" />Добавить подключение</button></div>
              {addingDeployment && <DeploymentEditor busy={locked} onSave={saveDeployment} onCancel={() => setAddingDeployment(false)} />}
              {!addingDeployment && selected.deployments.length === 0 && <div className="panel admin-state compact">
                <strong>Подключений пока нет</strong><span>Добавьте хотя бы один активный деплоймент, чтобы модель могла выполнять проверки.</span></div>}
              {selected.deployments.map(deployment => <DeploymentEditor key={`${deployment.id}:${deployment.updatedAt}`}
                deployment={deployment} busy={locked} onSave={saveDeployment}
                onRemove={item => setDeleting({ type: "deployment", id: item.id, name: item.name })}
                onCheck={id => execute(() => api.checkDeployment(id))} onSaveLimit={saveLimit}
                onRemoveLimit={rule => setDeleting({ type: "rule", id: rule.id })} />)}
            </>}
          </div>
        </div>}
      {deleting && <ConfirmDialog title={deleting.type === "deployment" ? "Удалить деплоймент?" : "Удалить лимит?"}
        description={deleting.type === "deployment" ? `Подключение «${deleting.name}» и его лимиты будут удалены. Новые запросы на него поступать не будут.` : "Это ограничение больше не будет применяться к запросам."}
        confirmLabel="Удалить" isBusy={busy} onCancel={() => setDeleting(null)} onConfirm={confirmDelete} />}
    </section>
  );
}
