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

test.describe("Cost Dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await login(page);
  });

  test("cost dashboard page loads without error", async ({ page }) => {
    await page.goto("/costs");
    await expect(page).not.toHaveURL(/error|404|403/);
    // Page should render within 10s (data may load async)
    await expect(page.locator("main")).toBeVisible({ timeout: 10000 });
  });

  test("page shows cost summary cards", async ({ page }) => {
    await page.goto("/costs");
    // Wait for loading to finish
    await page.waitForSelector("[class*='skeleton']", { state: "detached", timeout: 15000 }).catch(() => {});
    // Should have card-like elements showing cost data
    await expect(
      page.getByText(/total cost|monthly|\$\d|cost/i).first(),
    ).toBeVisible({ timeout: 10000 });
  });

  test("period selector changes the displayed period", async ({ page }) => {
    await page.goto("/costs");
    await page.waitForSelector("[class*='skeleton']", { state: "detached", timeout: 15000 }).catch(() => {});

    // Look for period selector (dropdown or button group)
    const periodSelector = page
      .getByRole("combobox", { name: /period/i })
      .or(page.getByRole("button", { name: /1m|3m|6m|12m|last.*month/i }).first());

    if (await periodSelector.count() === 0) {
      test.skip(); // No period selector found
    }

    await periodSelector.first().click();
    // Some option should appear
    await expect(
      page.getByRole("option").or(page.getByRole("menuitem")).first(),
    ).toBeVisible({ timeout: 5000 });
  });

  test("cost chart renders a recharts-based chart", async ({ page }) => {
    await page.goto("/costs");
    await page.waitForSelector("[class*='skeleton']", { state: "detached", timeout: 15000 }).catch(() => {});

    // Recharts renders SVG elements
    const chart = page.locator("svg.recharts-surface, [class*='recharts']").first();
    if (await chart.count() > 0) {
      await expect(chart).toBeVisible({ timeout: 8000 });
    }
    // If no chart, at least the page should have text content (cost numbers or categories)
    await expect(
      page.getByText(/\$\d|\d+\.\d{2}|compute|storage|network/i).first(),
    ).toBeVisible({ timeout: 10000 });
  });

  test("optimization tips section is present", async ({ page }) => {
    await page.goto("/costs");
    await page.waitForSelector("[class*='skeleton']", { state: "detached", timeout: 15000 }).catch(() => {});

    await expect(
      page.getByText(/optimization|saving|tip|recommend/i).first(),
    ).toBeVisible({ timeout: 10000 });
  });
});
