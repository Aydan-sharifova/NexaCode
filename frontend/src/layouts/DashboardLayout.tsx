import { useEffect, useRef, useState } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { Icon, type IconName } from "../components/Icon";
import { useAuth } from "../hooks/useAuth";
import { useTheme } from "../hooks/useTheme";
import { NotificationBell } from "../features/notifications/NotificationBell";
import { ProjectFormDialog } from "../features/projects/ProjectFormDialog";
import { useCreateProject, useProjects } from "../features/projects/hooks";
import type { ProjectInput } from "../features/projects/types";
import { useToast } from "../contexts/ToastContext";
import { GlobalSearchPalette } from "../features/search/GlobalSearchPalette";
import { useLanguage } from "../hooks/useLanguage";
import type { TranslationKey } from "../contexts/LanguageContext";
import { usePageTranslation } from "../hooks/usePageTranslation";

const navItems: Array<{ label: TranslationKey; path: string; icon: IconName }> = [
  { label: "overview", path: "/dashboard", icon: "dashboard" },
  { label: "projects", path: "/projects", icon: "folder" },
  { label: "chat", path: "/chat", icon: "team" },
  { label: "team", path: "/team", icon: "team" },
  { label: "analytics", path: "/analytics", icon: "chart" },
];

export function DashboardLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [accountMenuOpen, setAccountMenuOpen] = useState(false);
  const [topbarAccountOpen, setTopbarAccountOpen] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);
  const accountMenuRef = useRef<HTMLDivElement>(null);
  const topbarAccountRef = useRef<HTMLDivElement>(null);
  const [searchOpen, setSearchOpen] = useState(false); const [createOpen, setCreateOpen] = useState(false);
  const { session, logout } = useAuth();
  const location = useLocation();
  const { theme, toggleTheme } = useTheme();
  const { t } = useLanguage();
  const { pt } = usePageTranslation();
  const projects = useProjects(); const createProject = useCreateProject(); const navigate = useNavigate(); const { show } = useToast();
  const user = session?.user;
  const isDemo = Boolean(user?.isDemo);
  const projectId = location.pathname.match(/^\/projects\/([^/]+)/)?.[1];
  const initials = user ? `${user.firstName[0]}${user.lastName[0]}` : "AD";
  useEffect(() => { const shortcut = (event: KeyboardEvent) => { if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") { event.preventDefault(); setSearchOpen(true); window.setTimeout(() => document.getElementById("global-search")?.focus(), 0); } }; window.addEventListener("keydown", shortcut); return () => window.removeEventListener("keydown", shortcut); }, []);
  useEffect(() => {
    const close = (event: MouseEvent) => {
      if (!accountMenuRef.current?.contains(event.target as Node))
        setAccountMenuOpen(false);
      if (!topbarAccountRef.current?.contains(event.target as Node))
        setTopbarAccountOpen(false);
    };
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setAccountMenuOpen(false);
        setTopbarAccountOpen(false);
      }
    };
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", escape);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", escape);
    };
  }, []);
  const create = async (input: ProjectInput) => { try { const project = await createProject.mutateAsync(input); setCreateOpen(false); show("Project created successfully."); navigate(`/projects/${project.id}/workspace`); } catch (error) { show(error instanceof Error ? error.message : "Project creation failed.", "error"); } };
  const signOut = async () => {
    if (loggingOut) return;
    setLoggingOut(true);
    try {
      await logout();
      show(pt("signedOut"));
      navigate("/login", { replace: true });
    } catch (error) {
      show(error instanceof Error ? error.message : "Logout failed.", "error");
    } finally {
      setLoggingOut(false);
      setAccountMenuOpen(false);
    }
  };

  return (
    <div className="dashboard-shell">
      <aside className={`sidebar ${sidebarOpen ? "is-open" : ""}`}>
        <div className="sidebar-brand"><span className="brand-mark">N</span><span>NexaCode</span>{isDemo && <b className="sidebar-demo-label">Demo</b>}</div>
        <nav className="sidebar-nav" aria-label={t("openNavigation")}>
          <p>{t("workspace")}</p>
          {navItems.map((item, index) => <span className="sidebar-nav-entry" key={item.label}><NavLink to={item.path} end={item.path === "/dashboard"} onClick={() => setSidebarOpen(false)}><Icon name={item.icon} />{t(item.label)}</NavLink>{index === 0 && <NavLink to="/feed" onClick={() => setSidebarOpen(false)}><Icon name="activity" />Feed</NavLink>}</span>)}
          <NavLink to="/marketplace" onClick={() => setSidebarOpen(false)}><Icon name="trend" />Marketplace</NavLink>
          <NavLink to="/discover" onClick={() => setSidebarOpen(false)}><Icon name="search" />Discover</NavLink>
          <NavLink to="/saved" onClick={() => setSidebarOpen(false)}><Icon name="check" />Saved</NavLink>
          <NavLink to="/live-rooms" onClick={() => setSidebarOpen(false)}><Icon name="team" />Live rooms</NavLink>
          <NavLink to="/achievements" onClick={() => setSidebarOpen(false)}><Icon name="check" />Achievements</NavLink>
          <NavLink to="/mentor" onClick={() => setSidebarOpen(false)}><Icon name="activity" />AI Mentor</NavLink>
          <NavLink to="/planner" onClick={() => setSidebarOpen(false)}><Icon name="code" />Project Planner</NavLink>
          {projectId && <><p>PROJECT TOOLS</p><NavLink to={`/projects/${projectId}/workspace`}><Icon name="code" />Workspace</NavLink><NavLink to={`/projects/${projectId}/board`}><Icon name="check" />Tasks</NavLink><NavLink to={`/projects/${projectId}/architecture`}><Icon name="activity" />Architecture</NavLink><NavLink to={`/projects/${projectId}/knowledge`}><Icon name="trend" />Knowledge graph</NavLink><NavLink to={`/projects/${projectId}/debugging`}><Icon name="activity" />Debugging timeline</NavLink><NavLink to={`/projects/${projectId}/autonomous-tests`}><Icon name="check" />Test agent</NavLink><NavLink to={`/projects/${projectId}/database`}><Icon name="dashboard" />Database</NavLink><NavLink to={`/projects/${projectId}/api`}><Icon name="code" />API</NavLink><NavLink to={`/projects/${projectId}/versions`}><Icon name="trend" />Versions</NavLink><NavLink to={`/projects/${projectId}/approvals`}><Icon name="check" />AI approvals</NavLink></>}
          {projectId && <><NavLink to={`/projects/${projectId}/screenshot-code`}><Icon name="code" />Screenshot to code</NavLink><NavLink to={`/projects/${projectId}/ui-generator`}><Icon name="activity" />AI UI Generator</NavLink></>}
          {user?.roles.some((role) => ["SuperAdmin", "Admin", "Moderator"].includes(role)) && <NavLink to="/moderation" onClick={() => setSidebarOpen(false)}><Icon name="check" />Moderation</NavLink>}
          {user?.roles.some((role) => ["SuperAdmin", "Admin"].includes(role)) && <><NavLink to="/admin" onClick={() => setSidebarOpen(false)}><Icon name="settings" />{t("admin")}</NavLink><NavLink to="/admin/activity" onClick={() => setSidebarOpen(false)}><Icon name="activity" />{t("activity")}</NavLink></>}
          <p>{t("manage")}</p>
          <NavLink to="/settings"><Icon name="settings" />{t("settings")}</NavLink>
          <NavLink to="/settings/blocked"><Icon name="team" />Blocked users</NavLink>
          <NavLink to="/help"><Icon name="help" />{t("help")}</NavLink>
        </nav>
        <div className="sidebar-upgrade"><span><Icon name="trend" /></span><strong>{pt("unlockInsights")}</strong><p>{pt("upgradeWorkspace")}</p><button>{pt("viewPlans")}</button></div>
        <div className="sidebar-account" ref={accountMenuRef}>
          {accountMenuOpen && (
            <div className="account-menu" role="menu" aria-label={pt("accountMenu")}>
              <button role="menuitem" onClick={() => { setAccountMenuOpen(false); navigate("/settings?section=profile"); }}>
                <Icon name="team" />{t("profile")}
              </button>
              <button role="menuitem" onClick={() => { setAccountMenuOpen(false); navigate("/settings"); }}>
                <Icon name="settings" />{t("settings")}
              </button>
              <span />
              <button className="danger" role="menuitem" disabled={loggingOut} onClick={() => void signOut()}>
                <Icon name="activity" />{loggingOut ? `${pt("logout")}…` : pt("logout")}
              </button>
            </div>
          )}
          <button
            className="sidebar-user"
            aria-label={pt("accountMenu")}
            aria-haspopup="menu"
            aria-expanded={accountMenuOpen}
            onClick={() => setAccountMenuOpen((open) => !open)}
          >
            <span className="avatar">{initials}</span>
            <span><strong>{user ? `${user.firstName} ${user.lastName}` : "Alex Developer"}</strong><small>{user?.email ?? "alex@coding.dev"}</small></span>
            <Icon name="chevron" />
          </button>
        </div>
      </aside>
      {sidebarOpen && <button className="sidebar-backdrop" aria-label={t("closeNavigation")} onClick={() => setSidebarOpen(false)} />}
      <div className="dashboard-main">
        <header className="topbar">
          <button className="icon-button mobile-menu" onClick={() => setSidebarOpen(true)} aria-label={t("openNavigation")}><Icon name="menu" /></button>
          <div className="global-search-wrap"><button className="dashboard-search" onClick={() => setSearchOpen(true)} aria-label={t("search")}><Icon name="search" /><span>{t("search")}</span><kbd>⌘ K</kbd></button></div>
          <div className="topbar-actions">
            {isDemo && (
              <span className="persistent-demo-badge" title="Changes reset automatically">
                <i />
                Demo Environment
                {user?.demoRole && <b>{user.demoRole}</b>}
              </span>
            )}
            <button className="icon-button" onClick={toggleTheme} aria-label={t("theme")}><Icon name={theme === "dark" ? "sun" : "moon"} /></button>
            <NotificationBell />
            <button className="create-button" onClick={() => setCreateOpen(true)}><Icon name="plus" /> {t("newProject")}</button>
            <div className="topbar-account" ref={topbarAccountRef}>
              <button
                className="topbar-avatar"
                aria-label={pt("accountMenu")}
                aria-haspopup="menu"
                aria-expanded={topbarAccountOpen}
                onClick={() => setTopbarAccountOpen((open) => !open)}
              >
                {initials}
                <Icon name="chevron" />
              </button>
              {topbarAccountOpen && (
                <div className="account-menu topbar-account-menu" role="menu" aria-label={pt("accountMenu")}>
                  <header><strong>{user ? `${user.firstName} ${user.lastName}` : ""}</strong><small>{user?.email}</small></header>
                  <button role="menuitem" onClick={() => { setTopbarAccountOpen(false); navigate("/settings?section=profile"); }}><Icon name="team" />{t("profile")}</button>
                  <button role="menuitem" onClick={() => { setTopbarAccountOpen(false); navigate("/settings"); }}><Icon name="settings" />{t("settings")}</button>
                  <span />
                  <button className="danger" role="menuitem" disabled={loggingOut} onClick={() => void signOut()}><Icon name="activity" />{loggingOut ? `${pt("logout")}…` : pt("logout")}</button>
                </div>
              )}
            </div>
          </div>
        </header>
        <Outlet />
        <ProjectFormDialog open={createOpen} pending={createProject.isPending} onClose={() => setCreateOpen(false)} onSubmit={create} />
        <GlobalSearchPalette open={searchOpen} onOpenChange={setSearchOpen} />
      </div>
    </div>
  );
}
