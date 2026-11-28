import { test, expect, request } from '@playwright/test';
import { realHeaders, login, pollFor } from './helpers';

const TICKET = process.env.TICKET_API_URL!;
const AUTH = process.env.AUTH_API_URL!;
const ADMIN = process.env.ADMIN_API_URL!;
const ORIGIN = process.env.FRONTEND_URL ?? 'http://localhost:5173';
const EVENT = Number(process.env.TEST_EVENT_ID);
const TTYPE = Number(process.env.TEST_TICKET_TYPE_ID);

async function hold(api: any, auth: any, qty: number) {
  const r = await api.post(`${TICKET}/api/Holds/session`, {
    headers: auth,
    data: { eventId: EVENT, tickets: [{ ticketTypeId: TTYPE, quantity: qty }], seatIds: [] },
  });
  expect(r.status()).toBe(200);
  const b = await r.json();
  return (b.holdId ?? b.data?.holdId ?? b.id) as string;
}

// F-002: boundary FRD_T01 — tổng 6 OK, 7 dính
test('F-002 FRD_T01 boundary 6 ok 7 blocked', async () => {
  const api = await request.newContext();
  const h = realHeaders('f002', ORIGIN);
  const token = await login(api, AUTH, process.env.TEST_CUSTOMER_EMAIL!, process.env.TEST_CUSTOMER_PASSWORD!, h);
  const auth = { ...h, Authorization: `Bearer ${token}` };
  const hid = await hold(api, auth, 6);
  await new Promise((r) => setTimeout(r, 3500));
  const order = await api.post(`${TICKET}/api/Orders/create`, {
    headers: auth,
    data: {
      eventId: EVENT, isReservedSeating: false,
      orderDetails: [{ ticketTypeId: TTYPE, quantity: 6, seatIds: [] }],
      fullName: 'Risk Test', email: process.env.TEST_CUSTOMER_EMAIL, phone: '0900000002', holdId: hid,
    },
  });
  expect(order.status()).toBe(200);
  const orderBody = await order.json();
  const orderId = orderBody.orderId ?? orderBody.data?.orderId ?? orderBody.id;
  // +1 vé nữa phải dính FRD_T01
  const hid2 = await hold(api, auth, 1);
  await new Promise((r) => setTimeout(r, 3500));
  const over = await api.post(`${TICKET}/api/Orders/create`, {
    headers: auth,
    data: {
      eventId: EVENT, isReservedSeating: false,
      orderDetails: [{ ticketTypeId: TTYPE, quantity: 1, seatIds: [] }],
      fullName: 'Risk Test', email: process.env.TEST_CUSTOMER_EMAIL, phone: '0900000002', holdId: hid2,
    },
  });
  expect([403, 428]).toContain(over.status());
  // cleanup: hủy pending đã tạo
  if (orderId) {
    await api.post(`${TICKET}/api/Orders/${orderId}/cancel-pending`, {
      headers: { 'X-Internal-Api-Key': process.env.INTERNAL_API_KEY! },
    });
  }
  await api.dispose();
});

// F-009: eKYC gate
test('F-009 without eKYC booking returns EKYC_REQUIRED', async () => {
  test.skip(!process.env.TEST_CUSTOMER_NEW_EMAIL, 'needs TEST_CUSTOMER_NEW_* (chua KYC)');
  const api = await request.newContext();
  const h = realHeaders('f009', ORIGIN);
  const token = await login(api, AUTH, process.env.TEST_CUSTOMER_NEW_EMAIL!, process.env.TEST_CUSTOMER_NEW_PASSWORD!, h);
  const r = await api.post(`${TICKET}/api/Holds/session`, {
    headers: { ...h, Authorization: `Bearer ${token}` },
    data: { eventId: EVENT, tickets: [{ ticketTypeId: TTYPE, quantity: 1 }], seatIds: [] },
  });
  expect(r.status()).toBe(403);
  expect((await r.json()).code).toBe('EKYC_REQUIRED');
  await api.dispose();
});

// F-003/F-004: FRD_T02 — 2 returns OK, return thứ 3 dính
test('F-003 FRD_T02 third return is flagged', async () => {
  test.skip(true, 'can te: can 3 ve Paid + ticketId that de tao return; chay khi da seed xong');
});

// ADM-005/ADM-006: appeal Accept mở block, Reject vẫn chặn
test('ADM-005 appeal accept restores booking', async () => {
  const api = await request.newContext();
  const h = realHeaders('adm005', ORIGIN);
  const adminToken = await login(api, AUTH, process.env.TEST_ADMIN_EMAIL!, process.env.TEST_ADMIN_PASSWORD!, realHeaders('adm005a', ORIGIN));
  // 1. tạo block thủ công cho customer test
  const customerMe = await (
    await api.post(`${AUTH}/api/auth/login`, { headers: h, data: { email: process.env.TEST_CUSTOMER_EMAIL, password: process.env.TEST_CUSTOMER_PASSWORD } })
  ).json();
  const customerId = customerMe.userId ?? customerMe.data?.userId ?? customerMe.id;
  const block = await api.post(`${ADMIN}/api/admin/risk/blocks`, {
    headers: { Authorization: `Bearer ${adminToken}` },
    data: { scope: 'ACCOUNT', userId: customerId, durationHours: 1, reason: 'Playwright appeal e2e test block case for appeal flow' },
  });
  expect(block.status()).toBe(201);
  // 2. customer lấy decisions/me, gửi appeal
  const cToken = await login(api, AUTH, process.env.TEST_CUSTOMER_EMAIL!, process.env.TEST_CUSTOMER_PASSWORD!, h);
  const me = await pollFor(
    async () => api.get(`${ADMIN}/api/risk/decisions/me`, { headers: { Authorization: `Bearer ${cToken}` } }).then((r) => r.json()),
    (b) => Array.isArray(b.items ?? b.data ?? b) && (b.items ?? b.data ?? b).length > 0,
  );
  const decision = (me.items ?? me.data ?? me)[0];
  expect(decision.canAppeal ?? true).toBeTruthy();
  const appeal = await api.post(`${ADMIN}/api/risk/appeals`, {
    headers: { Authorization: `Bearer ${cToken}` },
    data: { riskDecisionId: decision.id, reason: 'Tai khoan test bi chan nham, xin mo lai de tiep tuc dat ve' },
  });
  expect(appeal.status()).toBe(201);
  const appealBody = await appeal.json();
  const appealId = appealBody.id ?? appealBody.data?.id;
  // 3. admin Accept
  const resolve = await api.post(`${ADMIN}/api/admin/risk/appeals/${appealId}/resolve`, {
    headers: { Authorization: `Bearer ${adminToken}` },
    data: { decision: 'Accept', reviewNote: 'Xac minh day la tai khoan test, chap nhan khang cao mo block' },
  });
  expect(resolve.status()).toBe(200);
  // 4. customer đặt lại được
  const retry = await api.post(`${TICKET}/api/Holds/session`, {
    headers: { ...h, Authorization: `Bearer ${cToken}` },
    data: { eventId: EVENT, tickets: [{ ticketTypeId: TTYPE, quantity: 1 }], seatIds: [] },
  });
  expect(retry.status()).toBe(200);
  await api.dispose();
});

// SEC-004: internal không key -> 401
test('SEC-004 internal check without key returns 401', async () => {
  const api = await request.newContext();
  const r = await api.get(`${ADMIN}/api/internal/risk/check?ip=10.0.0.1`);
  expect(r.status()).toBe(401);
  await api.dispose();
});
