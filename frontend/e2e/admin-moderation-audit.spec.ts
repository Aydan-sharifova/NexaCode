import { expect, test } from "@playwright/test";

test("moderator enforces reported content and reviews its audit event", async ({ page }) => {
  const moderator = { id: "1", publicId: "mod", userName: "mod", firstName: "Trust", lastName: "Moderator", email: "mod@test.local", isEmailVerified: true, roles: ["Admin", "Moderator"], isDemo: false, demoRole: null, demoProjectId: null };
  let state = "Pending", lastAction = "", lastNote = "";
  const report = () => ({ id: "80000000-0000-0000-0000-000000000008", reporter: { id: "2", publicId: "reporter", userName: "reporter", fullName: "Reporter" }, targetType: "Post", targetId: "9", targetLabel: "Unsafe post", reason: "Dangerous content", state, assignedModerator: state === "Pending" ? undefined : { id: "1", publicId: "mod", userName: "mod", fullName: "Trust Moderator" }, createdAt: "2026-08-24T00:00:00Z", actions: [] });
  const json = (route: any, body: unknown) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", async route => {
    const request = route.request(), url = new URL(request.url()), path = url.pathname;
    if (path.endsWith("/auth/refresh")) return json(route, { accessToken: "mod-e2e", accessTokenExpiresAt: "2099-01-01T00:00:00Z", user: moderator });
    if (path.endsWith("/notifications")) return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path.endsWith("/moderation/reports") && request.method() === "GET") { const requested = url.searchParams.get("state"); return json(route, { items: requested === state ? [report()] : [], total: requested === state ? 1 : 0, page: 1, pageSize: 30 }); }
    if (path.endsWith("/actions")) { const input = request.postDataJSON(); lastAction = input.action; lastNote = input.note; state = input.action === "StartReview" ? "Reviewing" : input.action === "RemoveContent" ? "ActionTaken" : state; return json(route, report()); }
    if (path.endsWith("/admin/activities")) return json(route, { items: [{ id: "a1", userName: "mod", actionType: "ModerationRemoveContent", entityType: "Post", entityId: "9", description: "Reported content removed after review.", metadata: {}, ipAddress: "127.0.0.1", createdAt: "2026-08-24T00:05:00Z" }], total: 1, page: 1, pageSize: 20 });
    return json(route, []);
  });

  const notes = ["Investigating verified report", "Removal supported by evidence"];
  page.on("dialog", dialog => dialog.accept(notes.shift() ?? ""));
  await page.goto("/moderation");
  await expect(page.getByRole("heading", { name: "Unsafe post" })).toBeVisible();
  await page.getByRole("button", { name: "Start review" }).click();
  await expect.poll(() => lastAction).toBe("StartReview");
  await page.locator(".moderation-page nav select").first().selectOption("Reviewing");
  await page.getByRole("button", { name: "Remove content" }).click();
  await expect.poll(() => ({ action: lastAction, note: lastNote })).toEqual({ action: "RemoveContent", note: "Removal supported by evidence" });
  await page.goto("/admin/activity");
  await expect(page.getByText("ModerationRemoveContent")).toBeVisible();
  await expect(page.getByText("Reported content removed after review.")).toBeVisible();
});
