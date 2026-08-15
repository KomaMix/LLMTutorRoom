import { useEffect, useId, useRef } from "react";
import { Loader2, Trash2 } from "lucide-react";

export function ConfirmDialog({
  title,
  description,
  confirmLabel,
  isBusy,
  onCancel,
  onConfirm
}) {
  const titleId = useId();
  const descriptionId = useId();
  const dialogRef = useRef(null);
  const cancelButtonRef = useRef(null);
  const isBusyRef = useRef(isBusy);
  const onCancelRef = useRef(onCancel);
  isBusyRef.current = isBusy;
  onCancelRef.current = onCancel;

  useEffect(() => {
    const previouslyFocusedElement = document.activeElement;
    cancelButtonRef.current?.focus();

    function handleKeyDown(event) {
      if (event.key === "Escape" && !isBusyRef.current) {
        event.preventDefault();
        onCancelRef.current();
        return;
      }

      if (event.key !== "Tab") {
        return;
      }

      const focusableElements = dialogRef.current?.querySelectorAll(
        "button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex='-1'])"
      );
      if (!focusableElements?.length) {
        event.preventDefault();
        dialogRef.current?.focus();
        return;
      }

      const firstElement = focusableElements[0];
      const lastElement = focusableElements[focusableElements.length - 1];
      if (event.shiftKey && document.activeElement === firstElement) {
        event.preventDefault();
        lastElement.focus();
      } else if (!event.shiftKey && document.activeElement === lastElement) {
        event.preventDefault();
        firstElement.focus();
      }
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      previouslyFocusedElement?.focus?.();
    };
  }, []);

  useEffect(() => {
    if (isBusy) {
      dialogRef.current?.focus();
    } else if (document.activeElement === dialogRef.current) {
      cancelButtonRef.current?.focus();
    }
  }, [isBusy]);

  return (
    <div
      className="modal-backdrop"
      role="presentation"
      onMouseDown={event => {
        if (event.target === event.currentTarget && !isBusy) {
          onCancel();
        }
      }}
    >
      <section
        ref={dialogRef}
        className="confirm-dialog"
        role="dialog"
        tabIndex={-1}
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
      >
        <div className="confirm-dialog-icon">
          <Trash2 size={20} aria-hidden="true" />
        </div>
        <div>
          <h2 id={titleId}>{title}</h2>
          <p id={descriptionId}>{description}</p>
        </div>
        <div className="confirm-dialog-actions">
          <button
            ref={cancelButtonRef}
            type="button"
            className="button secondary"
            onClick={onCancel}
            disabled={isBusy}
          >
            Отмена
          </button>
          <button type="button" className="button danger" onClick={onConfirm} disabled={isBusy}>
            {isBusy
              ? <Loader2 className="spin" size={16} aria-hidden="true" />
              : <Trash2 size={16} aria-hidden="true" />}
            {isBusy ? "Удаление..." : confirmLabel}
          </button>
        </div>
      </section>
    </div>
  );
}
