import { expect, test } from "@playwright/test";

test("developer publishes an immutable static deployment", async ({ page }) => {
  const projectId = "a0000000-0000-0000-0000-00000000000a",
    user = {
      id: "1",
      publicId: "dev",
      userName: "dev",
      firstName: "Deploy",
      lastName: "Developer",
      email: "dev@test.local",
      isEmailVerified: true,
      roles: ["User"],
      isDemo: false,
      demoRole: null,
      demoProjectId: null,
    };
  let items: any[] = [];
  const json = (route: any, body: unknown) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify(body),
    });
  await page.route("**/api/**", async (route) => {
    const request = route.request(),
      path = new URL(request.url()).pathname;
    if (path.endsWith("/auth/refresh"))
      return json(route, {
        accessToken: "deploy-e2e",
        accessTokenExpiresAt: "2099-01-01T00:00:00Z",
        user,
      });
    if (path.endsWith("/notifications"))
      return json(route, { items: [], total: 0, unreadCount: 0 });
    if (
      path.endsWith(`/projects/${projectId}/deployments`) &&
      request.method() === "POST"
    ) {
      items = [
        {
          id: "d1",
          projectId,
          slug: "production-site-a1b2c3d4",
          version: 1,
          sourceHash: "a".repeat(64),
          commitSha: "b".repeat(40),
          deployedAt: "2026-08-24T00:00:00Z",
          isActive: true,
          url: "/deploy/production-site-a1b2c3d4/",
        },
      ];
      return json(route, items[0]);
    }
    if (path.endsWith(`/projects/${projectId}/deployments`))
      return json(route, items);
    if (path.endsWith(`/projects/${projectId}`))
      return json(route, {
        id: projectId,
        name: "Production site",
        defaultLanguage: "HTML",
        isPublic: true,
        ownerId: user.id,
        currentUserRole: "Developer",
        createdAt: "2026-01-01T00:00:00Z",
        status: "Active",
        isReadOnly: false,
      });
    return json(route, []);
  });
  page.on("dialog", (dialog) => dialog.accept());
  await page.goto(`/projects/${projectId}/deployments`);
  await expect(page.getByText("No deployments yet")).toBeVisible();
  await page.getByRole("button", { name: "Deploy current version" }).click();
  await expect(page.getByText("Deployment v1 published.")).toBeVisible();
  const link = page.getByRole("link", { name: /Open deployment/ });
  await expect(link).toHaveAttribute(
    "href",
    /\/deploy\/production-site-a1b2c3d4\/$/,
  );
  await expect(page.getByText("Live", { exact: true })).toBeVisible();
});
