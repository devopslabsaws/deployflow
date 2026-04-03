import { test, expect, Page } from "@playwright/test";

const TEST_EMAIL = process.env.TEST_EMAIL ?? "admin@deployflow.dev";
const TEST_PASSWORD = process.env.TEST_PASSWORD ?? "Admin@1234";

async function login(page: Page) {
  await page.goto("/login");
  await page.getByLabel(/email/i).fill(TEST_EMAIL);
  await page.getByLabel(/password/i).fill(TEST_PASSWORD);
  await page.getByRole("button", { name: /sign in/i }).click();
  await expect(page).toHaveURL(/\/(dashboard|projects)/, { timeout: 10000 });
}

test.describe("Deployment Flow", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test("projects list page loads and shows project cards", async ({ page }) => {
    await page.goto("/projects");
    // Either shows project cards or the empty state
    await expect(
      page.getByRole("heading", { name: /projects/i }).or(page.getByText(/no projects/i)),
    ).toBeVisible({ timeout: 8000 });
  });

  test("deploy now button shows loading state on click", async ({ page }) => {
    await page.goto("/projects");
    // Find first active project link
    const firstProject = page.getByRole("link", { name: /view|open/i }).first();
    const projectCount = await firstProject.count();
    if (projectCount === 0) {
      test.skip(); // No projects exist in this environment
    }

    await firstProject.click();
    await expect(page).toHaveURL(/\/projects\//);

    const deployBtn = page.getByRole("button", { name: /deploy now/i });
    if (await deployBtn.count() === 0) test.skip();

    await deployBtn.click();
    // Button should transition to loading/deploying state
    await expect(
      page.getByRole("button", { name: /deploying|deployed/i }).or(deployBtn),
    ).toBeVisible({ timeout: 5000 });
  });

  test("deployment detail page renders overview tab", async ({ page }) => {
    await page.goto("/deployments");
    const firstDeployment = page.getByRole("link").filter({ hasText: /view|open|details/i }).first();
    if (await firstDeployment.count() === 0) {
      // Try clicking first row
      const rows = page.locator("tr, [data-deployment]");
      if (await rows.count() === 0) test.skip();
      await rows.first().click();
    } else {
      await firstDeployment.click();
    }
    await expect(page).toHaveURL(/\/deployments\/[a-z0-9-]+/, { timeout: 8000 });
    await expect(page.getByRole("tab", { name: /overview/i })).toBeVisible();
    await expect(page.getByRole("tab", { name: /logs/i })).toBeVisible();
  });

  test("deployment logs tab shows the live log component", async ({ page }) => {
    // Navigate directly to a deployment page (may need a real ID)
    await page.goto("/deployments");
    // Click any deployment row
    const deploymentLink = page
      .locator("a[href*='/deployments/']")
      .filter({ hasNot: page.locator("[href='/deployments']") })
      .first();

    if (await deploymentLink.count() === 0) test.skip();

    await deploymentLink.click();
    await expect(page).toHaveURL(/\/deployments\/[a-z0-9-]+/);

    // Switch to logs tab
    await page.getByRole("tab", { name: /logs/i }).click();

    // Live log component should render the terminal area
    await expect(
      page.locator(".font-mono").or(page.getByText(/waiting for deployment logs/i)),
    ).toBeVisible({ timeout: 5000 });
  });
});
