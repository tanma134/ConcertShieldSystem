import { request, APIRequestContext, expect } from '@playwright/test';

export const REAL_UA =
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36';

export function realHeaders(tag: string, origin: string) {
  return {
    'User-Agent': REAL_UA,
    'sec-ch-ua': '"Chromium";v="126", "Google Chrome";v="126"',
    'sec-fetch-site': 'same-origin',
    'Accept-Language': 'vi-VN,vi;q=0.9,en;q=0.8',
    'Accept-Encoding': 'gzip, deflate, br',
    Origin: origin,
    'X-Had-Input': '1',
    'X-Device-Fp': `dev-${tag}`,
    'X-Session-Id': `sess-${tag}`,
    'X-Forwarded-For': `10.20.30.${(tag.length % 200) + 10}`,
  } as Record<string, string>;
}

export function botHeaders(kind: string, tag: string) {
  const h = realHeaders(tag, 'https://evil.example');
  if (kind === 'cli') h['User-Agent'] = 'curl/8.0';
  if (kind === 'webdriver') h['X-Webdriver'] = '1';
  if (kind === 'no-sec') delete h['sec-ch-ua'];
  if (kind === 'no-lang') delete h['Accept-Language'];
  return h;
}

export async function login(
  api: APIRequestContext,
  base: string,
  email: string,
  password: string,
  headers: Record<string, string>,
) {
  const r = await api.post(`${base}/api/auth/login`, {
    headers,
    data: { email, password },
  });
  expect(r.status()).toBe(200);
  const body = await r.json();
  const token: string = body.token ?? body.accessToken ?? body.data?.token;
  expect(token, 'login returns JWT').toBeTruthy();
  return token as string;
}

export async function withEnforce(
  api: APIRequestContext,
  adminBase: string,
  adminToken: string,
  fn: () => Promise<void>,
) {
  const pol = await api.get(`${adminBase}/api/admin/risk/policy`, {
    headers: { Authorization: `Bearer ${adminToken}` },
  });
  expect(pol.status()).toBe(200);
  const before = await pol.json();
  const modeBefore =
    before.mode ?? before.data?.mode ?? before.items?.find?.((i: any) => i.key === 'risk.mode')?.value ?? 'SHADOW';
  const version = before.ruleVersion ?? before.data?.ruleVersion;
  const put = await api.put(`${adminBase}/api/admin/risk/policy`, {
    headers: { Authorization: `Bearer ${adminToken}` },
    data: {
      items: [{ key: 'risk.mode', value: 'ENFORCE' }],
      reason: 'Playwright fraud-bot test needs ENFORCE mode for this case run',
      ...(version ? { version } : {}),
    },
  });
  // Chấp nhận 200 hoặc 409 version (thử lại 1 lần khi 409)
  if (put.status() === 409) {
    const fresh = await (await api.get(`${adminBase}/api/admin/risk/policy`, { headers: { Authorization: `Bearer ${adminToken}` } })).json();
    const v2 = fresh.ruleVersion ?? fresh.data?.ruleVersion;
    await api.put(`${adminBase}/api/admin/risk/policy`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { items: [{ key: 'risk.mode', value: 'ENFORCE' }], reason: 'Playwright fraud-bot test needs ENFORCE mode retry', ...(v2 ? { version: v2 } : {}) },
    });
  }
  // Chờ cache config TTL 60s: polling đọc lại tới khi ENFORCE
  for (let i = 0; i < 12; i++) {
    const cur = await (await api.get(`${adminBase}/api/admin/risk/policy`, { headers: { Authorization: `Bearer ${adminToken}` } })).json();
    const m = cur.mode ?? cur.data?.mode ?? 'ENFORCE';
    if (String(m).toUpperCase() === 'ENFORCE') break;
    await new Promise((r) => setTimeout(r, 5000));
  }
  try {
    await fn();
  } finally {
    const fresh = await (await api.get(`${adminBase}/api/admin/risk/policy`, { headers: { Authorization: `Bearer ${adminToken}` } })).json();
    const v = fresh.ruleVersion ?? fresh.data?.ruleVersion;
    await api.put(`${adminBase}/api/admin/risk/policy`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: {
        items: [{ key: 'risk.mode', value: String(modeBefore).toUpperCase() }],
        reason: 'Playwright teardown restores original risk mode value back',
        ...(v ? { version: v } : {}),
      },
    });
  }
}

/** Polling có điều kiện thay cho sleep cứng. */
export async function pollFor<T>(fn: () => Promise<T>, ok: (v: T) => boolean, timeoutMs = 30000, stepMs = 2000) {
  const t0 = Date.now();
  for (;;) {
    const v = await fn();
    if (ok(v)) return v;
    if (Date.now() - t0 > timeoutMs) throw new Error('pollFor timeout');
    await new Promise((r) => setTimeout(r, stepMs));
  }
}
