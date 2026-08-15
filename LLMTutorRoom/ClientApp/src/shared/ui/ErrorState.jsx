export function ErrorState({ message, onRetry, children }) {
  return (
    <div className="screen-state" role="alert">
      <span>{message}</span>
      {onRetry && (
        <button type="button" className="button primary" onClick={onRetry}>
          Повторить
        </button>
      )}
      {children}
    </div>
  );
}
