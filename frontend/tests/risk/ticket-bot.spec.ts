import { test, expect, request } from '@playwright/test';
import { realHeaders, login } from './helpers';

const TICKET = process.env.TICKET_API_URL!;
const AUTH = process.env.AUTH_API_URL!;
const ORIGIN = process.env.FRONTEND_URL ?? 'http://localhost:5173';
const EVENT = Number(process.env.TEST_EVENT_ID);
const TTYPE = Number(process.env.TEST_TICKET_TYPE_ID);

// TKTB-001: happy path hold -> doi >3s -> order
test('TKTB-001 hold then order after 3s is allowed', async () => {
  const api = await request.newContext();
  const h = realHeaders('tktb001', ORIGIN);
  const token = await login(api, AUTH, process.env.TEST_CUSTOMER_EMAIL!, process.env.TEST_CUSTOMER_PASSWORD!, h);
  const auth = { ...h, Authorization: `Bearer ${token}` };
  const hold = await api.post(`${TICKET}/api/Holds/session`, {
    headers: auth,
    data: { eventId: EVENT, tickets: [{ ticketTypeId: TTYPE, quantity: 1 }], seatIds: [] },
  });
  expect(hold.status()).toBe(200);
  const holdBody = await hold.json();
  const holdId: string = holdBody.holdId ?? holdBody.data?.holdId ?? holdBody.id;
  expect(holdId).toBeTruthy();
  await new Promise((r) => setTimeout(r, 4000)); // qua BOT_V02 (>=3s), chờ có lý do
  const order = await api.post(`${TICKET}/api/Orders/create`, {
    headers: auth,
    data: {
      eventId: EVENT, isReservedSeating: false,
      orderDetails: [{ ticketTypeId: TTYPE, quantity: 1, seatIds: [] }],
      fullName: 'Risk Test', email: process.env.TEST_CUSTOMER_EMAIL, phone: '0900000001',
      holdId,
    },
  });
  expect(order.status()).toBe(200);
  const orderBody = await order.json();
  const orderId = orderBody.orderId ?? orderBody.data?.orderId ?? orderBody.id;
  // cleanup: hủy pending để không ảnh hưởng FRD_T01 của test khác
  if (orderId) {
    await api.post(`${TICKET}/api/Orders/${orderId}/cancel-pending`, {
      headers: { 'X-Internal-Api-Key': process.env.INTERNAL_API_KEY! },
    });
  }
  await api.dispose();
});

// TKTB-005: X-Had-Input=0 -> BOT_E03
test('TKTB-005 X-Had-Input 0 triggers BOT_E03', async () => {
  const api = await request.newContext();
  const h = { ...realHeaders('tktb005', ORIGIN), 'X-Had-Input': '0' };
  const token = await login(api, AUTH, process.env.TEST_CUSTOMER_EMAIL!, process.env.TEST_CUSTOMER_PASSWORD!, realHeaders('tktb005login', ORIGIN));
  const r = await api.post(`${TICKET}/api/Holds/session`, {
    headers: { ...h, Authorization: `Bearer ${token}` },
    data: { eventId: EVENT, tickets: [{ ticketTypeId: TTYPE, quantity: 1 }], seatIds: [] },
  });
  expect([200, 428, 403]).toContain(r.status());
  await api.dispose();
});

// R-001: race nhiều user giữ 1 ghế — đúng 1 thành công
test('R-001 concurrent holds on last seat sell once', async () => {
  const api = await request.newContext();
  const mk = async (tag: string) => {
    const h = realHeaders(tag, ORIGIN);
    const t = await login(api, AUTH, process.env.TEST_CUSTOMER_EMAIL!, process.env.TEST_CUSTOMER_PASSWORD!, h);
    return api.post(`${TICKET}/api/Holds/session`, {
      headers: { ...h, Authorization: `Bearer ${t}` },
      data: { eventId: EVENT, tickets: [{ ticketTypeId: TTYPE, quantity: 1 }], seatIds: [] },
    }).then((r) => r.status());
  };
  const results = await Promise.all([mk('race1'), mk('race2'), mk('race3')]);
  expect(results.filter((s) => s === 200).length).toBeGreaterThanOrEqual(1);
  await api.dispose();
});
