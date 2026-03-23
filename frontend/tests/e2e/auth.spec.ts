import { test, expect } from "@playwright/test";

const TEST_EMAIL = process.env.TEST_EMAIL ?? "admin@deployflow.dev";
const TEST_PASSWORD = process.env.TEST_PASSWORD ?? "Admin@1234";

test.describe("Authentication", () => {
  test("redirects unauthenticated users to /login", async ({ page }) => {
    await page.goto("/dashboard");
    await expect(page).toHaveURL(/\/login/);
  });

  test("shows validation errors on empty submit", async ({ page }) => {
    await page.goto("/login");
    await page.getByRole("button", { name: /sign in/i }).click();
    await expect(page.getByText(/email.*required|required/i).first()).toBeVisible();
  });

  test("shows error for invalid credentials", async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel(/email/i).fill("wrong@example.com");
    await page.getByLabel(/password/i).fill("wrongpassword");
    await page.getByRole("button", { name: /sign in/i }).click();
    await expect(
      page.getByText(/invalid credentials|unauthorized|incorrect/i).first(),
    ).toBeVisible({ timeout: 8000 });
  });

  test("successful login navigates to dashboard", async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel(/email/i).fill(TEST_EMAIL);
    await page.getByLabel(/password/i).fill(TEST_PASSWORD);
    await page.getByRole("button", { name: /sign in/i }).click();
    await expect(page).toHaveURL(/\/(dashboard|projects)/, { timeout: 10000 });
  });

  test("logout clears session and redirects to /login", async ({ page }) => {
    // Login first
    await page.goto("/login");
    await page.getByLabel(/email/i).fill(TEST_EMAIL);
    await page.getByLabel(/password/i).fill(TEST_PASSWORD);
    await page.getByRole("button", { name: /sign in/i }).click();
    await expect(page).toHaveURL(/\/(dashboard|projects)/, { timeout: 10000 });

    // Logout — typically a dropdown avatar item
    await page.getByRole("button", { name: /user|avatar|account/i }).first().click();
    await page.getByRole("menuitem", { name: /log out|sign out/i }).click();
    await expect(page).toHaveURL(/\/login/, { timeout: 8000 });
  });
});
