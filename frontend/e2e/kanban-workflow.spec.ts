import { expect, test } from "@playwright/test";

test("owner creates, moves, assigns, comments, edits, and deletes a task", async ({ page }) => {
  const projectId = "71000000-0000-0000-0000-000000000001";
  const ownerId = "71000000-0000-0000-0000-000000000002";
  const memberId = "71000000-0000-0000-0000-000000000003";
  const taskId = "71000000-0000-0000-0000-000000000004";
  const user = { id: ownerId, publicId: "board-owner", userName: "board-owner", firstName: "Board", lastName: "Owner", email: "owner@test.local", isEmailVerified: true, roles: ["User"], isDemo: false, demoRole: null, demoProjectId: null };
  let tasks: any[] = [];
  const json = (route: any, body: unknown) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path.endsWith("/auth/refresh")) return json(route, { accessToken: "kanban-e2e", accessTokenExpiresAt: "2099-01-01T00:00:00Z", user });
    if (path.endsWith("/notifications")) return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path === `/api/projects/${projectId}`) return json(route, { id: projectId, name: "Release board", defaultLanguage: "TypeScript", isPublic: false, ownerId, currentUserRole: "Owner", createdAt: "2026-01-01T00:00:00Z", status: "Active", isReadOnly: false });
    if (path === `/api/projects/${projectId}/members`) return json(route, [
      { userId: ownerId, publicId: "board-owner", fullName: "Board Owner", email: user.email, role: "Owner", joinedAt: "2026-01-01T00:00:00Z" },
      { userId: memberId, publicId: "team-dev", fullName: "Team Developer", email: "dev@test.local", role: "Developer", joinedAt: "2026-01-01T00:00:00Z" },
    ]);
    if (path === `/api/projects/${projectId}/tasks` && request.method() === "POST") {
      const input = request.postDataJSON();
      const task = { id: taskId, projectId, ...input, status: "Todo", position: 1024, createdByUserId: ownerId, createdAt: "2026-08-26T00:00:00Z", updatedAt: "2026-08-26T00:00:00Z", assignees: [], comments: [] };
      tasks = [task]; return json(route, task);
    }
    if (path === `/api/projects/${projectId}/tasks`) return json(route, tasks);
    if (path === `/api/tasks/${taskId}/position`) { tasks[0].status = request.postDataJSON().status; return json(route, tasks[0]); }
    if (path === `/api/tasks/${taskId}/assignees/${memberId}` && request.method() === "POST") { tasks[0].assignees = [{ userId: memberId, displayName: "Team Developer" }]; return json(route, tasks[0]); }
    if (path === `/api/tasks/${taskId}/comments` && request.method() === "POST") {
      const comment = { id: "71000000-0000-0000-0000-000000000005", userId: ownerId, displayName: "Board Owner", content: request.postDataJSON().content, createdAt: "2026-08-26T00:01:00Z" };
      tasks[0].comments.push(comment); return json(route, comment);
    }
    if (path === `/api/tasks/${taskId}` && request.method() === "PUT") { tasks[0] = { ...tasks[0], ...request.postDataJSON() }; return json(route, tasks[0]); }
    if (path === `/api/tasks/${taskId}` && request.method() === "DELETE") { tasks = []; return route.fulfill({ status: 204 }); }
    return json(route, []);
  });

  await page.goto(`/projects/${projectId}/board`);
  await page.getByRole("button", { name: /New task/ }).click();
  await page.getByLabel("Title").fill("Ship Kanban workflow");
  await page.getByLabel("Description").fill("Verify the complete project-board lifecycle.");
  await page.getByLabel("Priority").selectOption("High");
  await page.getByRole("button", { name: "Save task" }).click();
  await expect(page.getByText("Task created.")).toBeVisible();

  const card = page.locator(".kanban-card", { hasText: "Ship Kanban workflow" });
  const sourceBox = await card.boundingBox();
  const targetBox = await page.locator(".kanban-column", { hasText: "In progress" }).boundingBox();
  if (!sourceBox || !targetBox) throw new Error("Kanban drag targets were not rendered.");
  await page.mouse.move(sourceBox.x + sourceBox.width / 2, sourceBox.y + sourceBox.height / 2);
  await page.mouse.down();
  await page.mouse.move(sourceBox.x + sourceBox.width / 2 + 12, sourceBox.y + sourceBox.height / 2, { steps: 4 });
  await page.mouse.move(targetBox.x + targetBox.width / 2, targetBox.y + 120, { steps: 12 });
  await page.mouse.up();
  await expect(page.locator(".kanban-column", { hasText: "In progress" }).getByText("Ship Kanban workflow")).toBeVisible();
  await card.click();
  await page.getByRole("combobox").selectOption(memberId);
  await expect(page.getByText("Member assigned.")).toBeVisible();
  await page.getByPlaceholder("Write a comment. Use @username to mention someone.").fill("Ready for review.");
  await page.getByRole("button", { name: "Add comment" }).click();
  await expect(page.getByText("Ready for review.")).toBeVisible();

  await page.getByRole("button", { name: "Edit task" }).click();
  await page.getByLabel("Title").fill("Ship production Kanban workflow");
  await page.getByRole("button", { name: "Save task" }).click();
  await expect(page.getByText("Task updated.")).toBeVisible();
  await page.getByRole("button", { name: "Delete task" }).click();
  await page.getByRole("button", { name: "Delete task" }).last().click();
  await expect(page.getByText("Task deleted.")).toBeVisible();
  await expect(page.getByText("Your board is ready")).toBeVisible();
});
