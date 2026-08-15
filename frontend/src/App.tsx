import { Navigate, Route, Routes } from "react-router-dom";
import { lazy, Suspense } from "react";
import { AuthLayout } from "./layouts/AuthLayout";
import { useAuth } from "./hooks/useAuth";
import { RouteErrorBoundary } from "./components/RouteErrorBoundary";
import { RequireSystemRole } from "./components/RequireSystemRole";
import { PageSkeleton } from "./components/AsyncState";

const LoginPage = lazy(() => import("./pages/LoginPage").then((module) => ({ default: module.LoginPage })));
const RegisterPage = lazy(() => import("./pages/RegisterPage").then((module) => ({ default: module.RegisterPage })));
const ForgotPasswordPage = lazy(() => import("./pages/ForgotPasswordPage").then((module) => ({ default: module.ForgotPasswordPage })));
const ResetPasswordPage = lazy(() => import("./pages/ResetPasswordPage").then((module) => ({ default: module.ResetPasswordPage })));
const VerifyEmailPage = lazy(() => import("./pages/VerifyEmailPage").then((module) => ({ default: module.VerifyEmailPage })));
const GuestAiPage = lazy(() => import("./pages/GuestAiPage").then((module) => ({ default: module.GuestAiPage })));
const DemoLoginPage = lazy(() => import("./pages/DemoLoginPage").then((module) => ({ default: module.DemoLoginPage })));
const ErrorPage = lazy(() => import("./pages/ErrorPage").then((module) => ({ default: module.ErrorPage })));
const DashboardLayout = lazy(() => import("./layouts/DashboardLayout").then((module) => ({ default: module.DashboardLayout })));
const DashboardPage = lazy(() => import("./pages/DashboardPage").then((module) => ({ default: module.DashboardPage })));
const ProjectsPage = lazy(() => import("./pages/ProjectsPage").then((module) => ({ default: module.ProjectsPage })));
const ProjectSettingsPage = lazy(() => import("./pages/ProjectSettingsPage").then((module) => ({ default: module.ProjectSettingsPage })));
const InvitationPage = lazy(() => import("./pages/InvitationPage").then((module) => ({ default: module.InvitationPage })));
const FileExplorerPage = lazy(() => import("./pages/FileExplorerPage").then((module) => ({ default: module.FileExplorerPage })));
const ChatPage = lazy(() => import("./pages/ChatPage").then((module) => ({ default: module.ChatPage })));
const NotificationCenterPage = lazy(() => import("./pages/NotificationCenterPage").then((module) => ({ default: module.NotificationCenterPage })));
const KanbanPage = lazy(() => import("./pages/KanbanPage").then((module) => ({ default: module.KanbanPage })));
const AdminActivityPage = lazy(() => import("./pages/AdminActivityPage").then((module) => ({ default: module.AdminActivityPage })));
const SettingsPage = lazy(() => import("./pages/SettingsPage").then((module) => ({ default: module.SettingsPage })));
const HelpCenterPage = lazy(() => import("./pages/HelpCenterPage").then((module) => ({ default: module.HelpCenterPage })));
const TeamPage = lazy(() => import("./pages/TeamPage").then((module) => ({ default: module.TeamPage })));
const AnalyticsPage = lazy(() => import("./pages/AnalyticsPage").then((module) => ({ default: module.AnalyticsPage })));
const AdminPage = lazy(() => import("./pages/AdminPage").then((module) => ({ default: module.AdminPage })));
const ProjectToolPage = lazy(() => import("./pages/ProjectToolPage").then((module) => ({ default: module.ProjectToolPage })));

function ProtectedDashboard() {
  const { session, isInitializing } = useAuth();
  if (isInitializing) return <PageSkeleton />;
  if (!session) return <Navigate to="/login" replace />;
  if (!session.user.isDemo && !session.user.isEmailVerified) return <Navigate to="/register" replace />;
  return <RouteErrorBoundary><Suspense fallback={<div className="route-loader" role="status">Loading workspace…</div>}><DashboardLayout /></Suspense></RouteErrorBoundary>;
}

function HomeRedirect() {
  const { session, isInitializing } = useAuth();
  if (isInitializing) return <PageSkeleton />;
  const demoBuild = import.meta.env.VITE_DEMO_MODE === "true";
  return <Navigate to={session ? (!session.user.isDemo && !session.user.isEmailVerified ? "/register" : "/dashboard") : demoBuild ? "/demo" : "/ai"} replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<HomeRedirect />} />
      <Route
        path="/ai"
        element={
          <RouteErrorBoundary>
            <Suspense fallback={<PageSkeleton />}>
              <GuestAiPage />
            </Suspense>
          </RouteErrorBoundary>
        }
      />
      <Route
        path="/demo"
        element={
          <RouteErrorBoundary>
            <Suspense fallback={<PageSkeleton />}>
              <DemoLoginPage />
            </Suspense>
          </RouteErrorBoundary>
        }
      />
      <Route element={<AuthLayout />}>
        <Route path="/login" element={<Suspense fallback={<PageSkeleton />}><LoginPage /></Suspense>} />
        <Route path="/register" element={<Suspense fallback={<PageSkeleton />}><RegisterPage /></Suspense>} />
        <Route path="/forgot-password" element={<Suspense fallback={<PageSkeleton />}><ForgotPasswordPage /></Suspense>} />
        <Route path="/reset-password" element={<Suspense fallback={<PageSkeleton />}><ResetPasswordPage /></Suspense>} />
        <Route path="/verify-email" element={<Suspense fallback={<PageSkeleton />}><VerifyEmailPage /></Suspense>} />
        <Route path="/401" element={<Suspense fallback={<PageSkeleton />}><ErrorPage code={401} /></Suspense>} />
      </Route>
      <Route element={<ProtectedDashboard />}>
        <Route path="/dashboard" element={<Suspense fallback={<div className="route-loader" role="status">Loading dashboard…</div>}><DashboardPage /></Suspense>} />
        <Route path="/projects" element={<Suspense fallback={<div className="route-loader" role="status">Loading projects…</div>}><ProjectsPage /></Suspense>} />
        <Route path="/projects/:projectId/settings" element={<Suspense fallback={<div className="route-loader" role="status">Loading project…</div>}><ProjectSettingsPage /></Suspense>} />
        <Route path="/projects/:projectId/workspace" element={<Suspense fallback={<div className="route-loader" role="status">Loading workspace…</div>}><FileExplorerPage /></Suspense>} />
        <Route path="/projects/:projectId/board" element={<Suspense fallback={<div className="route-loader" role="status">Loading board…</div>}><KanbanPage /></Suspense>} />
        <Route path="/projects/:projectId/architecture" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="architecture" /></Suspense>} />
        <Route path="/projects/:projectId/database" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="database" /></Suspense>} />
        <Route path="/projects/:projectId/api" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="api" /></Suspense>} />
        <Route path="/projects/:projectId/versions" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="versions" /></Suspense>} />
        <Route path="/projects/:projectId/approvals" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="approvals" /></Suspense>} />
        <Route path="/projects/:projectId/billing" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="billing" /></Suspense>} />
        <Route element={<RequireSystemRole roles={["SuperAdmin", "Admin"]} />}>
          <Route path="/admin" element={<Suspense fallback={<div className="route-loader" role="status">Loading administration…</div>}><AdminPage /></Suspense>} />
          <Route path="/admin/activity" element={<Suspense fallback={<div className="route-loader" role="status">Loading activity…</div>}><AdminActivityPage /></Suspense>} />
        </Route>
        <Route path="/chat" element={<Suspense fallback={<div className="route-loader" role="status">Loading chat…</div>}><ChatPage /></Suspense>} />
        <Route path="/notifications" element={<Suspense fallback={<div className="route-loader" role="status">Loading notifications…</div>}><NotificationCenterPage /></Suspense>} />
        <Route path="/settings" element={<Suspense fallback={<div className="route-loader" role="status">Loading settings…</div>}><SettingsPage /></Suspense>} />
        <Route path="/help" element={<Suspense fallback={<div className="route-loader" role="status">Loading help center…</div>}><HelpCenterPage /></Suspense>} />
        <Route path="/team" element={<Suspense fallback={<div className="route-loader" role="status">Loading team…</div>}><TeamPage /></Suspense>} />
        <Route path="/analytics" element={<Suspense fallback={<div className="route-loader" role="status">Loading analytics…</div>}><AnalyticsPage /></Suspense>} />
        <Route path="/invitations/:token" element={<Suspense fallback={<div className="route-loader" role="status">Loading invitation…</div>}><InvitationPage /></Suspense>} />
      </Route>
      <Route path="/403" element={<Suspense fallback={<PageSkeleton />}><ErrorPage code={403} /></Suspense>} />
      <Route path="/500" element={<Suspense fallback={<PageSkeleton />}><ErrorPage code={500} /></Suspense>} />
      <Route path="*" element={<Suspense fallback={<PageSkeleton />}><ErrorPage code={404} /></Suspense>} />
    </Routes>
  );
}
