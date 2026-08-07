import { expect, test } from "@playwright/test";

test("anonymous user can navigate between login and registration", async ({ page }) => {
  await page.goto("/login");
  await expect(page.getByRole("heading", { name: "Sign in to your account" })).toBeVisible();
  await page.getByRole("link", { name: "Create an account" }).click();
  await expect(page.getByRole("heading", { name: "Create your account" })).toBeVisible();
});

const authenticatedTest = process.env.E2E_USER_EMAIL && process.env.E2E_USER_PASSWORD
  ? test
  : test.skip;

authenticatedTest("login and logout smoke flow", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email address").fill(process.env.E2E_USER_EMAIL!);
  await page.getByLabel("Password").fill(process.env.E2E_USER_PASSWORD!);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/dashboard/);
  await page.goto("/settings");
  await page.getByRole("button", { name: /log out/i }).click();
  await expect(page).toHaveURL(/login/);
});
