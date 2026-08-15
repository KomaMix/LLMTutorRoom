import { useState } from "react";
import {
  GraduationCap,
  LogOut,
  PanelLeft,
  School,
  ShieldCheck,
  UserRoundCheck
} from "lucide-react";
import { NavLink } from "react-router-dom";
import { useAuth } from "../auth/AuthContext.jsx";
import { getPageTitle } from "../shared/lib/roles.js";
import { StatusBadge } from "../shared/ui/StatusBadge.jsx";
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

function AppShellContent({ currentUser, role, children }) {
  const { isPreparingLogout, requestLogout } = useNavigationGuard();
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);
  const RoleIcon = roleIcons[role];
  const activeNavigation = navigation[role] ?? [];

  return (
    <div className={isSidebarCollapsed ? "app-shell sidebar-collapsed" : "app-shell"}>
      <aside className={isSidebarCollapsed ? "sidebar collapsed" : "sidebar"}>
        <div className="brand">
          <div className="brand-mark">
            <School size={22} aria-hidden="true" />
          </div>
          <div className="sidebar-label">
            <strong>LLMTutorRoom</strong>
            <span>умная проверка знаний</span>
          </div>
        </div>

        <div className="user-card">
          <div className="user-avatar">
            <RoleIcon size={18} aria-hidden="true" />
          </div>
          <div className="sidebar-label">
            <strong>{currentUser.displayName}</strong>
            <span>{currentUser.userName}</span>
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
              >
                <Icon size={18} aria-hidden="true" />
                <span className="sidebar-label">{item.label}</span>
              </NavLink>
            );
          })}
        </nav>
      </aside>

      <main className="workspace">
        <header className="topbar">
          <div>
            <span className="eyebrow">НИР prototype</span>
            <h1>{getPageTitle(role)}</h1>
          </div>
          <div className="topbar-actions">
            <StatusBadge status={role} />
            <button
              type="button"
              className="button secondary"
              disabled={isPreparingLogout}
              onClick={requestLogout}
            >
              <LogOut size={16} aria-hidden="true" />
              Выйти
            </button>
            <button
              type="button"
              className="icon-button"
              title={isSidebarCollapsed ? "Развернуть меню" : "Свернуть меню"}
              aria-label={isSidebarCollapsed ? "Развернуть меню" : "Свернуть меню"}
              aria-expanded={!isSidebarCollapsed}
              onClick={() => setIsSidebarCollapsed(current => !current)}
            >
              <PanelLeft size={18} aria-hidden="true" />
            </button>
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
