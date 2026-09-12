import { useEffect, useState } from "react";
import {
  BookOpen,
  Check,
  GraduationCap,
  Loader2,
  LockKeyhole
} from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import { useAuth } from "./AuthContext.jsx";

const loginRoles = [
  {
    value: "student",
    label: "Студент",
    description: "Задания, результаты и прогресс",
    icon: GraduationCap
  },
  {
    value: "teacher",
    label: "Преподаватель",
    description: "Тесты, задания и проверки",
    icon: BookOpen
  }
];

function getInitialRole(locationState) {
  if (locationState?.preferredRole === "student") {
    return "student";
  }

  const requestedPath = locationState?.from?.pathname;
  return typeof requestedPath === "string" && requestedPath.startsWith("/student")
    ? "student"
    : "teacher";
}

export function LoginScreen() {
  const location = useLocation();
  const {
    cancelAuthentication,
    clearError,
    error,
    isSigningIn,
    login
  } = useAuth();
  const [selectedRole, setSelectedRole] = useState(() => getInitialRole(location.state));
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  useEffect(() => () => cancelAuthentication(), [cancelAuthentication]);

  function selectRole(role) {
    setSelectedRole(role);
    clearError();
  }

  return (
    <>
      <div className="login-heading">
        <span className="eyebrow">Добро пожаловать</span>
        <h1>Вход в кабинет</h1>
        <p>Выберите свою роль и войдите в учетную запись.</p>
      </div>

      <form
        className="login-form"
        onSubmit={event => {
          event.preventDefault();
          login({ email: email.trim(), password }, selectedRole);
        }}
      >
        <fieldset className="login-role-selector">
          <legend>Кто вы?</legend>
          <div className="login-role-grid">
            {loginRoles.map(role => {
              const Icon = role.icon;
              const isSelected = selectedRole === role.value;
              return (
                <button
                  key={role.value}
                  type="button"
                  className={`login-role-option${isSelected ? " active" : ""}`}
                  aria-pressed={isSelected}
                  onClick={() => selectRole(role.value)}
                >
                  <span className="login-role-icon">
                    <Icon size={21} aria-hidden="true" />
                  </span>
                  <span className="login-role-copy">
                    <strong>{role.label}</strong>
                    <small>{role.description}</small>
                  </span>
                  <span className="login-role-check" aria-hidden="true">
                    {isSelected && <Check size={15} strokeWidth={3} />}
                  </span>
                </button>
              );
            })}
          </div>
        </fieldset>

        <div className="field">
          <label htmlFor="login-email">Email</label>
          <input
            id="login-email"
            type="email"
            autoComplete="username"
            inputMode="email"
            autoCapitalize="none"
            spellCheck={false}
            maxLength={256}
            placeholder="name@example.com"
            value={email}
            onChange={event => {
              setEmail(event.target.value);
              if (error) {
                clearError();
              }
            }}
            required
          />
        </div>
        <div className="field">
          <label htmlFor="login-password">Пароль</label>
          <input
            id="login-password"
            autoComplete="current-password"
            type="password"
            value={password}
            onChange={event => {
              setPassword(event.target.value);
              if (error) {
                clearError();
              }
            }}
            required
          />
        </div>

        {error && <p className="form-error" role="alert">{error}</p>}

        <button type="submit" className="button primary" disabled={isSigningIn}>
          {isSigningIn
            ? <Loader2 className="spin" size={16} aria-hidden="true" />
            : <LockKeyhole size={16} aria-hidden="true" />}
          {isSigningIn ? "Вход..." : "Войти"}
        </button>
      </form>

      {selectedRole === "student" && (
        <p className="auth-switch">
          Нет аккаунта студента?
          <Link to="/register" state={location.state} onClick={clearError}>
            Зарегистрироваться
          </Link>
        </p>
      )}
    </>
  );
}
