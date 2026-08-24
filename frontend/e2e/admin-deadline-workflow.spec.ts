import { expect, test } from "@playwright/test";

test("SuperAdmin reopens an expired project with a later deadline", async ({ page }) => {
  const projectId = "90000000-0000-0000-0000-000000000009";
  const user = { id: "1", publicId: "root", userName: "root", firstName: "Super", lastName: "Admin", email: "root@test.local", isEmailVerified: true, roles: ["SuperAdmin"], isDemo: false, demoRole: null, demoProjectId: null };
  let deadlinePayload: any;
  const json = (route: any, body: unknown) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    if (path.endsWith("/auth/refresh")) return json(route, { accessToken: "root-e2e", accessTokenExpiresAt: "2099-01-01T00:00:00Z", user });
    if (path.endsWith("/notifications")) return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path.endsWith(`/projects/${projectId}/deadline`)) { deadlinePayload = request.postDataJSON(); return json(route, { projectId, deadlineAt: deadlinePayload.deadlineAt, status: "Active" }); }
    if (path.endsWith(`/projects/${projectId}/members`) || path.endsWith(`/projects/${projectId}/invitations`)) return json(route, []);
    if (path.endsWith(`/projects/${projectId}`)) return json(route, { id: projectId, name: "Expired production project", description: "Deadline policy", defaultLanguage: "C#", isPublic: false, ownerId: "2", currentUserRole: "Viewer", createdAt: "2026-01-01T00:00:00Z", deadlineAt: "2026-08-20T00:00:00Z", status: "DeadlineExpired", isReadOnly: true });
    return json(route, []);
  });

  await page.goto(`/projects/${projectId}/settings`);
  await page.getByRole("button", { name: "Extend deadline" }).click();
  await page.getByLabel("New deadline").fill("2026-08-30T12:00");
  await page.getByRole("button", { name: "Extend deadline" }).last().click();
  await expect.poll(() => deadlinePayload?.deadlineAt).toContain("2026-08-30");
  await expect(page.getByText("Project deadline extended.")).toBeVisible();
});
