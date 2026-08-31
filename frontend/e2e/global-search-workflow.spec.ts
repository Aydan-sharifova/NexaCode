import { expect, test } from "@playwright/test";

test("global search filters, paginates, supports keyboard navigation, and remembers queries", async ({ page }) => {
  const user = { id: "73000000-0000-0000-0000-000000000001", publicId: "searcher", userName: "searcher", firstName: "Global", lastName: "Searcher", email: "searcher@test.local", isEmailVerified: true, roles: ["User"], isDemo: false, demoRole: null, demoProjectId: null };
  const json = (route: any, body: unknown) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  const result = (id: string, title: string) => ({ type: "User", id, title, subtitle: `@${title.toLowerCase().replaceAll(" ", "-")}`, matchedText: title, navigationUrl: `/users/${id}`, rank: 3 });

  await page.route("**/api/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/auth/refresh")) return json(route, { accessToken: "search-e2e", accessTokenExpiresAt: "2099-01-01T00:00:00Z", user });
    if (path === "/api/notifications") return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path === "/api/search") {
      const url = new URL(route.request().url());
      const type = url.searchParams.get("type");
      const currentPage = Number(url.searchParams.get("page"));
      if (type === "User") return json(route, { query: "platform", page: currentPage, pageSize: 5, groups: [{ type: "User", hasMore: currentPage === 1, items: currentPage === 1 ? [result("alice", "Alice Platform")] : [result("bob", "Bob Platform")] }] });
      return json(route, { query: "platform", page: 1, pageSize: 5, groups: [
        { type: "Project", hasMore: false, items: [{ type: "Project", id: "project-one", title: "Platform API", subtitle: "C#", projectId: "project-one", matchedText: "Platform API", navigationUrl: "/public/projects/project-one", rank: 3 }] },
        { type: "User", hasMore: true, items: [result("alice", "Alice Platform")] },
      ] });
    }
    return json(route, []);
  });

  await page.goto("/projects");
  await page.locator(".dashboard-search").click();
  const palette = page.getByRole("dialog", { name: "Global search" });
  await palette.getByPlaceholder("Search projects, files, users and tasks…").fill("platform");
  await expect(palette.locator("b", { hasText: "Platform API" })).toBeVisible();
  await palette.getByRole("button", { name: "Users", exact: true }).click();
  await expect(palette.locator("b", { hasText: "Alice Platform" })).toBeVisible();
  await palette.getByRole("button", { name: "Load more" }).click();
  await expect(palette.locator("b", { hasText: "Bob Platform" })).toBeVisible();
  await palette.getByPlaceholder("Search projects, files, users and tasks…").press("ArrowDown");
  await palette.getByPlaceholder("Search projects, files, users and tasks…").press("Enter");
  await expect(page).toHaveURL(/\/users\/bob$/);

  await page.goto("/projects");
  await page.locator(".dashboard-search").click();
  await expect(page.getByRole("dialog", { name: "Global search" }).getByRole("button", { name: /platform/ })).toBeVisible();
});
