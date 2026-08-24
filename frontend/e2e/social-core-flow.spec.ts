import { expect, test } from "@playwright/test";

test("profile follow, publish, like, comment, and save flow", async ({ page }) => {
  const user = { id: "10000000-0000-0000-0000-000000000001", publicId: "me", userName: "me", firstName: "E2E", lastName: "User", email: "e2e@test.local", isEmailVerified: true, roles: ["User"], isDemo: false, demoRole: null, demoProjectId: null };
  let following = false, liked = false, saved = false;
  let posts: any[] = [];
  let comments: any[] = [];
  const author = { id: user.id, publicId: user.publicId, userName: user.userName, displayName: "E2E User" };
  const json = (route: any, body: unknown, status = 200) => route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });

  await page.route("**/api/**", async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    if (path.endsWith("/auth/refresh")) return json(route, { accessToken: "e2e-token", accessTokenExpiresAt: "2099-01-01T00:00:00Z", user });
    if (path.endsWith("/notifications")) return json(route, { items: [], total: 0, unreadCount: 0 });
    if (path.endsWith("/users/developer/profile")) return json(route, { id: "20000000-0000-0000-0000-000000000002", publicId: "developer", userName: "developer", displayName: "Test Developer", bio: "Builds reliable tools", joinedAt: "2026-01-01T00:00:00Z", publicProjectCount: 0, skills: [], learningTopics: [], isProfilePublic: true, isActivityPublic: true, areFollowersPublic: true, followerCount: following ? 1 : 0, followingCount: 0, isFollowing: following, isOwnProfile: false, isBlockedByMe: false });
    if (path.endsWith("/users/developer/follow")) { following = request.method() === "POST"; return json(route, { isFollowing: following, followerCount: following ? 1 : 0 }); }
    if (path.endsWith("/users/developer/projects/public")) return json(route, { items: [], page: 1, pageSize: 20, hasMore: false });
    if (path.endsWith("/achievements/users/developer")) return json(route, { reputationScore: 0, contributionLevel: "Newcomer", unlockedCount: 0, achievements: [] });
    if (path.endsWith("/achievements/users/developer/journey")) return json(route, []);
    if (path.endsWith("/users/developer/portfolio")) return json(route, { activityVisible: true, posts: [], snippets: [], activity: [], followers: [], following: [] });
    if (path.endsWith("/feed/discover")) return json(route, { developers: [], projects: [], topics: [], rankingExplanation: "Chronological." });
    if (path === "/api/projects") return json(route, []);
    if (path === "/api/feed" && request.method() === "POST") {
      const input = request.postDataJSON();
      posts = [{ id: "30000000-0000-0000-0000-000000000003", type: input.type, content: input.content, author, createdAt: "2026-08-24T00:00:00Z", likeCount: 0, commentCount: 0, saveCount: 0, shareCount: 0, isLiked: false, isSaved: false, isOwner: true }];
      return json(route, posts[0]);
    }
    if (path === "/api/feed") return json(route, { items: posts, nextCursor: null });
    if (path.endsWith("/like")) { liked = !liked; posts[0] = { ...posts[0], isLiked: liked, likeCount: liked ? 1 : 0 }; return json(route, { active: liked, count: liked ? 1 : 0 }); }
    if (path.endsWith("/save")) { saved = !saved; posts[0] = { ...posts[0], isSaved: saved, saveCount: saved ? 1 : 0 }; return json(route, { active: saved, count: saved ? 1 : 0 }); }
    if (path.endsWith("/comments") && request.method() === "POST") { const input = request.postDataJSON(); comments = [{ id: "40000000-0000-0000-0000-000000000004", content: input.content, author, createdAt: "2026-08-24T00:00:00Z", isOwner: true }]; posts[0] = { ...posts[0], commentCount: 1 }; return json(route, comments[0]); }
    if (path.endsWith("/comments")) return json(route, { items: comments, nextCursor: null });
    return json(route, []);
  });

  await page.goto("/users/developer");
  await page.getByRole("button", { name: "Follow", exact: true }).click();
  await expect(page.getByRole("button", { name: "Following" })).toBeVisible();
  await page.goto("/feed");
  await page.getByLabel("Post content").fill("Production E2E post");
  await page.getByRole("button", { name: "Publish" }).click();
  await expect(page.getByRole("article").getByText("Production E2E post")).toBeVisible();
  await page.getByRole("button", { name: "♥ 0" }).click();
  await expect(page.getByRole("button", { name: "♥ 1" })).toBeVisible();
  await page.getByRole("button", { name: "◫ 0" }).click();
  await page.getByRole("textbox", { name: "Comment" }).fill("Verified comment");
  await page.getByRole("button", { name: "Post", exact: true }).click();
  await expect(page.getByText("Verified comment")).toBeVisible();
  await page.getByRole("button", { name: "⌑ 0" }).click();
  await expect(page.getByRole("button", { name: "⌑ 1" })).toBeVisible();
});
