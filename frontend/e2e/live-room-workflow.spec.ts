import { expect, test } from "@playwright/test";

test("host invites, collaborates, and completes a live room", async ({ page }) => {
  const roomId = "50000000-0000-0000-0000-000000000005";
  const owner = { id: "10000000-0000-0000-0000-000000000001", publicId: "host", userName: "host", fullName: "Room Host" };
  const user = { ...owner, firstName: "Room", lastName: "Host", email: "host@test.local", isEmailVerified: true, roles: ["User"], isDemo: false, demoRole: null, demoProjectId: null };
  let status = "Scheduled", version = 1;
  let participants: any[] = [{ id: "p1", user: owner, role: "Owner", status: "Joined", invitedAt: "2026-08-24T00:00:00Z", joinedAt: "2026-08-24T00:00:00Z" }];
  let messages: any[] = [], tasks: any[] = [];
  const details = () => ({ room: { id: roomId, projectId: "60000000-0000-0000-0000-000000000006", title: "Production pairing", description: "Ship safely", mode: "PairProgramming", status, visibility: "ProjectMembers", durationMinutes: 60, stateVersion: version, owner, participantCount: participants.length, currentUserRole: "Owner", ...(status !== "Scheduled" ? { startedAt: "2026-08-24T00:00:00Z" } : {}), ...(status === "Completed" ? { completedAt: "2026-08-24T01:00:00Z" } : {}) }, participants, canManage: true, canStart: status === "Scheduled", canComplete: status === "Active" });
  const json = (route: any, body: unknown) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

  await page.route("**/api/**", async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    if (path.endsWith("/auth/refresh")) return json(route, { accessToken: "e2e", accessTokenExpiresAt: "2099-01-01T00:00:00Z", user });
    if (path.endsWith("/notifications")) return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path.endsWith(`/live-rooms/${roomId}/join`)) return json(route, details());
    if (path.endsWith(`/live-rooms/${roomId}/participants`)) { participants = [...participants, { id: "p2", user: { id: "2", publicId: "guest", userName: "guest", fullName: "Invited Guest" }, role: request.postDataJSON().role, status: "Invited", invitedAt: "2026-08-24T00:01:00Z" }]; return json(route, details()); }
    if (path.endsWith(`/live-rooms/${roomId}/status`)) { status = request.postDataJSON().status; version++; return json(route, details()); }
    if (path.endsWith(`/live-rooms/${roomId}/messages`) && request.method() === "POST") { const message = { id: "m1", roomId, author: owner, content: request.postDataJSON().content, sentAt: "2026-08-24T00:02:00Z" }; messages.push(message); return json(route, message); }
    if (path.endsWith(`/live-rooms/${roomId}/messages`)) return json(route, messages);
    if (path.endsWith(`/live-rooms/${roomId}/tasks`) && request.method() === "POST") { const task = { id: "t1", roomId, createdBy: owner, title: request.postDataJSON().title, status: "Open", createdAt: "2026-08-24T00:03:00Z" }; tasks.push(task); return json(route, task); }
    if (path.endsWith(`/live-rooms/${roomId}/tasks`)) return json(route, tasks);
    if (path.endsWith(`/live-rooms/${roomId}/leave`)) return json(route, null);
    return json(route, []);
  });

  await page.goto(`/live-rooms/${roomId}`);
  await expect(page.getByRole("heading", { name: "Production pairing" })).toBeVisible();
  await page.getByPlaceholder("@ABCD1234").fill("guest");
  await page.getByRole("button", { name: "Invite" }).click();
  await expect(page.getByText("Invited Guest")).toBeVisible();
  await page.getByRole("button", { name: "Start" }).click();
  await expect(page.getByText("Active", { exact: true })).toBeVisible();
  await page.getByLabel("New room task").fill("Run isolated tests");
  await page.getByRole("button", { name: "Add" }).click();
  await expect(page.getByText("Run isolated tests")).toBeVisible();
  await page.getByLabel("Room message").fill("Shared state verified");
  await page.getByRole("button", { name: "Send" }).click();
  await expect(page.getByText("Shared state verified")).toBeVisible();
  await page.getByRole("button", { name: "Complete" }).click();
  await expect(page.getByText("Completed", { exact: true })).toBeVisible();
  await expect(page.getByLabel("Room message")).toBeDisabled();
});
