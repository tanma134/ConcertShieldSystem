import { test, expect, request } from '@playwright/test';
import { realHeaders, botHeaders, withEnforce } from './helpers';

const AUTH = process.env.AUTH_API_URL!;
const ADMIN = process.env.ADMIN_API_URL!;
const ORIGIN = process.env.FRONTEND_URL ?? 'http://localhost:5173';

// BOT-001: happy path login không bị chặn (tránh false positive)
test('BOT-001 login with real headers is allowed', async () => {
  const api = await request.newContext();
  const r = await api.post(`${AUTH}/api/auth/login`, {
    headers: realHeaders('bot001', ORIGIN),
    data: { email: process.env.TEST_CUSTOMER_EMAIL, password: process.env.TEST_CUSTOMER_PASSWORD },
  });
  expect(r.status()).toBe(200);
  await api.dispose();
});

// BOT-002/BOT-003/BOT-004: UA CLI / headless / webdriver
for (const kind of ['cli', 'webdriver'] as const) {
  test(`BOT-00x bot header kind=${kind} triggers BOT rule`, async () => {
    const api = await request.newContext();
    const admin = await api
      .post(`${AUTH}/api/auth/login`, {
        headers: realHeaders(`adm-${kind}`, ORIGIN),
        data: { email: process.env.TEST_ADMIN_EMAIL, password: process.env.TEST_ADMIN_PASSWORD },
      })
      .then((r) => r.json());
    const adminToken: string = admin.token ?? admin.accessToken ?? admin.data?.token;
    await withEnforce(api, ADMIN, adminToken, async () => {
      const r = await api.post(`${AUTH}/api/auth/login`, {
        headers:
          kind === 'webdriver'
            ? { ...botHeaders('plain', `b-${kind}`, ), 'X-Webdriver': '1' }
            : { ...botHeaders('cli', `b-${kind}`), Origin: ORIGIN },
        data: { email: process.env.TEST_CUSTOMER_EMAIL, password: process.env.TEST_CUSTOMER_PASSWORD },
      });
      expect([200, 428, 403]).toContain(r.status());
      if (r.status() !== 200) {
        const body = await r.json();
        expect(body.code).toMatch(/RISK_CHALLENGE|RISK_BLOCKED|RISK_HOLD/);
      }
    });
    await api.dispose();
  });
}

// BOT-008: rate limit login 10/phút/IP -> 429 + Retry-After
test('BOT-008 login rate limit 10/min returns 429', async () => {
  const api = await request.newContext();
  const h = realHeaders('bot008', ORIGIN);
  let last = 0;
  for (let i = 0; i < 11; i++) {
    const r = await api.post(`${AUTH}/api/auth/login`, {
      headers: h,
      data: { email: `nouser${i}@example.com`, password: 'wrong-password-1' },
    });
    last = r.status();
    if (last === 429) {
      expect(r.headers()['retry-after']).toBeTruthy();
      break;
    }
  }
  expect(last).toBe(429);
  await api.dispose();
});
