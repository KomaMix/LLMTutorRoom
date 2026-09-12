import { useLayoutEffect } from "react";
import { School } from "lucide-react";
import { Outlet, useLocation } from "react-router-dom";

export function AuthLayout() {
  const { pathname } = useLocation();

  useLayoutEffect(() => {
    window.scrollTo({ top: 0, left: 0 });
  }, [pathname]);

  return (
    <main className="login-screen">
      <section className="login-panel">
        <div className="brand login-brand">
          <div className="brand-mark">
            <School size={22} aria-hidden="true" />
          </div>
          <div>
            <strong className="login-wordmark">LLMTutorRoom</strong>
          </div>
        </div>
        <Outlet />
      </section>
    </main>
  );
}
