import { useRef } from "react";
import { Search, X } from "lucide-react";

export function SearchField({ className = "", inputRef, onClear, ...inputProps }) {
  const fallbackInputRef = useRef(null);
  const fieldRef = inputRef ?? fallbackInputRef;
  const canClear = Boolean(inputProps.value) && onClear && !inputProps.disabled && !inputProps.readOnly;

  return (
    <div className={`search-field${className ? ` ${className}` : ""}`}>
      <Search size={17} aria-hidden="true" />
      <input
        autoComplete="off"
        spellCheck={false}
        {...inputProps}
        ref={fieldRef}
        type="search"
      />
      {canClear && (
        <button
          type="button"
          className="search-field-clear"
          aria-label="Очистить поиск"
          title="Очистить поиск"
          onClick={() => {
            onClear();
            fieldRef.current?.focus();
          }}
        >
          <X size={18} aria-hidden="true" />
        </button>
      )}
    </div>
  );
}
