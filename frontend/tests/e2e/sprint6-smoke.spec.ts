import { test, expect } from "@playwright/test";

test("sprint6 dashboard smoke", async ({ page }) => {
  const email = process.env.SMOKE_EMAIL;
  const password = process.env.SMOKE_PASSWORD;
  const apiUrl = process.env.SMOKE_API_URL || "http://localhost:5000/api";

  if (!email || !password) {
    test.skip(true, "SMOKE_EMAIL/SMOKE_PASSWORD not provided");
  }

  const loginRes = await page.request.post(`${apiUrl}/auth/login`, {
    data: { email, password },
  });
  expect(loginRes.status()).toBe(200);
  const payload = await loginRes.json();

  await page.addInitScript((authPayload) => {
    localStorage.setItem(
      "deployflow-auth",
      JSON.stringify({
        state: {
          user: authPayload.user,
          accessToken: authPayload.accessToken,
          refreshToken: authPayload.refreshToken,
          isAuthenticated: true,
        },
        version: 0,
      })
    );
  }, payload);

  await page.goto("/domains");
  await expect(page.getByRole("main").getByRole("heading", { name: /^domains$/i })).toBeVisible();
  await expect(page.getByText(/network error/i)).toHaveCount(0);

  await page.goto("/alerts");
  await expect(page.getByRole("main").getByRole("heading", { name: /^alerts$/i })).toBeVisible();
  await expect(page.getByText(/alert rules/i)).toBeVisible();

  await page.goto("/settings");
  await expect(page.getByRole("main").getByRole("heading", { name: /^settings$/i })).toBeVisible();
  await expect(page.getByText(/integrations/i)).toBeVisible();
});
