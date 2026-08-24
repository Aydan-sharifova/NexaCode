import { expect, test } from "@playwright/test";

test("SuperAdmin assigns a role and applies a timed suspension", async ({ page }) => {
  const targetId = "70000000-0000-0000-0000-000000000007";
  const user = { id: "1", publicId: "root", userName: "root", firstName: "Super", lastName: "Admin", email: "root@test.local", isEmailVerified: true, roles: ["SuperAdmin"], isDemo: false, demoRole: null, demoProjectId: null };
  let roles = ["User"], suspensionPayload: any, rolePayload: any;
  const suspensionCalls: any[] = [];
  const summary = () => ({ id: targetId, displayName: "Managed User", userName: "managed", email: "managed@test.local", isSuspended: Boolean(suspensionPayload?.suspended), banExpiresAt: suspensionPayload?.expiresAt, roles, createdAt: "2026-01-01T00:00:00Z", lastSeen: "2026-08-24T00:00:00Z" });
  const details = () => ({ ...summary(), firstName: "Managed", lastName: "User", projectCount: 2 });
  const json = (route: any, body: unknown) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    if (path.endsWith("/auth/refresh")) return json(route, { accessToken: "admin-e2e", accessTokenExpiresAt: "2099-01-01T00:00:00Z", user });
    if (path.endsWith("/notifications")) return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path.endsWith("/admin/statistics")) return json(route, { totalUsers: 2, activeUsers30Days: 2, suspendedUsers: suspensionPayload?.suspended ? 1 : 0, totalProjects: 1, projects30Days: 1, activity30Days: 5 });
    if (path.endsWith(`/admin/users/${targetId}/roles/Moderator`)) { rolePayload = request.postDataJSON(); roles = rolePayload.enabled ? [...new Set([...roles, "Moderator"])] : roles.filter(x => x !== "Moderator"); return json(route, null); }
    if (path.endsWith(`/admin/users/${targetId}/suspension`)) { suspensionPayload = request.postDataJSON(); suspensionCalls.push(suspensionPayload); return json(route, null); }
    if (path.endsWith(`/admin/users/${targetId}`)) return json(route, details());
    if (path.endsWith("/admin/users")) return json(route, { items: [summary()], total: 1, page: 1, pageSize: 20 });
    return json(route, []);
  });

  await page.goto("/admin");
  await page.getByRole("button", { name: "Managed User" }).click();
  await page.getByLabel("Moderator").click();
  await expect.poll(() => rolePayload).toEqual({ enabled: true });
  await page.locator(".admin-drawer > button").click();
  await page.getByRole("row", { name: /Managed User/ }).getByRole("button", { name: /Suspend|Blokla/i }).click();
  const answers = ["Security review", "24h"];
  page.on("dialog", async dialog => dialog.accept(answers.shift() ?? ""));
  await page.getByRole("button", { name: "Təsdiqlə" }).click();
  await expect.poll(() => suspensionPayload?.suspended).toBe(true);
  expect(suspensionPayload.reason).toBe("Security review");
  expect(new Date(suspensionPayload.expiresAt).getTime()).toBeGreaterThan(Date.now() + 23 * 3_600_000);
  await page.getByRole("row", { name: /Managed User/ }).getByRole("button", { name: /Activate|Aktivləşdir/i }).click();
  await page.getByRole("button", { name: "Təsdiqlə" }).click();
  await expect.poll(() => suspensionCalls.at(-1)?.suspended).toBe(false);
});
