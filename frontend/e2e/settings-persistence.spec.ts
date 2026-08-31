import { expect, test } from "@playwright/test";

test("profile, appearance, and notification preferences persist", async ({ page }) => {
  const userId = "72000000-0000-0000-0000-000000000001";
  const publicId = "settings-owner";
  const user = { id: userId, publicId, userName: "settings-owner", firstName: "Settings", lastName: "Owner", email: "settings@test.local", isEmailVerified: true, roles: ["User"], isDemo: false, demoRole: null, demoProjectId: null };
  let settings = {
    profile: { id: userId, publicId, firstName: "Settings", lastName: "Owner", userName: user.userName, email: user.email, bio: "Initial bio" },
    preferences: { theme: "system", language: "en", reducedMotion: false, compactMode: false, securityAlertsEnabled: true },
    notifications: [{ type: "ProjectInvitation", inAppEnabled: true, emailEnabled: false }],
  };
  let profile: any = { id: userId, publicId, userName: user.userName, displayName: "Settings Owner", publicProjectCount: 0, joinedAt: "2026-01-01T00:00:00Z", skills: [], learningTopics: [], isFollowing: false, isBlockedByMe: false, isOwnProfile: true, isProfilePublic: true, isActivityPublic: true, areFollowersPublic: true };
  let passwordChanged = false;
  let sessions = [
    { id: "72000000-0000-0000-0000-000000000010", device: "Chrome on macOS", ipAddress: "127.0.0.1", createdAt: "2026-08-01T00:00:00Z", lastSeenAt: "2026-08-26T00:00:00Z", expiresAt: "2026-09-01T00:00:00Z", isCurrent: true },
    { id: "72000000-0000-0000-0000-000000000011", device: "Firefox on Linux", ipAddress: "10.0.0.2", createdAt: "2026-08-02T00:00:00Z", lastSeenAt: "2026-08-25T00:00:00Z", expiresAt: "2026-09-02T00:00:00Z", isCurrent: false },
  ];
  const json = (route: any, body: unknown) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

  await page.route("**/api/**", async (route) => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    if (path.endsWith("/auth/refresh")) return json(route, { accessToken: "settings-e2e", accessTokenExpiresAt: "2099-01-01T00:00:00Z", user });
    if (path === "/api/notifications") return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path === "/api/settings" && request.method() === "GET") return json(route, settings);
    if (path === `/api/users/${publicId}/profile`) return json(route, profile);
    if (path === "/api/settings/profile" && request.method() === "PUT") { settings.profile = { ...settings.profile, ...request.postDataJSON() }; return json(route, settings.profile); }
    if (path === "/api/users/profile" && request.method() === "PUT") { profile = { ...profile, ...request.postDataJSON() }; return json(route, profile); }
    if (path === "/api/settings/preferences" && request.method() === "PUT") { settings.preferences = request.postDataJSON(); return json(route, settings.preferences); }
    if (path === "/api/settings/notifications" && request.method() === "PUT") { settings.notifications = request.postDataJSON().preferences; return route.fulfill({ status: 204 }); }
    if (path === "/api/settings/password" && request.method() === "PUT") { passwordChanged = request.postDataJSON().revokeOtherSessions === true; return route.fulfill({ status: 204 }); }
    if (path === "/api/settings/sessions" && request.method() === "GET") return json(route, sessions);
    if (path === "/api/settings/sessions/72000000-0000-0000-0000-000000000011" && request.method() === "DELETE") { sessions = sessions.filter((item) => item.isCurrent); return route.fulfill({ status: 204 }); }
    return json(route, []);
  });

  await page.goto("/settings");
  await page.getByLabel("Public display name").fill("Production Developer");
  await page.getByLabel("Skills").fill("C#, React, C#");
  await page.getByRole("button", { name: /Save profile/ }).click();
  await expect(page.getByText("Profile updated.")).toBeVisible();
  expect(profile.displayName).toBe("Production Developer");
  expect(profile.skills).toEqual(["C#", "React"]);

  await page.getByRole("button", { name: "Appearance" }).click();
  await page.getByRole("combobox").first().selectOption("dark");
  await expect(page.getByText("Preferences saved.")).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  expect(settings.preferences.theme).toBe("dark");

  await page.locator(".settings-console > nav").getByRole("button", { name: "Notifications", exact: true }).click();
  const notificationRow = page.locator(".notification-settings > div", { hasText: "Project Invitation" });
  await notificationRow.locator('input[type="checkbox"]').nth(1).click();
  await expect(page.getByText("Notification preferences saved.")).toBeVisible();
  await expect(notificationRow.locator('input[type="checkbox"]').nth(1)).toBeChecked();
  expect(settings.notifications[0].emailEnabled).toBe(true);

  await page.locator(".settings-console > nav").getByRole("button", { name: "Security", exact: true }).click();
  await expect(page.getByText("Firefox on Linux")).toBeVisible();
  await page.getByLabel("Current password").fill("Current#123");
  await page.getByLabel("New password").fill("NewSecure#123");
  await page.getByLabel("Confirm password").fill("NewSecure#123");
  await page.getByRole("button", { name: "Change password" }).click();
  await expect(page.getByText("Password changed.")).toBeVisible();
  expect(passwordChanged).toBe(true);
  await page.getByRole("button", { name: "Revoke", exact: true }).click();
  await expect(page.getByText("Session revoked.")).toBeVisible();
  await expect(page.getByText("Firefox on Linux")).toHaveCount(0);
});
