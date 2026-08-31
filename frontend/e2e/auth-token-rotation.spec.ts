import { expect, test } from "@playwright/test";

test("expired access token is rotated and the interrupted request is replayed", async ({ page }) => {
  const user = { id: "74000000-0000-0000-0000-000000000001", publicId: "rotation", userName: "rotation", firstName: "Token", lastName: "Rotation", email: "rotation@test.local", isEmailVerified: true, roles: ["User"], isDemo: false, demoRole: null, demoProjectId: null };
  let refreshes = 0; let rejectedSearches = 0; let rotatedSearches = 0;
  const json = (route: any, body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });

  await page.route("**/api/**", async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/auth/refresh")) {
      refreshes++;
      return json(route, { accessToken: refreshes === 1 ? "initial-token" : "rotated-token", accessTokenExpiresAt: "2099-01-01T00:00:00Z", user });
    }
    if (path === "/api/notifications") return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path === "/api/search") {
      const authorization = route.request().headers().authorization;
      if (authorization === "Bearer initial-token") { rejectedSearches++; return json(route, { title: "Unauthorized" }, 401); }
      if (authorization === "Bearer rotated-token") { rotatedSearches++; return json(route, { query: "rotate", page: 1, pageSize: 5, groups: [{ type: "Project", hasMore: false, items: [{ type: "Project", id: "rotated-project", title: "Rotated session project", subtitle: "C#", projectId: "rotated-project", matchedText: "rotate", navigationUrl: "/public/projects/rotated-project", rank: 3 }] }] }); }
    }
    return json(route, []);
  });

  await page.goto("/projects");
  await page.locator(".dashboard-search").click();
  await page.getByPlaceholder("Search projects, files, users and tasks…").fill("rotate");
  await expect(page.getByText("Rotated session project")).toBeVisible();
  expect(refreshes).toBe(2);
  expect(rejectedSearches).toBe(1);
  expect(rotatedSearches).toBe(1);
});
