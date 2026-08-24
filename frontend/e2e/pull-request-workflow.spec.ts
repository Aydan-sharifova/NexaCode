import { expect, test } from "@playwright/test";

test("maintainer can review and merge a protected-branch pull request", async ({ page }) => {
  const projectId = "11111111-1111-1111-1111-111111111111";
  const reviewerId = "22222222-2222-2222-2222-222222222222";
  const author = { id: "33333333-3333-3333-3333-333333333333", publicId: "author1", userName: "author", fullName: "Feature Author" };
  const reviewer = { id: reviewerId, publicId: "review1", userName: "reviewer", fullName: "Review Maintainer" };
  let approved = false;
  let merged = false;
  const listItem = () => ({
    id: "44444444-4444-4444-4444-444444444444", number: 7, title: "Add branch-safe preview", sourceBranch: "feature/preview", targetBranch: "main",
    sourceHeadSha: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", status: merged ? "Merged" : "Open", author,
    approvalCount: approved ? 1 : 0, requiredApprovals: 1, unresolvedBlockingComments: 0, requirePassingTests: false,
    createdAt: "2026-08-23T00:00:00Z", updatedAt: "2026-08-23T00:00:00Z",
  });
  const details = () => ({
    pullRequest: listItem(), description: "Keep workspace state isolated per branch.", targetHeadSha: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
    mergeCommitSha: merged ? "cccccccccccccccccccccccccccccccccccccccc" : undefined,
    mergedAt: merged ? "2026-08-23T00:05:00Z" : undefined,
    reviews: approved ? [{ id: "55555555-5555-5555-5555-555555555555", reviewer, decision: "Approved", reviewedSourceSha: listItem().sourceHeadSha, updatedAt: "2026-08-23T00:03:00Z" }] : [],
    comments: [], mergeBlockReasons: approved && !merged ? [] : merged ? ["Pull request is not open."] : ["1 required approval(s) are missing."], canMerge: approved && !merged,
  });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const pathname = url.pathname;
    const json = (body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
    if (pathname.endsWith("/auth/refresh")) return json({ accessToken: "e2e-token", accessTokenExpiresAt: "2026-08-24T00:00:00Z", user: { id: reviewerId, firstName: "Review", lastName: "Maintainer", userName: "reviewer", email: "reviewer@e2e.local", isEmailVerified: true, roles: ["User"], isDemo: false, demoRole: null, demoProjectId: null } });
    if (pathname === `/api/projects/${projectId}`) return json({ id: projectId, name: "E2E IDE", description: "Review test", defaultLanguage: "C#", isPublic: false, ownerId: author.id, currentUserRole: "Maintainer", createdAt: "2026-08-23T00:00:00Z", status: "Active", isReadOnly: false });
    if (pathname.endsWith("/pull-requests/policy")) return json({ protectedBranch: "main", requiredApprovals: 1, requirePassingTests: false });
    if (pathname.endsWith("/repository/branches")) return json([{ name: "main", isCurrent: false }, { name: "feature/preview", isCurrent: true }]);
    if (pathname.endsWith("/pull-requests/7/diff")) return json({ sourceHeadSha: listItem().sourceHeadSha, targetHeadSha: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", patch: "diff --git a/app.ts b/app.ts\n+branch-safe preview" });
    if (pathname.endsWith("/pull-requests/7/review") && request.method() === "PUT") { approved = true; return json(details()); }
    if (pathname.endsWith("/pull-requests/7/merge") && request.method() === "POST") { merged = true; return json(details()); }
    if (pathname.endsWith("/pull-requests/7")) return json(details());
    if (pathname.endsWith("/pull-requests")) return json(merged && url.searchParams.get("status") === "Open" ? [] : [listItem()]);
    if (pathname.endsWith("/notifications")) return json({ items: [], total: 0, unreadCount: 0 });
    return json([]);
  });

  await page.goto(`/projects/${projectId}/pull-requests?number=7`);
  await expect(page.getByRole("heading", { name: "Pull requests" })).toBeVisible();
  await expect(page.getByRole("heading", { name: /#7 Add branch-safe preview/ })).toBeVisible();
  await expect(page.getByText("1 required approval(s) are missing.")).toBeVisible();
  await expect(page.getByText("+branch-safe preview", { exact: false })).toBeVisible();
  await expect(page.getByRole("button", { name: "Merge", exact: true })).toBeDisabled();

  await page.getByRole("button", { name: "Approve" }).click();
  await expect(page.getByText("Ready to merge")).toBeVisible();
  await expect(page.getByRole("button", { name: "Merge", exact: true })).toBeEnabled();
  await page.getByRole("button", { name: "Merge", exact: true }).click();

  await expect(page.getByText("Pull request merged.")).toBeVisible();
  await expect(page.getByText("Merged", { exact: true }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "Merge", exact: true })).toBeDisabled();
});
