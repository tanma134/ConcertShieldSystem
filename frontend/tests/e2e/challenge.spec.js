import { test, expect } from "@playwright/test";

// Mọi test đều mock API nên không cần backend. Request bị chặn là POST /auth/login
// (interceptor xử lý 428 cho mọi request, không riêng login).
const LOGIN_URL = "**/api/auth/login";
const CORS = {
  "access-control-allow-origin": "*",
  "access-control-allow-headers": "*",
  "access-control-allow-methods": "*",
};

const json = (route, status, body) =>
  route.fulfill({ status, headers: CORS, contentType: "application/json", body: JSON.stringify(body) });

// Xử lý preflight rồi giao POST cho handler
async function mockPost(page, url, handler) {
  await page.route(url, async (route) => {
    const req = route.request();
    if (req.method() === "OPTIONS") return route.fulfill({ status: 204, headers: CORS });
    return handler(route, req);
  });
}

// Giả Turnstile: gọi callback ngay với token giả, hoặc error-callback / lỗi tải script
async function mockTurnstile(page, mode = "pass") {
  await page.route("**/challenges.cloudflare.com/turnstile/**", (route) => {
    if (mode === "load-fail") return route.abort();
    const body = `
      window.turnstile = {
        render(el, opts) {
          ${mode === "pass" ? 'setTimeout(() => opts.callback("fake-captcha-token"), 50);' : ""}
          ${mode === "error" ? 'setTimeout(() => opts["error-callback"](), 50);' : ""}
          return "w1";
        },
        remove() {},
      };`;
    return route.fulfill({ status: 200, contentType: "application/javascript", body });
  });
}

async function submitLogin(page) {
  await page.goto("/login");
  await page.locator('input[name="email"]').fill("test@example.com");
  await page.locator('input[name="password"]').fill("Passw0rd!");
  await page.getByRole("button", { name: "Log In" }).click();
}

const challenge = (type) => ({ success: false, code: "RISK_CHALLENGE", message: "Additional verification required", challengeType: type });
const dialog = (page) => page.getByRole("dialog");

test.describe("CAPTCHA", () => {
  test("428 CAPTCHA -> hiện modal Security check", async ({ page }) => {
    await mockTurnstile(page, "none"); // widget không tự pass, modal ở lại
    await mockPost(page, LOGIN_URL, (route) => json(route, 428, challenge("CAPTCHA")));
    await submitLogin(page);
    await expect(dialog(page)).toBeVisible();
    await expect(page.getByText("Security check")).toBeVisible();
    await expect(page.getByText("Please confirm you are not a robot")).toBeVisible();
  });

  test("challengeType thiếu/không rõ -> mặc định CAPTCHA", async ({ page }) => {
    await mockTurnstile(page, "none");
    await mockPost(page, LOGIN_URL, (route) =>
      json(route, 428, { success: false, code: "RISK_CHALLENGE" }));
    await submitLogin(page);
    await expect(page.getByText("Please confirm you are not a robot")).toBeVisible();
  });

  test("qua CAPTCHA -> request được gửi lại kèm X-Challenge-Token", async ({ page }) => {
    await mockTurnstile(page, "pass");
    const seen = [];
    await mockPost(page, LOGIN_URL, (route, req) => {
      const token = req.headers()["x-challenge-token"];
      seen.push(token || null);
      if (!token) return json(route, 428, challenge("CAPTCHA"));
      return json(route, 200, { accessToken: "a", refreshToken: "r", roles: ["Customer"] });
    });
    await submitLogin(page);
    await expect.poll(() => seen.length).toBe(2);
    expect(seen[0]).toBeNull();
    expect(seen[1]).toBe("fake-captcha-token");
    await expect(dialog(page)).toBeHidden();
  });

  test("bấm Cancel -> đóng modal và hiện lỗi đăng nhập", async ({ page }) => {
    await mockTurnstile(page, "none");
    let calls = 0;
    await mockPost(page, LOGIN_URL, (route) => { calls++; return json(route, 428, challenge("CAPTCHA")); });
    await submitLogin(page);
    await expect(dialog(page)).toBeVisible();
    await page.getByRole("button", { name: "Cancel" }).click();
    await expect(dialog(page)).toBeHidden();
    await expect(page.locator(".auth-error")).toContainText("Additional verification required");
    expect(calls).toBe(1); // không gửi lại
  });

  test("Turnstile báo lỗi -> hiện 'Verification failed'", async ({ page }) => {
    await mockTurnstile(page, "error");
    await mockPost(page, LOGIN_URL, (route) => json(route, 428, challenge("CAPTCHA")));
    await submitLogin(page);
    await expect(dialog(page).getByText("Verification failed. Please try again.")).toBeVisible();
  });

  test("không tải được script Turnstile -> hiện lỗi", async ({ page }) => {
    await mockTurnstile(page, "load-fail");
    await mockPost(page, LOGIN_URL, (route) => json(route, 428, challenge("CAPTCHA")));
    await submitLogin(page);
    await expect(dialog(page).getByText("Could not load Turnstile")).toBeVisible();
  });

  test("qua CAPTCHA nhưng server vẫn 428 -> không lặp vô hạn", async ({ page }) => {
    await mockTurnstile(page, "pass");
    let calls = 0;
    await mockPost(page, LOGIN_URL, (route) => { calls++; return json(route, 428, challenge("CAPTCHA")); });
    await submitLogin(page);
    await expect(page.locator(".auth-error")).toBeVisible();
    await page.waitForTimeout(500);
    expect(calls).toBe(2); // 1 lần đầu + 1 lần retry duy nhất
  });
});

test.describe("OTP step-up", () => {
  const isSend = (u) => !/verify/i.test(u);

  async function mockStepUp(page, { verifyStatus = 200 } = {}) {
    await mockPost(page, /step-?up/i, (route, req) => {
      if (isSend(req.url())) return json(route, 200, { success: true });
      if (verifyStatus !== 200) return json(route, verifyStatus, { success: false, message: "Invalid or expired code." });
      return json(route, 200, { stepUpToken: "fake-stepup", expiresAt: new Date(Date.now() + 600000).toISOString() });
    });
  }

  test("428 OTP -> hiện modal OTP", async ({ page }) => {
    await mockPost(page, LOGIN_URL, (route) => json(route, 428, challenge("OTP")));
    await submitLogin(page);
    await expect(page.getByText("Confirm it is you with a one-time code", { exact: false })).toBeVisible();
    await expect(page.getByRole("button", { name: "Send code to my email" })).toBeVisible();
  });

  test("gửi mã -> hiện ô nhập, Verify bị khoá tới khi đủ 6 số", async ({ page }) => {
    await mockStepUp(page);
    await mockPost(page, LOGIN_URL, (route) => json(route, 428, challenge("OTP")));
    await submitLogin(page);
    await page.getByRole("button", { name: "Send code to my email" }).click();
    await expect(page.getByText("A 6-digit code was sent to your email.")).toBeVisible();
    const input = page.getByPlaceholder("123456");
    await input.fill("12ab34"); // chữ bị loại bỏ
    await expect(input).toHaveValue("1234");
    await expect(page.getByRole("button", { name: "Verify" })).toBeDisabled();
    await input.fill("123456");
    await expect(page.getByRole("button", { name: "Verify" })).toBeEnabled();
  });

  test("OTP đúng -> retry kèm X-StepUp-Token", async ({ page }) => {
    await mockStepUp(page);
    const seen = [];
    await mockPost(page, LOGIN_URL, (route, req) => {
      const t = req.headers()["x-stepup-token"];
      seen.push(t || null);
      if (!t) return json(route, 428, challenge("OTP"));
      return json(route, 200, { accessToken: "a", refreshToken: "r", roles: ["Customer"] });
    });
    await submitLogin(page);
    await page.getByRole("button", { name: "Send code to my email" }).click();
    await page.getByPlaceholder("123456").fill("123456");
    await page.getByRole("button", { name: "Verify" }).click();
    await expect.poll(() => seen.length).toBe(2);
    expect(seen[1]).toBe("fake-stepup");
    await expect(dialog(page)).toBeHidden();
  });

  test("OTP sai -> hiện lỗi, modal vẫn mở", async ({ page }) => {
    await mockStepUp(page, { verifyStatus: 400 });
    await mockPost(page, LOGIN_URL, (route) => json(route, 428, challenge("OTP")));
    await submitLogin(page);
    await page.getByRole("button", { name: "Send code to my email" }).click();
    await page.getByPlaceholder("123456").fill("000000");
    await page.getByRole("button", { name: "Verify" }).click();
    await expect(page.getByText("Invalid or expired code.")).toBeVisible();
    await expect(dialog(page)).toBeVisible();
  });

  test("gửi mã thất bại -> hiện lỗi từ server", async ({ page }) => {
    await mockPost(page, /step-?up/i, (route) =>
      json(route, 429, { success: false, message: "Too many requests." }));
    await mockPost(page, LOGIN_URL, (route) => json(route, 428, challenge("OTP")));
    await submitLogin(page);
    await page.getByRole("button", { name: "Send code to my email" }).click();
    await expect(page.getByText("Too many requests.")).toBeVisible();
  });

  test("Cancel ở modal OTP -> đóng modal", async ({ page }) => {
    await mockPost(page, LOGIN_URL, (route) => json(route, 428, challenge("OTP")));
    await submitLogin(page);
    await page.getByRole("button", { name: "Cancel" }).click();
    await expect(dialog(page)).toBeHidden();
  });
});

test.describe("Các lỗi risk khác (không có modal)", () => {
  for (const [status, code, message] of [
    [403, "RISK_BLOCKED", "Your account or device is temporarily restricted due to unusual activity."],
    [403, "RISK_HOLD", "This action is being reviewed by our team. Please try again later."],
    [429, "RATE_LIMITED", "Too many requests. Please slow down."],
  ]) {
    test(`${status} ${code} -> hiện message, không mở modal`, async ({ page }) => {
      await mockPost(page, LOGIN_URL, (route) => json(route, status, { success: false, code, message }));
      await submitLogin(page);
      await expect(page.locator(".auth-error")).toContainText(message);
      await expect(dialog(page)).toHaveCount(0);
    });
  }
});

// Test thật: chỉ chạy khi backend ở ENFORCE và ngưỡng bot medium đã hạ thấp.
// Chạy: REAL_RISK=1 npx playwright test -g "thật"
test("thật: Playwright headless dính BOT_E01 -> ra CAPTCHA", async ({ page }) => {
  test.skip(!process.env.REAL_RISK, "Đặt REAL_RISK=1 khi backend đã sẵn sàng");
  await submitLogin(page);
  await expect(page.getByText("Please confirm you are not a robot")).toBeVisible({ timeout: 15_000 });
});
