import { useState } from "react";
import { Loader2, LockKeyhole, School, ShieldCheck } from "lucide-react";
import { useAuth } from "./AuthContext.jsx";

export function LoginScreen() {
  const { error, isSigningIn, login } = useAuth();
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");

  return (
    <main className="login-screen">
      <section className="login-panel">
        <div className="brand login-brand">
          <div className="brand-mark">
            <School size={22} aria-hidden="true" />
          </div>
          <div>
            <strong>LLMTutorRoom</strong>
            <span>умная проверка знаний</span>
          </div>
        </div>

        <div>
          <span className="eyebrow">Вход</span>
          <h1>Учебный кабинет</h1>
        </div>

        <div className="preset-users">
          <div className="preset-user">
            <ShieldCheck size={17} aria-hidden="true" />
            <span>Стартовый администратор создается из конфигурации.</span>
          </div>
        </div>

        <form
          className="login-form"
          onSubmit={event => {
            event.preventDefault();
            login({ userName, password });
          }}
        >
          <div className="field">
            <label htmlFor="login-user-name">Логин</label>
            <input
              id="login-user-name"
              autoComplete="username"
              value={userName}
              onChange={event => setUserName(event.target.value)}
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
              onChange={event => setPassword(event.target.value)}
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
      </section>
    </main>
  );
}
