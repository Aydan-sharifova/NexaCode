import { expect, test } from "@playwright/test";

test("developer manages files, edits, previews, commits, and branches", async ({
  page,
}) => {
  const projectId = "d0000000-0000-0000-0000-00000000000d";
  const fileId = "e0000000-0000-0000-0000-00000000000e";
  const folderId = "f0000000-0000-0000-0000-00000000000f";
  const nestedFileId = "f1000000-0000-0000-0000-00000000000f";
  const versionOneId = "a1000000-0000-0000-0000-000000000001";
  const versionTwoId = "a2000000-0000-0000-0000-000000000002";
  const user = {
    id: "1",
    publicId: "ide-dev",
    userName: "ide-dev",
    firstName: "IDE",
    lastName: "Developer",
    email: "ide@test.local",
    isEmailVerified: true,
    roles: ["User"],
    isDemo: false,
    demoRole: null,
    demoProjectId: null,
  };
  let content = "<!doctype html><html><body><h1>Original</h1></body></html>";
  let token = "token-1";
  let dirty = false;
  let committed = false;
  let branches = [{ name: "main", isCurrent: true }];
  let nodes: any[] = [
    { id: fileId, projectId, name: "index.html", nodeType: "File", path: "/index.html", hasChildren: false, createdAt: "2026-01-01T00:00:00Z" },
  ];
  const commit = {
    sha: "a".repeat(40),
    shortSha: "aaaaaaa",
    authorName: "IDE Developer",
    message: "Update preview",
    committedAt: "2026-08-26T00:00:00Z",
  };
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
        accessToken: "ide-e2e",
        accessTokenExpiresAt: "2099-01-01T00:00:00Z",
        user,
      });
    if (path.endsWith("/notifications"))
      return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path === `/api/projects/${projectId}`)
      return json(route, {
        id: projectId,
        name: "IDE workflow",
        defaultLanguage: "HTML",
        isPublic: true,
        ownerId: user.id,
        currentUserRole: "Owner",
        createdAt: "2026-01-01T00:00:00Z",
        status: "Active",
        isReadOnly: false,
      });
    if (path.endsWith(`/projects/${projectId}/nodes`)) return json(route, nodes);
    if (path.endsWith(`/projects/${projectId}/folders`) && request.method() === "POST") {
      const body = request.postDataJSON();
      const node = { id: folderId, projectId, parentId: body.parentId, name: body.name, nodeType: "Folder", path: `/${body.name}`, hasChildren: false, createdAt: "2026-08-26T00:00:00Z" };
      nodes.push(node);
      return json(route, node);
    }
    if (path.endsWith(`/projects/${projectId}/files`) && request.method() === "POST") {
      const body = request.postDataJSON();
      const parent = nodes.find((node) => node.id === body.parentId);
      const node = { id: nestedFileId, projectId, parentId: body.parentId, name: body.name, nodeType: "File", path: `${parent?.path ?? ""}/${body.name}`, hasChildren: false, createdAt: "2026-08-26T00:00:00Z" };
      nodes.push(node);
      return json(route, node);
    }
    if (path.endsWith(`/nodes/${folderId}/name`) && request.method() === "PUT") {
      const node = nodes.find((item) => item.id === folderId)!;
      node.name = request.postDataJSON().name;
      node.path = `/${node.name}`;
      nodes.filter((item) => item.parentId === folderId).forEach((item) => { item.path = `${node.path}/${item.name}`; });
      return json(route, node);
    }
    if (path.endsWith(`/nodes/${folderId}`) && request.method() === "DELETE") {
      nodes = nodes.filter((item) => item.id !== folderId && item.parentId !== folderId);
      return route.fulfill({ status: 204 });
    }
    if (path.endsWith(`/files/${fileId}/versions/${versionOneId}/restore`) && request.method() === "POST") {
      content = "<!doctype html><html><body><h1>Original</h1></body></html>";
      token = "token-restored";
      dirty = true;
      return json(route, { nodeId: fileId, path: "/index.html", content, isBinary: false, contentHash: "restored", concurrencyToken: token, versionNumber: 3, updatedAt: "2026-08-26T00:01:00Z" });
    }
    if (path.endsWith(`/files/${fileId}/versions`))
      return json(route, [
        { id: versionTwoId, nodeId: fileId, versionNumber: 2, contentHash: "updated", createdById: user.id, createdBy: "IDE Developer", createdAt: "2026-08-26T00:00:00Z" },
        { id: versionOneId, nodeId: fileId, versionNumber: 1, contentHash: "original", createdById: user.id, createdBy: "IDE Developer", createdAt: "2026-01-01T00:00:00Z" },
      ]);
    if (
      path.endsWith(`/files/${fileId}/content`) &&
      request.method() === "PUT"
    ) {
      const body = request.postDataJSON();
      content = body.content;
      token = "token-2";
      dirty = true;
      return json(route, {
        nodeId: fileId,
        path: "/index.html",
        content,
        isBinary: false,
        contentHash: "updated",
        concurrencyToken: token,
        versionNumber: 2,
        updatedAt: "2026-08-26T00:00:00Z",
      });
    }
    if (path.endsWith(`/files/${fileId}/content`))
      return json(route, {
        nodeId: fileId,
        path: "/index.html",
        content,
        isBinary: false,
        contentHash: "original",
        concurrencyToken: token,
        versionNumber: 1,
        updatedAt: "2026-01-01T00:00:00Z",
      });
    if (path.endsWith("/repository/status"))
      return json(route, {
        currentBranch: "main",
        isClean: !dirty,
        files: dirty
          ? [{ path: "index.html", indexStatus: " ", workingTreeStatus: "M" }]
          : [],
      });
    if (path.endsWith("/repository/commits") && request.method() === "POST") {
      committed = true;
      dirty = false;
      return json(route, commit);
    }
    if (path.endsWith("/repository/commits"))
      return json(route, committed ? [commit] : []);
    if (path.endsWith("/repository/branches") && request.method() === "POST") {
      const name = request.postDataJSON().name;
      branches = [...branches, { name, isCurrent: false }];
      return json(route, null);
    }
    if (path.endsWith("/repository/branches")) return json(route, branches);
    if (path.endsWith("/repository/diff"))
      return json(route, { patch: dirty ? "+<h1>Edited in IDE</h1>" : "" });
    return json(route, []);
  });

  await page.goto(`/projects/${projectId}/workspace`);

  await page.getByRole("button", { name: "New folder" }).click();
  await page.getByLabel("Name").fill("src");
  await page.getByRole("button", { name: "Create", exact: true }).click();
  await expect(page.getByText("Folder created.")).toBeVisible();
  const folder = page.getByRole("treeitem", { name: /src/ });
  await folder.click();
  await page.getByRole("button", { name: "New file", exact: true }).click();
  await page.getByLabel("Name").fill("app.js");
  await page.getByRole("button", { name: "Create", exact: true }).click();
  await folder.dblclick();
  await expect(page.getByRole("treeitem", { name: /app\.js/ })).toBeVisible();
  await folder.press("F2");
  await page.locator(".inline-rename").fill("source");
  await page.locator(".inline-rename").press("Enter");
  await expect(page.getByText("Folder renamed.")).toBeVisible();

  await page.getByRole("treeitem", { name: /index\.html/ }).dblclick();
  await expect(
    page.getByText("index.html", { exact: true }).last(),
  ).toBeVisible();

  await page.locator(".monaco-editor .view-lines").click({ position: { x: 120, y: 18 } });
  await page.keyboard.press(process.platform === "darwin" ? "Meta+A" : "Control+A");
  await page.keyboard.insertText(
    "<!doctype html><html><body><h1>Edited in IDE</h1></body></html>",
  );
  await page.getByRole("button", { name: /Run/, exact: true }).first().click();
  await expect(page.getByTitle("Preview of index.html")).toBeVisible();
  await expect(
    page
      .getByTitle("Preview of index.html")
      .contentFrame()
      .getByRole("heading", { name: "Edited in IDE" }),
  ).toBeVisible();

  await page.getByRole("button", { name: "Collaboration" }).click();
  await expect(page.getByText("2 versions")).toBeVisible();
  await page.locator(".version-list article").filter({ hasText: "Version 1" }).getByRole("button", { name: "Restore" }).click();
  await page.getByRole("button", { name: "Restore version" }).click();
  await expect(page.getByText("Version restored.")).toBeVisible();
  await page.getByRole("button", { name: /Run/, exact: true }).first().click();
  await expect(page.getByTitle("Preview of index.html").contentFrame().getByRole("heading", { name: "Original" })).toBeVisible();

  await page.getByRole("button", { name: /SOURCE/ }).click();
  await page.getByLabel("Commit message").fill("Update preview");
  await page.getByRole("button", { name: "Commit", exact: true }).click();
  await expect(page.getByText("Commit aaaaaaa created.")).toBeVisible();

  await page.getByRole("button", { name: "branches", exact: true }).click();
  await page.getByLabel("New branch name").fill("feature/e2e");
  await page.getByRole("button", { name: "Create branch" }).click();
  await expect(page.getByText("Branch feature/e2e created.")).toBeVisible();

  await page.getByRole("button", { name: /EXPLORER/ }).click();
  const renamedFolder = page.getByRole("treeitem", { name: /source/ });
  await renamedFolder.press("Delete");
  await page.getByRole("button", { name: "Delete", exact: true }).click();
  await expect(page.getByText("Folder deleted.")).toBeVisible();
  await expect(renamedFolder).toHaveCount(0);
});
