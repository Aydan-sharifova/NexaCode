import { expect, test } from "@playwright/test";

test("AI suggestion is reviewed as a diff, approved, applied, previewed, and tested", async ({
  page,
}) => {
  const projectId = "f0000000-0000-0000-0000-00000000000f";
  const fileId = "f1000000-0000-0000-0000-00000000000f";
  const original =
    "<!doctype html><html><body><h1>Before AI</h1></body></html>";
  const suggested =
    "<!doctype html><html><body><h1>After AI</h1></body></html>";
  const actions: string[] = [];
  const json = (route: any, body: unknown) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify(body),
    });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path.endsWith("/auth/refresh"))
      return json(route, {
        accessToken: "ai-e2e",
        accessTokenExpiresAt: "2099-01-01T00:00:00Z",
        user: {
          id: "1",
          publicId: "ai-dev",
          userName: "ai-dev",
          firstName: "AI",
          lastName: "Developer",
          email: "ai@test.local",
          isEmailVerified: true,
          roles: ["User"],
          isDemo: false,
          demoRole: null,
          demoProjectId: null,
        },
      });
    if (path.endsWith("/notifications"))
      return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path.endsWith("/ai/stream")) {
      const requestBody = request.postDataJSON();
      actions.push(requestBody.action);
      const content =
        requestBody.action === "GenerateTests"
          ? "Test plan: verify that the rendered heading is After AI."
          : `Plan: replace the heading while preserving the document structure.\n\n\`\`\`html\n${suggested}\n\`\`\``;
      return route.fulfill({
        contentType: "text/event-stream",
        body: `data: ${JSON.stringify({ content, isCompleted: true, conversationId: "conversation-1" })}\n\n`,
      });
    }
    if (path === `/api/projects/${projectId}`)
      return json(route, {
        id: projectId,
        name: "AI approval",
        defaultLanguage: "HTML",
        isPublic: false,
        ownerId: "1",
        currentUserRole: "Owner",
        createdAt: "2026-01-01T00:00:00Z",
        status: "Active",
        isReadOnly: false,
      });
    if (path.endsWith(`/projects/${projectId}/nodes`))
      return json(route, [
        {
          id: fileId,
          projectId,
          name: "index.html",
          nodeType: "File",
          path: "/index.html",
          hasChildren: false,
          createdAt: "2026-01-01T00:00:00Z",
        },
      ]);
    if (path.endsWith(`/files/${fileId}/content`) && request.method() === "PUT")
      return json(route, {
        nodeId: fileId,
        path: "/index.html",
        content: request.postDataJSON().content,
        isBinary: false,
        contentHash: "saved",
        concurrencyToken: "token-2",
        versionNumber: 2,
        updatedAt: "2026-08-26T00:00:00Z",
      });
    if (path.endsWith(`/files/${fileId}/content`))
      return json(route, {
        nodeId: fileId,
        path: "/index.html",
        content: original,
        isBinary: false,
        contentHash: "original",
        concurrencyToken: "token-1",
        versionNumber: 1,
        updatedAt: "2026-01-01T00:00:00Z",
      });
    if (path.endsWith("/repository/status"))
      return json(route, { currentBranch: "main", isClean: true, files: [] });
    if (
      path.endsWith("/repository/commits") ||
      path.endsWith("/repository/branches")
    )
      return json(route, []);
    return json(route, []);
  });

  await page.goto(`/projects/${projectId}/workspace`);
  await page.getByRole("treeitem", { name: /index\.html/ }).dblclick();
  await page.getByRole("button", { name: "Fix", exact: true }).click();
  await expect(
    page.getByText("Plan: replace the heading", { exact: false }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Apply code" }).click();

  const dialog = page.getByRole("dialog", { name: "Apply AI suggestion?" });
  await expect(dialog.getByLabel("AI suggestion diff")).toContainText(
    "− <!doctype html",
  );
  await expect(dialog.getByLabel("AI suggestion diff")).toContainText(
    "+ <!doctype html",
  );
  await dialog.getByRole("button", { name: "Apply to editor" }).click();

  await page.getByRole("button", { name: /Run/, exact: true }).first().click();
  await expect(
    page
      .getByTitle("Preview of index.html")
      .contentFrame()
      .getByRole("heading", { name: "After AI" }),
  ).toBeVisible();

  await page.getByRole("button", { name: /AI/, exact: true }).click();
  await page.getByRole("button", { name: "Tests", exact: true }).click();
  await expect(
    page.getByText("Test plan: verify", { exact: false }),
  ).toBeVisible();
  expect(actions).toEqual(["SuggestFix", "GenerateTests"]);
});
