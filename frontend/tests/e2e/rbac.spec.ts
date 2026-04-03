import { test, expect, Page } from "@playwright/test";

const ADMIN_EMAIL = process.env.TEST_EMAIL ?? "admin@deployflow.dev";
const ADMIN_PASSWORD = process.env.TEST_PASSWORD ?? "Admin@1234";

// Developer-role account credentials (lower privileges)
const DEV_EMAIL = process.env.TEST_DEV_EMAIL ?? "dev@deployflow.dev";
const DEV_PASSWORD = process.env.TEST_DEV_PASSWORD ?? "Dev@1234";

async function loginAs(page: Page, email: string, password: string) {
  await page.goto("/login");
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/password/i).fill(password);
  await page.getByRole("button", { name: /sign in/i }).click();
  await expect(page).toHaveURL(/\/(dashboard|projects)/, { timeout: 10000 });
}

test.describe("RBAC — Admin access", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);
  });

  test("admin can access /team page", async ({ page }) => {
    await page.goto("/team");
    // Should NOT redirect to 403/forbidden
    await expect(page).not.toHaveURL(/forbidden|403/);
    await expect(
      page.getByRole("heading", { name: /team/i }).or(page.getByText(/members/i)),
    ).toBeVisible({ timeout: 8000 });
  });

  test("admin can access /settings page", async ({ page }) => {
    await page.goto("/settings");
    await expect(page).not.toHaveURL(/forbidden|403/);
    await expect(
      page.getByRole("heading", { name: /settings/i }),
    ).toBeVisible({ timeout: 8000 });
  });

  test("admin sees invite member button on /team", async ({ page }) => {
    await page.goto("/team");
    await expect(
      page.getByRole("button", { name: /invite|add.*member/i }),
    ).toBeVisible({ timeout: 8000 });
  });
});

test.describe("RBAC — Navigation items", () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);
  });

  test("sidebar contains expected nav items", async ({ page }) => {
    await page.goto("/dashboard");
    const sidebar = page.locator("nav, aside").first();
    const navItems = ["Projects", "Deployments", "Servers"];
    for (const item of navItems) {
      await expect(
        sidebar.getByRole("link", { name: new RegExp(item, "i") }).first(),
      ).toBeVisible({ timeout: 8000 });
    }
  });

  test("costs page is accessible to admin", async ({ page }) => {
    await page.goto("/costs");
    await expect(page).not.toHaveURL(/forbidden|403/);
    // Either shows content or a loading state
    await expect(
      page.locator("main").first(),
    ).toBeVisible({ timeout: 8000 });
  });
});
