import { expect, test } from "@playwright/test";

test("developer forks a discovered public repository into an editable project", async ({
  page,
}) => {
  const sourceId = "b0000000-0000-0000-0000-00000000000b",
    forkId = "c0000000-0000-0000-0000-00000000000c",
    user = {
      id: "1",
      publicId: "dev",
      userName: "dev",
      firstName: "Fork",
      lastName: "Developer",
      email: "fork@test.local",
      isEmailVerified: true,
      roles: ["User"],
      isDemo: false,
      demoRole: null,
      demoProjectId: null,
    };
  const json = (route: any, body: unknown) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify(body),
    });
  await page.addInitScript(() => {
    Object.defineProperty(navigator, "share", {
      configurable: true,
      value: ({ url }: ShareData) => {
        (window as typeof window & { __sharedUrl?: string }).__sharedUrl = url;
        return Promise.resolve();
      },
    });
  });
  await page.route("**/api/**", async (route) => {
    const request = route.request(),
      path = new URL(request.url()).pathname;
    if (path.endsWith("/auth/refresh"))
      return json(route, {
        accessToken: "fork-e2e",
        accessTokenExpiresAt: "2099-01-01T00:00:00Z",
        user,
      });
    if (path.endsWith("/notifications"))
      return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path.endsWith(`/public-projects/${sourceId}/fork`))
      return json(route, { projectId: forkId, sourceProjectId: sourceId });
    if (path.endsWith(`/public-projects/${sourceId}/tree`))
      return json(route, []);
    if (path.endsWith(`/public-projects/${sourceId}`))
      return json(route, {
        id: sourceId,
        name: "Public starter",
        description: "Forkable repository",
        defaultLanguage: "HTML",
        ownerPublicId: "owner",
        ownerDisplayName: "Project Owner",
        createdAt: "2026-01-01T00:00:00Z",
        updatedAt: "2026-08-25T00:00:00Z",
      });
    if (path.endsWith("/saved"))
      return json(route, { posts: [], projects: [], marketplaceItems: [] });
    return json(route, []);
  });
  page.on("dialog", (dialog) => dialog.accept());
  await page.goto(`/public/projects/${sourceId}`);
  await expect(
    page.getByRole("heading", { name: "Public starter" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Share", exact: true }).click();
  await expect
    .poll(() =>
      page.evaluate(
        () => (window as typeof window & { __sharedUrl?: string }).__sharedUrl,
      ),
    )
    .toMatch(new RegExp(`/public/projects/${sourceId}$`));
  await page.getByRole("button", { name: "Fork", exact: true }).click();
  await expect(page.getByText("Private fork created.")).toBeVisible();
  await expect(page).toHaveURL(new RegExp(`/projects/${forkId}/workspace$`));
});
