import { useEffect, useState } from "react";
import { ArrowLeft, Loader2, UserPlus } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import { useAuth } from "./AuthContext.jsx";

export function RegisterScreen() {
  const location = useLocation();
  const {
    clearError,
    cancelAuthentication,
    error,
    isRegistering,
    registerStudent
  } = useAuth();
  const [userName, setUserName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [passwordConfirmation, setPasswordConfirmation] = useState("");
  const [validationError, setValidationError] = useState("");

  useEffect(() => () => cancelAuthentication(), [cancelAuthentication]);

  function updateField(setValue, value) {
    setValue(value);
    setValidationError("");
    if (error) {
      clearError();
    }
  }

  function handleSubmit(event) {
    event.preventDefault();

    if (password !== passwordConfirmation) {
      setValidationError("Пароли не совпадают.");
      return;
    }

    registerStudent({
      userName: userName.trim(),
      email: email.trim(),
      password
    });
  }

  const displayedError = validationError || error;

  return (
    <>
      <div className="login-heading">
        <span className="eyebrow">Новый аккаунт</span>
        <h1>Регистрация студента</h1>
        <p>Создайте учетную запись и сразу перейдите к доступным заданиям.</p>
      </div>

      <form className="login-form" onSubmit={handleSubmit}>
        <div className="field">
          <label htmlFor="register-user-name">Имя пользователя</label>
          <input
            id="register-user-name"
            autoComplete="username"
            maxLength={64}
            pattern="\S+"
            placeholder="Например, IvanPetrov"
            value={userName}
            onChange={event => updateField(setUserName, event.target.value)}
            required
          />
          <small>До 64 символов, без пробелов.</small>
        </div>

        <div className="field">
          <label htmlFor="register-email">Email</label>
          <input
            id="register-email"
            type="email"
            autoComplete="email"
            inputMode="email"
            autoCapitalize="none"
            spellCheck={false}
            maxLength={256}
            placeholder="name@example.com"
            value={email}
            onChange={event => updateField(setEmail, event.target.value)}
            required
          />
        </div>

        <div className="field">
          <label htmlFor="register-password">Пароль</label>
          <input
            id="register-password"
            type="password"
            autoComplete="new-password"
            minLength={6}
            maxLength={128}
            placeholder="Не менее 6 символов"
            value={password}
            onChange={event => updateField(setPassword, event.target.value)}
            required
          />
        </div>

        <div className="field">
          <label htmlFor="register-password-confirmation">Повторите пароль</label>
          <input
            id="register-password-confirmation"
            type="password"
            autoComplete="new-password"
            minLength={6}
            maxLength={128}
            aria-invalid={Boolean(validationError)}
            aria-describedby={validationError ? "register-password-error" : undefined}
            value={passwordConfirmation}
            onChange={event => updateField(setPasswordConfirmation, event.target.value)}
            required
          />
        </div>

        {displayedError && (
          <p
            id={validationError ? "register-password-error" : undefined}
            className="form-error"
            role="alert"
          >
            {displayedError}
          </p>
        )}

        <button type="submit" className="button primary" disabled={isRegistering}>
          {isRegistering
            ? <Loader2 className="spin" size={16} aria-hidden="true" />
            : <UserPlus size={16} aria-hidden="true" />}
          {isRegistering ? "Создание аккаунта..." : "Создать аккаунт"}
        </button>
      </form>

      <p className="auth-switch">
        Уже есть аккаунт?
        <Link
          to="/login"
          state={{ from: location.state?.from, preferredRole: "student" }}
          onClick={clearError}
        >
          <ArrowLeft size={14} aria-hidden="true" />
          Войти
        </Link>
      </p>
    </>
  );
}
