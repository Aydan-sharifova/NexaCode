import { Navigate, Route, Routes } from "react-router-dom";
import { lazy, Suspense, type ComponentType } from "react";
import { AuthLayout } from "./layouts/AuthLayout";
import { useAuth } from "./hooks/useAuth";
import { RouteErrorBoundary } from "./components/RouteErrorBoundary";
import { RequireSystemRole } from "./components/RequireSystemRole";
import { PageSkeleton } from "./components/AsyncState";

const dynamicImportRetryKey = "coding:dynamic-import-retry";
function lazyRoute<T extends ComponentType<any>>(loader: () => Promise<{ default: T }>) {
  return lazy(async () => {
    try {
      const loaded = await loader();
      sessionStorage.removeItem(dynamicImportRetryKey);
      return loaded;
    } catch (error) {
      const message = error instanceof Error ? error.message : "";
      const isImportFailure = /dynamically imported module|loading chunk|importing a module/i.test(message);
      if (isImportFailure && !sessionStorage.getItem(dynamicImportRetryKey)) {
        sessionStorage.setItem(dynamicImportRetryKey, window.location.pathname);
        window.location.reload();
        return await new Promise<never>(() => undefined);
      }
      sessionStorage.removeItem(dynamicImportRetryKey);
      throw error;
    }
  });
}

const LoginPage = lazyRoute(() => import("./pages/LoginPage").then((module) => ({ default: module.LoginPage })));
const RegisterPage = lazyRoute(() => import("./pages/RegisterPage").then((module) => ({ default: module.RegisterPage })));
const ForgotPasswordPage = lazyRoute(() => import("./pages/ForgotPasswordPage").then((module) => ({ default: module.ForgotPasswordPage })));
const ResetPasswordPage = lazyRoute(() => import("./pages/ResetPasswordPage").then((module) => ({ default: module.ResetPasswordPage })));
const VerifyEmailPage = lazyRoute(() => import("./pages/VerifyEmailPage").then((module) => ({ default: module.VerifyEmailPage })));
const GuestAiPage = lazyRoute(() => import("./pages/GuestAiPage").then((module) => ({ default: module.GuestAiPage })));
const DemoLoginPage = lazyRoute(() => import("./pages/DemoLoginPage").then((module) => ({ default: module.DemoLoginPage })));
const ErrorPage = lazyRoute(() => import("./pages/ErrorPage").then((module) => ({ default: module.ErrorPage })));
const DashboardLayout = lazyRoute(() => import("./layouts/DashboardLayout").then((module) => ({ default: module.DashboardLayout })));
const DashboardPage = lazyRoute(() => import("./pages/DashboardPage").then((module) => ({ default: module.DashboardPage })));
const FeedPage = lazyRoute(() => import("./pages/FeedPage").then((module) => ({ default: module.FeedPage })));
const DiscoverPage = lazyRoute(() => import("./pages/DiscoverPage").then((module) => ({ default: module.DiscoverPage })));
const SavedPage = lazyRoute(() => import("./pages/SavedPage").then((module) => ({ default: module.SavedPage })));
const ProjectsPage = lazyRoute(() => import("./pages/ProjectsPage").then((module) => ({ default: module.ProjectsPage })));
const ProjectSettingsPage = lazyRoute(() => import("./pages/ProjectSettingsPage").then((module) => ({ default: module.ProjectSettingsPage })));
const PullRequestsPage = lazyRoute(() => import("./pages/PullRequestsPage").then((module) => ({ default: module.PullRequestsPage })));
const DeploymentsPage = lazyRoute(() => import("./pages/DeploymentsPage").then((module) => ({ default: module.DeploymentsPage })));
const InvitationPage = lazyRoute(() => import("./pages/InvitationPage").then((module) => ({ default: module.InvitationPage })));
const FileExplorerPage = lazyRoute(() => import("./pages/FileExplorerPage").then((module) => ({ default: module.FileExplorerPage })));
const ChatPage = lazyRoute(() => import("./pages/ChatPage").then((module) => ({ default: module.ChatPage })));
const NotificationCenterPage = lazyRoute(() => import("./pages/NotificationCenterPage").then((module) => ({ default: module.NotificationCenterPage })));
// Keep in-app board navigation client-side. Automatically reloading this protected
// route can unnecessarily recreate the auth session and trigger the auth limiter.
const KanbanPage = lazy(() => import("./pages/KanbanPage").then((module) => ({ default: module.KanbanPage })));
const AdminActivityPage = lazyRoute(() => import("./pages/AdminActivityPage").then((module) => ({ default: module.AdminActivityPage })));
const SettingsPage = lazyRoute(() => import("./pages/SettingsPage").then((module) => ({ default: module.SettingsPage })));
const BlockedUsersPage = lazyRoute(() => import("./pages/BlockedUsersPage").then((module) => ({ default: module.BlockedUsersPage })));
const HelpCenterPage = lazyRoute(() => import("./pages/HelpCenterPage").then((module) => ({ default: module.HelpCenterPage })));
const TeamPage = lazyRoute(() => import("./pages/TeamPage").then((module) => ({ default: module.TeamPage })));
const AnalyticsPage = lazyRoute(() => import("./pages/AnalyticsPage").then((module) => ({ default: module.AnalyticsPage })));
const AdminPage = lazyRoute(() => import("./pages/AdminPage").then((module) => ({ default: module.AdminPage })));
const ProjectToolPage = lazyRoute(() => import("./pages/ProjectToolPage").then((module) => ({ default: module.ProjectToolPage })));
const PublicUserProfilePage = lazyRoute(() => import("./pages/PublicUserProfilePage").then((module) => ({ default: module.PublicUserProfilePage })));
const PublicProjectPage = lazyRoute(() => import("./pages/PublicProjectPage").then((module) => ({ default: module.PublicProjectPage })));
const MarketplacePage = lazyRoute(() => import("./pages/MarketplacePage").then((module) => ({ default: module.MarketplacePage })));
const LiveRoomsPage = lazyRoute(() => import("./pages/LiveRoomsPage").then((module) => ({ default: module.LiveRoomsPage })));
const LiveRoomPage = lazyRoute(() => import("./pages/LiveRoomPage").then((module) => ({ default: module.LiveRoomPage })));
const AchievementsPage = lazyRoute(() => import("./pages/AchievementsPage").then((module) => ({ default: module.AchievementsPage })));
const MentorPage = lazyRoute(() => import("./pages/MentorPage").then((module) => ({ default: module.MentorPage })));
const ProjectPlannerPage = lazyRoute(() => import("./pages/ProjectPlannerPage").then((module) => ({ default: module.ProjectPlannerPage })));
const KnowledgeGraphPage = lazyRoute(() => import("./pages/KnowledgeGraphPage").then((module) => ({ default: module.KnowledgeGraphPage })));
const DebuggingTimelinePage = lazyRoute(() => import("./pages/DebuggingTimelinePage").then((module) => ({ default: module.DebuggingTimelinePage })));
const AutonomousTestingPage = lazyRoute(() => import("./pages/AutonomousTestingPage").then((module) => ({ default: module.AutonomousTestingPage })));
const ScreenshotToCodePage = lazyRoute(() => import("./pages/ScreenshotToCodePage").then((module) => ({ default: module.ScreenshotToCodePage })));
const AiUiGeneratorPage = lazyRoute(() => import("./pages/AiUiGeneratorPage").then((module) => ({ default: module.AiUiGeneratorPage })));
const ModerationPage = lazyRoute(() => import("./pages/ModerationPage").then((module) => ({ default: module.ModerationPage })));
const BillingPage = lazyRoute(() => import("./pages/BillingPage").then((module) => ({ default: module.BillingPage })));

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
  const params = new URLSearchParams(window.location.search);
  const accountAction = params.get("accountAction");
  if (accountAction === "verify-email" || accountAction === "reset-password") {
    params.delete("accountAction");
    return <Navigate to={`/${accountAction}?${params.toString()}`} replace />;
  }
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
        <Route path="/feed" element={<Suspense fallback={<div className="route-loader" role="status">Loading feed…</div>}><FeedPage /></Suspense>} />
        <Route path="/discover" element={<Suspense fallback={<PageSkeleton />}><DiscoverPage /></Suspense>} />
        <Route path="/saved" element={<Suspense fallback={<PageSkeleton />}><SavedPage /></Suspense>} />
        <Route path="/projects" element={<Suspense fallback={<div className="route-loader" role="status">Loading projects…</div>}><ProjectsPage /></Suspense>} />
        <Route path="/marketplace" element={<Suspense fallback={<PageSkeleton />}><MarketplacePage /></Suspense>} />
        <Route path="/live-rooms" element={<Suspense fallback={<PageSkeleton />}><LiveRoomsPage /></Suspense>} />
        <Route path="/live-rooms/:roomId" element={<Suspense fallback={<PageSkeleton />}><LiveRoomPage /></Suspense>} />
        <Route path="/achievements" element={<Suspense fallback={<PageSkeleton />}><AchievementsPage /></Suspense>} />
        <Route path="/mentor" element={<Suspense fallback={<PageSkeleton />}><MentorPage /></Suspense>} />
        <Route path="/planner" element={<Suspense fallback={<PageSkeleton />}><ProjectPlannerPage /></Suspense>} />
        <Route path="/projects/:projectId/settings" element={<Suspense fallback={<div className="route-loader" role="status">Loading project…</div>}><ProjectSettingsPage /></Suspense>} />
        <Route path="/projects/:projectId/workspace" element={<Suspense fallback={<div className="route-loader" role="status">Loading workspace…</div>}><FileExplorerPage /></Suspense>} />
        <Route path="/projects/:projectId/pull-requests" element={<Suspense fallback={<div className="route-loader" role="status">Loading pull requests…</div>}><PullRequestsPage /></Suspense>} />
        <Route path="/projects/:projectId/deployments" element={<Suspense fallback={<PageSkeleton />}><DeploymentsPage /></Suspense>} />
        <Route path="/projects/:projectId/board" element={<Suspense fallback={<div className="route-loader" role="status">Loading board…</div>}><KanbanPage /></Suspense>} />
        <Route path="/projects/:projectId/architecture" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="architecture" /></Suspense>} />
        <Route path="/projects/:projectId/knowledge" element={<Suspense fallback={<PageSkeleton />}><KnowledgeGraphPage /></Suspense>} />
        <Route path="/projects/:projectId/debugging" element={<Suspense fallback={<PageSkeleton />}><DebuggingTimelinePage /></Suspense>} />
        <Route path="/projects/:projectId/autonomous-tests" element={<Suspense fallback={<PageSkeleton />}><AutonomousTestingPage /></Suspense>} />
        <Route path="/projects/:projectId/screenshot-code" element={<Suspense fallback={<PageSkeleton />}><ScreenshotToCodePage /></Suspense>} />
        <Route path="/projects/:projectId/ui-generator" element={<Suspense fallback={<PageSkeleton />}><AiUiGeneratorPage /></Suspense>} />
        <Route path="/projects/:projectId/database" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="database" /></Suspense>} />
        <Route path="/projects/:projectId/api" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="api" /></Suspense>} />
        <Route path="/projects/:projectId/versions" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="versions" /></Suspense>} />
        <Route path="/projects/:projectId/approvals" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="approvals" /></Suspense>} />
        <Route path="/projects/:projectId/billing" element={<Suspense fallback={<PageSkeleton />}><ProjectToolPage tool="billing" /></Suspense>} />
        <Route element={<RequireSystemRole roles={["SuperAdmin", "Admin"]} />}>
        <Route path="/admin" element={<Suspense fallback={<div className="route-loader" role="status">Loading administration…</div>}><AdminPage /></Suspense>} />
          <Route path="/admin/activity" element={<Suspense fallback={<div className="route-loader" role="status">Loading activity…</div>}><AdminActivityPage /></Suspense>} />
        </Route>
        <Route element={<RequireSystemRole roles={["SuperAdmin", "Admin", "Moderator"]} />}><Route path="/moderation" element={<Suspense fallback={<PageSkeleton />}><ModerationPage /></Suspense>} /></Route>
        <Route path="/chat" element={<Suspense fallback={<div className="route-loader" role="status">Loading chat…</div>}><ChatPage /></Suspense>} />
        <Route path="/notifications" element={<Suspense fallback={<div className="route-loader" role="status">Loading notifications…</div>}><NotificationCenterPage /></Suspense>} />
        <Route path="/settings" element={<Suspense fallback={<div className="route-loader" role="status">Loading settings…</div>}><SettingsPage /></Suspense>} />
        <Route path="/settings/blocked" element={<Suspense fallback={<PageSkeleton />}><BlockedUsersPage /></Suspense>} />
        <Route path="/billing" element={<Suspense fallback={<PageSkeleton />}><BillingPage /></Suspense>} />
        <Route path="/help" element={<Suspense fallback={<div className="route-loader" role="status">Loading help center…</div>}><HelpCenterPage /></Suspense>} />
        <Route path="/team" element={<Suspense fallback={<div className="route-loader" role="status">Loading team…</div>}><TeamPage /></Suspense>} />
        <Route path="/users/:publicId" element={<Suspense fallback={<div className="route-loader" role="status">Loading public profile…</div>}><PublicUserProfilePage /></Suspense>} />
        <Route path="/profile/:publicId" element={<Suspense fallback={<div className="route-loader" role="status">Loading public profile…</div>}><PublicUserProfilePage /></Suspense>} />
        <Route path="/public/projects/:projectId" element={<Suspense fallback={<div className="route-loader" role="status">Loading public project…</div>}><PublicProjectPage /></Suspense>} />
        <Route path="/analytics" element={<Suspense fallback={<div className="route-loader" role="status">Loading analytics…</div>}><AnalyticsPage /></Suspense>} />
        <Route path="/invitations/:token" element={<Suspense fallback={<div className="route-loader" role="status">Loading invitation…</div>}><InvitationPage /></Suspense>} />
      </Route>
      <Route path="/403" element={<Suspense fallback={<PageSkeleton />}><ErrorPage code={403} /></Suspense>} />
      <Route path="/500" element={<Suspense fallback={<PageSkeleton />}><ErrorPage code={500} /></Suspense>} />
      <Route path="*" element={<Suspense fallback={<PageSkeleton />}><ErrorPage code={404} /></Suspense>} />
    </Routes>
  );
}
