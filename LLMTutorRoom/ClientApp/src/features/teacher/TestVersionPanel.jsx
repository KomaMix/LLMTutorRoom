import { useEffect, useState } from "react";
import { GitBranchPlus, History, Rocket, Trash2 } from "lucide-react";
import { ConfirmDialog } from "../../shared/ui/ConfirmDialog.jsx";
import { StatusBadge } from "../../shared/ui/StatusBadge.jsx";

export function TestVersionPanel({
  isBusy,
  message,
  onCreateDraft,
  onDeleteDraft,
  onPublish,
  onSelectVersion,
  test
}) {
  const [confirmation, setConfirmation] = useState("");
  const versions = [...(test.versions ?? [])]
    .sort((left, right) => right.versionNumber - left.versionNumber);
  const draft = versions.find(version => version.status === "draft") ?? null;
  const isDraft = test.status === "draft";
  const deletesEntireTest = isDraft
    && versions.length === 1
    && !test.publishedVersionNumber;

  useEffect(() => {
    setConfirmation("");
  }, [test.id, test.versionNumber]);

  async function confirmVersionAction() {
    let shouldClose = true;
    if (confirmation === "publish") {
      shouldClose = await onPublish();
    } else if (confirmation === "delete") {
      shouldClose = await onDeleteDraft();
    }

    if (shouldClose !== false) {
      setConfirmation("");
    }
  }

  return (
    <section className="version-panel teacher-version-panel" aria-busy={isBusy}>
      <header className="teacher-section-header teacher-version-header">
        <div>
          <span className="eyebrow">Версии</span>
          <h3>История публикаций</h3>
        </div>
        <div className="teacher-version-context">
          <History size={16} aria-hidden="true" />
          <span>Открыта версия {test.versionNumber}</span>
          {test.publishedVersionNumber && test.publishedVersionNumber !== test.versionNumber && (
            <span>· опубликована версия {test.publishedVersionNumber}</span>
          )}
        </div>
      </header>

      <div className="version-list" aria-label="История версий теста">
        {versions.map(version => (
          <button
            type="button"
            className={version.versionNumber === test.versionNumber ? "active" : ""}
            aria-current={version.versionNumber === test.versionNumber ? "page" : undefined}
            disabled={isBusy}
            key={version.versionNumber}
            onClick={() => onSelectVersion(version.versionNumber)}
          >
            <span>Версия {version.versionNumber}</span>
            <StatusBadge status={version.status} variant="inline" />
          </button>
        ))}
      </div>

      <div className="version-actions teacher-version-actions">
        {isDraft ? (
          <>
            <button
              type="button"
              className="button primary"
              disabled={isBusy}
              onClick={() => setConfirmation("publish")}
            >
              <Rocket size={16} aria-hidden="true" />
              Опубликовать версию {test.versionNumber}
            </button>
            <button
              type="button"
              className="button danger"
              disabled={isBusy}
              onClick={() => setConfirmation("delete")}
            >
              <Trash2 size={16} aria-hidden="true" />
              Удалить черновик
            </button>
          </>
        ) : draft ? (
          <button
            type="button"
            className="button secondary"
            disabled={isBusy}
            onClick={() => onSelectVersion(draft.versionNumber)}
          >
            <History size={16} aria-hidden="true" />
            Открыть черновик версии {draft.versionNumber}
          </button>
        ) : (
          <button
            type="button"
            className="button primary"
            disabled={isBusy || test.status !== "published"}
            onClick={onCreateDraft}
          >
            <GitBranchPlus size={16} aria-hidden="true" />
            Создать новую версию
          </button>
        )}
      </div>

      {!isDraft && (
        <p className="muted version-readonly-note">
          Опубликованные и предыдущие версии доступны только для чтения.
        </p>
      )}
      <div className="teacher-version-message" role="status" aria-atomic="true" aria-live="polite">
        {message && <p className="form-note">{message}</p>}
      </div>

      {confirmation && (
        <ConfirmDialog
          title={confirmation === "publish"
            ? `Опубликовать версию ${test.versionNumber}?`
            : deletesEntireTest
              ? "Удалить весь тест?"
              : `Удалить черновик версии ${test.versionNumber}?`}
          description={confirmation === "publish"
            ? "Текущая опубликованная версия станет предыдущей. Начатые по ней попытки продолжат работать."
            : deletesEntireTest
              ? "Это единственная версия. Вместе с черновиком будет полностью удалён весь тест. Действие нельзя отменить."
              : "Будет удалён только неопубликованный черновик. Опубликованная версия сохранится. Действие нельзя отменить."}
          confirmLabel={confirmation === "publish"
            ? "Опубликовать"
            : deletesEntireTest ? "Удалить тест" : "Удалить черновик"}
          busyLabel={confirmation === "publish" ? "Публикация..." : "Удаление..."}
          icon={confirmation === "publish" ? Rocket : Trash2}
          isBusy={isBusy}
          onCancel={() => setConfirmation("")}
          onConfirm={confirmVersionAction}
          variant={confirmation === "publish" ? "primary" : "danger"}
        />
      )}
    </section>
  );
}

export default TestVersionPanel;
