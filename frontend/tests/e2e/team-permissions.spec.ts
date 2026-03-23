import { test, expect } from "@playwright/test";

const DEBUG_ROUTES = process.env.PW_DEBUG_ROUTES === "1";

test.describe("Team permissions screen", () => {
  test.describe.configure({ mode: "serial" });

  test.beforeEach(async ({ page }) => {
    if (DEBUG_ROUTES) {
      page.on("request", (request) => {
        const url = request.url();
        if (url.includes("/api/")) {
          console.log(`[pw-debug][request] ${request.method()} ${url}`);
        }
      });

      page.on("response", async (response) => {
        const url = response.url();
        const status = response.status();
        if (url.includes("/api/") || status === 401 || url.includes("/login")) {
          console.log(`[pw-debug][response] ${status} ${url}`);
        }
        if (status === 401) {
          let bodyPreview = "";
          try {
            bodyPreview = (await response.text()).slice(0, 300);
          } catch {
            bodyPreview = "<no response body>";
          }
          console.log(`[pw-debug][response-401-body] ${bodyPreview}`);
        }
      });

      page.on("framenavigated", (frame) => {
        if (frame === page.mainFrame()) {
          console.log(`[pw-debug][nav] ${frame.url()}`);
        }
      });
    }

    await page.addInitScript(() => {
      localStorage.setItem(
        "deployflow-auth",
        JSON.stringify({
          state: {
            user: {
              id: "user-admin",
              email: "admin@example.com",
              name: "Admin User",
              role: "admin",
              avatarUrl: "",
              tenantId: "tenant-1",
              tenantName: "Tenant One",
              tenantPlan: "pro",
              twoFactorEnabled: false,
              createdAt: new Date().toISOString(),
              isActive: true,
              lastLoginAt: new Date().toISOString(),
            },
            accessToken: "test-access-token",
            refreshToken: "test-refresh-token",
            isAuthenticated: true,
          },
          version: 0,
        })
      );
    });

    await page.route("**/api/team", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([
          {
            id: "user-admin",
            name: "Admin User",
            email: "admin@example.com",
            role: "admin",
            avatarUrl: "",
            status: "active",
            joinedAt: new Date().toISOString(),
            createdAt: new Date().toISOString(),
          },
          {
            id: "user-dev",
            name: "Dev User",
            email: "dev@example.com",
            role: "developer",
            avatarUrl: "",
            status: "active",
            joinedAt: new Date().toISOString(),
            createdAt: new Date().toISOString(),
          },
        ]),
      });
    });

    await page.route("**/api/team/invitations", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify([
          {
            id: "invite-1",
            email: "new-user@example.com",
            name: "New User",
            role: "viewer",
            status: "pending",
            expiresAt: new Date(Date.now() + 86400000).toISOString(),
            lastSentAt: new Date().toISOString(),
            resendCount: 0,
            invitedByName: "Admin User",
            createdAt: new Date().toISOString(),
          },
        ]),
      });
    });

    await page.route("**/api/stats/dashboard", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          totalProjects: 1,
          totalDeployments: 0,
          activeServices: 0,
          totalServers: 0,
          onlineServers: 0,
          failedDeploymentsToday: 0,
          successfulDeploymentsToday: 0,
          avgDeploymentDuration: 0,
          totalDatabases: 0,
          activeAlerts: 0,
          monthlyCost: 0,
        }),
      });
    });

    await page.route("**/api/projects*", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          data: [{ id: "project-1", name: "Core Project" }],
          total: 1,
          page: 1,
          pageSize: 10,
          totalPages: 1,
        }),
      });
    });

    await page.route("**/api/services*", async (route) => {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([]) });
    });
    await page.route("**/api/databases*", async (route) => {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([]) });
    });
    await page.route("**/api/domains*", async (route) => {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([]) });
    });
    await page.route("**/api/volumes*", async (route) => {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([]) });
    });

    await page.route("**/api/permissions/users/**/grants", async (route) => {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([]) });
    });

    await page.route("**/api/permissions/users/**/preview*", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          userId: "user-admin",
          resourceType: "project",
          resourceId: "project-1",
          effectiveActions: ["read"],
        }),
      });
    });

    await page.route("**/api/**", async (route) => {
      const request = route.request();
      const url = new URL(request.url());
      const pathname = url.pathname;

      const explicitlyMockedPaths = [
        "/api/team",
        "/api/team/invitations",
        "/api/stats/dashboard",
        "/api/projects",
        "/api/services",
        "/api/databases",
        "/api/domains",
        "/api/volumes",
      ];

      if (
        explicitlyMockedPaths.some((path) => pathname === path || pathname.startsWith(`${path}/`)) ||
        pathname.includes("/api/permissions/users/") ||
        pathname === "/api/permissions/grants/bulk"
      ) {
        await route.fallback();
        return;
      }

      if (pathname === "/api/auth/refresh") {
        await route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            accessToken: "test-access-token",
            refreshToken: "test-refresh-token",
          }),
        });
        return;
      }

      await route.fulfill({
        status: request.method() === "GET" ? 200 : 204,
        contentType: "application/json",
        body: request.method() === "GET" ? JSON.stringify({}) : "",
      });
    });
  });

  test("renders invitations and permission editor", async ({ page }) => {
    await page.goto("/team");

    await expect(page.getByRole("main").getByRole("heading", { name: /^team$/i })).toBeVisible();
    await expect(page.getByText(/pending invitations/i)).toBeVisible();
    await expect(page.getByText("new-user@example.com")).toBeVisible();
    await expect(page.getByText(/resource permissions/i)).toBeVisible();
    await expect(page.getByText(/effective:/i)).toBeVisible();
  });

  test("submits bulk permission grant payload", async ({ page }) => {
    let grantPayload: any = null;

    await page.route("**/api/permissions/grants/bulk", async (route) => {
      grantPayload = route.request().postDataJSON();
      await route.fulfill({ status: 204, body: "" });
    });

    await page.goto("/team");

    if (DEBUG_ROUTES && page.url().includes("/login")) {
      const authStorage = await page.evaluate(() => localStorage.getItem("deployflow-auth"));
      console.log(`[pw-debug][redirected-to-login] storage=${authStorage ?? "<null>"}`);
    }

    await expect(page.getByRole("button", { name: /apply grant/i })).toBeVisible();
    await page.getByRole("button", { name: /apply grant/i }).click();

    await expect.poll(() => grantPayload).not.toBeNull();
    expect(grantPayload.userId).toBeTruthy();
    expect(grantPayload.grants).toHaveLength(1);
    expect(grantPayload.grants[0].resourceType).toBe("project");
    expect(grantPayload.grants[0].resourceId).toBe("project-1");
  });
});
