import { useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { EmptyState, ErrorState, LoadingState } from "../components/AsyncState";
import { ConfirmDialog, Dialog } from "../components/ui/Dialog";
import { useToast } from "../contexts/ToastContext";
import { ProjectFormDialog } from "../features/projects/ProjectFormDialog";
import {
  useChangeMemberRole,
  useDeleteProject,
  useExtendProjectDeadline,
  useInviteMember,
  useProject,
  useProjectInvitations,
  useProjectMembers,
  useRemoveMember,
  useUpdateProject,
} from "../features/projects/hooks";
import type { ProjectInput, ProjectRole } from "../features/projects/types";
import { useAuth } from "../hooks/useAuth";

export function ProjectSettingsPage() {
  const { projectId = "" } = useParams();
  const navigate = useNavigate();
  const { show } = useToast();
  const { session } = useAuth();
  const isDemo = Boolean(session?.user.isDemo);
  const isSuperAdmin = session?.user.roles.includes("SuperAdmin") ?? false;
  const project = useProject(projectId);
  const members = useProjectMembers(projectId);
  const canManage =
    project.data?.currentUserRole === "Owner" ||
    project.data?.currentUserRole === "Admin";
  const isOwner = project.data?.currentUserRole === "Owner";
  const invitations = useProjectInvitations(projectId, canManage);
  const update = useUpdateProject(projectId);
  const remove = useDeleteProject(projectId);
  const invite = useInviteMember(projectId);
  const changeRole = useChangeMemberRole(projectId);
  const removeMember = useRemoveMember(projectId);
  const extendDeadline = useExtendProjectDeadline(projectId);
  const [editOpen, setEditOpen] = useState(false);
  const [inviteOpen, setInviteOpen] = useState(false);
  const [deadlineOpen, setDeadlineOpen] = useState(false);
  const [deadline, setDeadline] = useState("");
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [email, setEmail] = useState("");
  const [inviteRole, setInviteRole] = useState<
    "Admin" | "Maintainer" | "Developer" | "Viewer"
  >("Developer");
  const [removing, setRemoving] = useState<{
    userId: string;
    name: string;
  } | null>(null);
  if (project.isLoading)
    return (
      <main className="dashboard-content">
        <LoadingState label="Loading project…" />
      </main>
    );
  if (project.isError || !project.data)
    return (
      <main className="dashboard-content">
        <ErrorState message={project.error?.message ?? "Project not found."} />
      </main>
    );
  const details = project.data;
  const save = async (input: ProjectInput) => {
    try {
      await update.mutateAsync(input);
      setEditOpen(false);
      show("Project settings updated.");
    } catch (error) {
      show(error instanceof Error ? error.message : "Update failed.", "error");
    }
  };
  const sendInvite = async () => {
    try {
      const result = await invite.mutateAsync({ email, role: inviteRole });
      await navigator.clipboard?.writeText(
        `${location.origin}/invitations/${result.token}`,
      );
      setInviteOpen(false);
      setEmail("");
      show("Invitation created and link copied.");
    } catch (error) {
      show(
        error instanceof Error ? error.message : "Invitation failed.",
        "error",
      );
    }
  };
  const extend = async () => {
    try {
      await extendDeadline.mutateAsync(new Date(deadline).toISOString());
      setDeadlineOpen(false);
      setDeadline("");
      show("Project deadline extended.");
    } catch (error) {
      show(
        error instanceof Error ? error.message : "Deadline extension failed.",
        "error",
      );
    }
  };
  return (
    <main className="dashboard-content feature-page">
      <header className="feature-heading">
        <div>
          <button className="back-link" onClick={() => navigate("/projects")}>
            ← Projects
          </button>
          <h1>{details.name}</h1>
          <p>Project settings and access management</p>
        </div>
        <span className={`role-badge ${details.currentUserRole.toLowerCase()}`}>
          {details.currentUserRole}
        </span>
      </header>
      {isDemo && (
        <div className="demo-settings-notice">
          <strong>Demo safeguards are active.</strong>
          <span>
            Invitations, role changes, removals and project deletion are
            disabled.
          </span>
        </div>
      )}
      <div className="settings-layout">
        <section className="settings-main">
          <article className="settings-card">
            <header>
              <div>
                <h2>General settings</h2>
                <p>Project information and visibility.</p>
              </div>
              {canManage && (
                <button
                  className="ui-button ghost"
                  onClick={() => setEditOpen(true)}
                >
                  Edit
                </button>
              )}
            </header>
            <dl>
              <div>
                <dt>Name</dt>
                <dd>{details.name}</dd>
              </div>
              <div>
                <dt>Language</dt>
                <dd>{details.defaultLanguage}</dd>
              </div>
              <div>
                <dt>Visibility</dt>
                <dd>{details.isPublic ? "Public" : "Private"}</dd>
              </div>
              <div>
                <dt>Description</dt>
                <dd>{details.description || "—"}</dd>
              </div>
            </dl>
          </article>
          <article className="settings-card">
            <header>
              <div>
                <h2>Deadline and lifecycle</h2>
                <p>Server-calculated project capability state.</p>
              </div>
              {isSuperAdmin && details.status === "DeadlineExpired" && (
                <button
                  className="ui-button primary"
                  onClick={() => setDeadlineOpen(true)}
                >
                  Extend deadline
                </button>
              )}
            </header>
            <dl>
              <div>
                <dt>Status</dt>
                <dd>
                  <span className={`status ${details.status.toLowerCase()}`}>
                    {details.status}
                  </span>
                </dd>
              </div>
              <div>
                <dt>Deadline</dt>
                <dd>
                  {details.deadlineAt
                    ? new Date(details.deadlineAt).toLocaleString()
                    : "No deadline"}
                </dd>
              </div>
              <div>
                <dt>Workspace access</dt>
                <dd>{details.isReadOnly ? "Read-only" : "Writable"}</dd>
              </div>
            </dl>
          </article>
          <article className="settings-card">
            <header>
              <div>
                <h2>Members</h2>
                <p>Everyone with access to this project.</p>
              </div>
              {canManage && !isDemo && (
                <button
                  className="ui-button primary"
                  onClick={() => setInviteOpen(true)}
                >
                  Invite member
                </button>
              )}
            </header>
            {members.isLoading ? (
              <LoadingState />
            ) : members.data?.length ? (
              <div className="members-list">
                {members.data.map((member) => (
                  <div className="member-row" key={member.userId}>
                    <span className="avatar">
                      {member.fullName
                        .split(" ")
                        .map((part) => part[0])
                        .join("")
                        .slice(0, 2)}
                    </span>
                    <div>
                      <strong>{member.fullName}</strong>
                      <small>{member.email}</small>
                    </div>
                    {isOwner && member.role !== "Owner" && !isDemo ? (
                      <select
                        value={member.role}
                        onChange={(event) =>
                          changeRole.mutate({
                            userId: member.userId,
                            role: event.target.value as Exclude<
                              ProjectRole,
                              "Owner"
                            >,
                          })
                        }
                      >
                        <option>Admin</option>
                        <option>Viewer</option>
                        <option>Developer</option>
                        <option>Maintainer</option>
                      </select>
                    ) : (
                      <span
                        className={`role-badge ${member.role.toLowerCase()}`}
                      >
                        {member.role}
                      </span>
                    )}
                    {isOwner && member.role !== "Owner" && !isDemo && (
                      <button
                        className="remove-action"
                        onClick={() =>
                          setRemoving({
                            userId: member.userId,
                            name: member.fullName,
                          })
                        }
                      >
                        Remove
                      </button>
                    )}
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState
                title="No members"
                description="No project members were returned."
              />
            )}
          </article>
          {canManage && (
            <article className="settings-card">
              <header>
                <div>
                  <h2>Pending invitations</h2>
                  <p>Invitations waiting for a response.</p>
                </div>
              </header>
              {invitations.data?.length ? (
                <div className="members-list">
                  {invitations.data.map((item) => (
                    <div className="member-row" key={item.id}>
                      <div>
                        <strong>{item.email}</strong>
                        <small>
                          Expires{" "}
                          {new Date(item.expiresAt).toLocaleDateString()}
                        </small>
                      </div>
                      <span className={`role-badge ${item.role.toLowerCase()}`}>
                        {item.role}
                      </span>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="muted-copy">No pending invitations.</p>
              )}
            </article>
          )}
        </section>
        {isOwner && !isDemo && (
          <aside className="danger-card">
            <h2>Danger zone</h2>
            <p>Soft-delete this project and revoke normal access.</p>
            <button
              className="ui-button danger"
              onClick={() => setDeleteOpen(true)}
            >
              Delete project
            </button>
          </aside>
        )}
      </div>
      <ProjectFormDialog
        open={editOpen}
        initial={{
          name: details.name,
          description: details.description,
          defaultLanguage: details.defaultLanguage,
          isPublic: details.isPublic,
        }}
        pending={update.isPending}
        onClose={() => setEditOpen(false)}
        onSubmit={save}
      />
      <Dialog
        open={deadlineOpen}
        onClose={() => setDeadlineOpen(false)}
        title="Extend project deadline"
        description="Only SuperAdmin can reopen an expired project. The new deadline must be later than the previous deadline."
        footer={
          <>
            <button
              className="ui-button ghost"
              onClick={() => setDeadlineOpen(false)}
            >
              Cancel
            </button>
            <button
              className="ui-button primary"
              disabled={!deadline || extendDeadline.isPending}
              onClick={() => void extend()}
            >
              {extendDeadline.isPending ? "Extending…" : "Extend deadline"}
            </button>
          </>
        }
      >
        <div className="feature-form">
          <label>
            New deadline
            <input
              type="datetime-local"
              min={new Date().toISOString().slice(0, 16)}
              value={deadline}
              onChange={(event) => setDeadline(event.target.value)}
            />
          </label>
        </div>
      </Dialog>
      <Dialog
        open={inviteOpen}
        onClose={() => setInviteOpen(false)}
        title="Invite a member"
        description="The invitation link will be copied after creation."
        footer={
          <>
            <button
              className="ui-button ghost"
              onClick={() => setInviteOpen(false)}
            >
              Cancel
            </button>
            <button
              className="ui-button primary"
              disabled={!email || invite.isPending}
              onClick={sendInvite}
            >
              {invite.isPending ? "Sending…" : "Create invitation"}
            </button>
          </>
        }
      >
        <div className="feature-form">
          <label>
            Email address
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="teammate@company.com"
            />
          </label>
          <label>
            Project role
            <select
              value={inviteRole}
              onChange={(event) =>
                setInviteRole(
                  event.target.value as
                    | "Admin"
                    | "Maintainer"
                    | "Developer"
                    | "Viewer",
                )
              }
            >
              <option>Viewer</option>
              <option>Developer</option>
              <option>Maintainer</option>
              <option>Admin</option>
            </select>
          </label>
        </div>
      </Dialog>
      <ConfirmDialog
        open={deleteOpen}
        title="Delete project?"
        description="The project will be soft-deleted and unavailable to members."
        confirmLabel="Delete project"
        destructive
        pending={remove.isPending}
        onClose={() => setDeleteOpen(false)}
        onConfirm={async () => {
          try {
            await remove.mutateAsync();
            show("Project deleted.");
            navigate("/projects");
          } catch (error) {
            show(
              error instanceof Error ? error.message : "Delete failed.",
              "error",
            );
          }
        }}
      />
      <ConfirmDialog
        open={Boolean(removing)}
        title={`Remove ${removing?.name ?? "member"}?`}
        description="They will immediately lose access to all project resources."
        confirmLabel="Remove member"
        destructive
        pending={removeMember.isPending}
        onClose={() => setRemoving(null)}
        onConfirm={async () => {
          if (!removing) return;
          try {
            await removeMember.mutateAsync(removing.userId);
            setRemoving(null);
            show("Member removed.");
          } catch (error) {
            show(
              error instanceof Error ? error.message : "Removal failed.",
              "error",
            );
          }
        }}
      />
    </main>
  );
}
