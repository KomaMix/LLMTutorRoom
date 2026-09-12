import { useEffect, useRef, useState } from "react";
import {
  GraduationCap,
  LogOut,
  PanelLeft,
  School,
  ShieldCheck,
  UserRoundCheck,
  X
} from "lucide-react";
import { NavLink, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext.jsx";
import { statusText } from "../shared/config/status.js";
import { getPageTitle } from "../shared/lib/roles.js";
import {
  NavigationGuardProvider,
  useNavigationGuard
} from "./NavigationGuardContext.jsx";
import { navigation } from "./navigation.js";

const roleIcons = {
  admin: ShieldCheck,
  teacher: UserRoundCheck,
  student: GraduationCap
};

const rolePageTitles = {
  admin: {
    "/admin/teachers": "Преподаватели",
    "/admin/model-access": "Доступ к моделям"
  },
  teacher: {
    "/teacher/dashboard": "Обзор",
    "/teacher/tests": "Тесты",
    "/teacher/reviews": "Проверки учеников",
    "/teacher/models": "Модели проверки"
  }
};

function AppShellContent({ currentUser, role, children }) {
  const { isPreparingLogout, requestLogout } = useNavigationGuard();
  const { pathname } = useLocation();
  const hasResponsiveSidebar = role === "teacher" || role === "admin";
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(() => (
    hasResponsiveSidebar
      && typeof window !== "undefined"
      && typeof window.matchMedia === "function"
      && window.matchMedia("(max-width: 1120px)").matches
  ));
  const [isSidebarNarrow, setIsSidebarNarrow] = useState(() => (
    hasResponsiveSidebar
      && typeof window !== "undefined"
      && typeof window.matchMedia === "function"
      && window.matchMedia("(max-width: 1120px)").matches
  ));
  const sidebarRef = useRef(null);
  const sidebarCloseRef = useRef(null);
  const sidebarToggleRef = useRef(null);
  const RoleIcon = roleIcons[role];
  const activeNavigation = navigation[role] ?? [];
  const shellClassName = `app-shell role-${role}${isSidebarCollapsed ? " sidebar-collapsed" : ""}`;
  const currentPageTitles = rolePageTitles[role] ?? {};
  const rolePagePath = Object.keys(currentPageTitles)
    .find(path => pathname.startsWith(path));
  const pageTitle = rolePagePath
    ? currentPageTitles[rolePagePath]
    : getPageTitle(role);
  const isSidebarDrawerOpen = hasResponsiveSidebar
    && isSidebarNarrow
    && !isSidebarCollapsed;

  useEffect(() => {
    if (hasResponsiveSidebar) {
      window.scrollTo({ top: 0, left: 0 });
    }
  }, [hasResponsiveSidebar, pathname]);

  useEffect(() => {
    if (!hasResponsiveSidebar || typeof window.matchMedia !== "function") {
      return;
    }

    const mediaQuery = window.matchMedia("(max-width: 1120px)");
    function handleBreakpointChange(event) {
      setIsSidebarNarrow(event.matches);
      if (event.matches) {
        setIsSidebarCollapsed(true);
      }
    }

    setIsSidebarNarrow(mediaQuery.matches);
    mediaQuery.addEventListener("change", handleBreakpointChange);
    return () => mediaQuery.removeEventListener("change", handleBreakpointChange);
  }, [hasResponsiveSidebar]);

  useEffect(() => {
    if (
      !hasResponsiveSidebar
      || isSidebarCollapsed
      || !isSidebarNarrow
    ) {
      return;
    }

    const sidebar = sidebarRef.current;
    const sidebarToggle = sidebarToggleRef.current;
    const focusFrame = window.requestAnimationFrame(() => {
      sidebarCloseRef.current?.focus();
    });

    function handleDrawerKeyDown(event) {
      if (event.key === "Escape") {
        event.preventDefault();
        setIsSidebarCollapsed(true);
        return;
      }

      if (event.key !== "Tab" || !sidebar) {
        return;
      }

      const focusableElements = [...sidebar.querySelectorAll(
        "a[href], button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled)"
      )].filter(element => element.getClientRects().length > 0);
      const firstElement = focusableElements[0];
      const lastElement = focusableElements.at(-1);
      if (!firstElement || !lastElement) {
        return;
      }

      if (event.shiftKey && document.activeElement === firstElement) {
        event.preventDefault();
        lastElement.focus();
      } else if (!event.shiftKey && document.activeElement === lastElement) {
        event.preventDefault();
        firstElement.focus();
      }
    }

    document.addEventListener("keydown", handleDrawerKeyDown);
    return () => {
      window.cancelAnimationFrame(focusFrame);
      document.removeEventListener("keydown", handleDrawerKeyDown);
      window.requestAnimationFrame(() => sidebarToggle?.focus());
    };
  }, [hasResponsiveSidebar, isSidebarCollapsed, isSidebarNarrow]);

  useEffect(() => {
    if (!isSidebarDrawerOpen) {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [isSidebarDrawerOpen]);

  function handleNavigation() {
    if (
      hasResponsiveSidebar
      && isSidebarNarrow
    ) {
      setIsSidebarCollapsed(true);
    }
  }

  const sidebarToggle = (
    <button
      type="button"
      ref={sidebarToggleRef}
      className="icon-button"
      title={isSidebarCollapsed ? "Развернуть меню" : "Свернуть меню"}
      aria-label={isSidebarCollapsed ? "Развернуть меню" : "Свернуть меню"}
      aria-controls="application-navigation"
      aria-expanded={!isSidebarCollapsed}
      onClick={() => setIsSidebarCollapsed(current => !current)}
    >
      <PanelLeft size={18} aria-hidden="true" />
    </button>
  );

  return (
    <div className={shellClassName}>
      <aside
        id="application-navigation"
        ref={sidebarRef}
        className={isSidebarCollapsed ? "sidebar collapsed" : "sidebar"}
      >
        <div className="brand">
          <div className="brand-mark">
            <School size={22} aria-hidden="true" />
          </div>
          <div className="sidebar-label">
            <strong>LLMTutorRoom</strong>
          </div>
          {hasResponsiveSidebar && (
            <button
              type="button"
              ref={sidebarCloseRef}
              className="workspace-sidebar-close"
              aria-label="Закрыть меню"
              onClick={() => setIsSidebarCollapsed(true)}
            >
              <X size={20} aria-hidden="true" />
            </button>
          )}
        </div>

        <div className="user-card">
          <div className="user-avatar">
            <RoleIcon size={18} aria-hidden="true" />
          </div>
          <div className="sidebar-label">
            <strong>{currentUser.userName}</strong>
            <span>{currentUser.email}</span>
          </div>
        </div>

        <nav className="nav-list" aria-label="Разделы">
          {activeNavigation.map(item => {
            const Icon = item.icon;
            return (
              <NavLink
                key={item.path}
                to={item.path}
                className={({ isActive }) => isActive ? "active" : undefined}
                title={isSidebarCollapsed ? item.label : undefined}
                onClick={handleNavigation}
              >
                <Icon size={18} aria-hidden="true" />
                <span className="sidebar-label">{item.label}</span>
              </NavLink>
            );
          })}
        </nav>
      </aside>

      {isSidebarDrawerOpen && (
        <button
          type="button"
          className="workspace-sidebar-backdrop"
          aria-label="Закрыть меню"
          onClick={() => setIsSidebarCollapsed(true)}
        />
      )}

      <main className="workspace" inert={isSidebarDrawerOpen || undefined}>
        <header className="topbar">
          <div className="topbar-heading">
            {hasResponsiveSidebar && sidebarToggle}
            <h1>{pageTitle}</h1>
          </div>
          <div className="topbar-actions">
            <span className="role-badge">
              <RoleIcon size={22} aria-hidden="true" />
              {statusText[role] ?? role}
            </span>
            <button
              type="button"
              className="button secondary"
              disabled={isPreparingLogout}
              onClick={requestLogout}
            >
              <LogOut size={16} aria-hidden="true" />
              Выйти
            </button>
            {!hasResponsiveSidebar && sidebarToggle}
          </div>
        </header>

        {children}
      </main>
    </div>
  );
}

export function AppShell({ currentUser, role, children }) {
  const { logout } = useAuth();

  return (
    <NavigationGuardProvider onLogout={logout}>
      <AppShellContent currentUser={currentUser} role={role}>
        {children}
      </AppShellContent>
    </NavigationGuardProvider>
  );
}
