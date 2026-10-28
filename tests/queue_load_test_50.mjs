import crypto from "node:crypto";

const baseUrl = process.env.QUEUE_API_URL || "https://localhost:7310";
const eventId = Number(process.env.QUEUE_EVENT_ID || 3);
const userOffset = Number(process.env.QUEUE_USER_OFFSET || 20000);
const secret = "SuperSecretKey_MustBe32CharsOrMore!@#";
const issuer = "AuthenticationAPI";
const audience = "AuthenticationAPIUsers";
const roleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
const nameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";

process.env.NODE_TLS_REJECT_UNAUTHORIZED = "0";

const base64Url = (value) => Buffer.from(JSON.stringify(value)).toString("base64url");
const makeToken = (userId, role = "Customer") => {
  const header = base64Url({ alg: "HS256", typ: "JWT" });
  const now = Math.floor(Date.now() / 1000);
  const payload = base64Url({ sub: String(userId), [nameClaim]: String(userId), [roleClaim]: role, iss: issuer, aud: audience, iat: now, exp: now + 3600 });
  const unsigned = `${header}.${payload}`;
  const signature = crypto.createHmac("sha256", secret).update(unsigned).digest("base64url");
  return `${unsigned}.${signature}`;
};

async function request(method, path, token, body = undefined, extraHeaders = {}) {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}`, Accept: "application/json", ...(body === undefined ? {} : { "Content-Type": "application/json" }), ...extraHeaders },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  let data = {};
  try { data = text ? JSON.parse(text) : {}; } catch { data = { body: text }; }
  return { status: response.status, data };
}

const vietnamNow = new Date(Date.now() + 7 * 60 * 60 * 1000);
const localIso = (date) => date.toISOString().slice(0, 19);
const organizerToken = makeToken(9001, "Organizer");
const configure = await request("POST", `/api/queue/${eventId}/prequeue/configure`, organizerToken, {
  preQueueStartTime: localIso(new Date(vietnamNow.getTime() - 60_000)),
  preQueueEndTime: localIso(new Date(vietnamNow.getTime() + 10 * 60_000)),
  admissionRate: 10,
  queueEnabled: true,
  fairnessPolicy: "RANDOMIZED",
});
if (configure.status < 200 || configure.status >= 300) {
  console.error("CONFIGURE FAIL", configure.status, configure.data);
  process.exit(1);
}
console.log(`CONFIGURE PASS: event ${eventId}`);

const registerUser = async (userId) => {
  const token = makeToken(userId);
  const registered = await request("POST", `/api/queue/${eventId}/prequeue/register`, token);
  return { userId, token, register: registered.status, queueToken: registered.data.queueToken, error: registered.status >= 300 ? registered.data : null };
};

const registeredUsers = await Promise.all(Array.from({ length: 50 }, (_, index) => registerUser(userOffset + index + 1)));
const joinedUsers = await Promise.all(registeredUsers.map(async (user) => {
  if (!user.queueToken) return { ...user, join: null };
  const joined = await request("POST", `/api/queue/${eventId}/join`, user.token, undefined, { "X-Queue-Token": user.queueToken });
  return { ...user, join: joined.status, error: joined.status >= 300 ? joined.data : user.error };
}));
const results = await Promise.all(joinedUsers.map(async (user) => {
  if (!user.queueToken || user.join < 200 || user.join >= 300) return { ...user, status: null, position: null };
  const status = await request("GET", `/api/queue/${eventId}/status`, user.token, undefined, { "X-Queue-Token": user.queueToken });
  return { ...user, status: status.status, position: status.data.position, error: status.status >= 300 ? status.data : user.error };
}));
const passed = (key) => results.filter((result) => result[key] >= 200 && result[key] < 300).length;
const positions = results.map((result) => result.position).filter((position) => Number.isInteger(position));
console.log(`REGISTER: ${passed("register")}/50`);
console.log(`JOIN:     ${passed("join")}/50`);
console.log(`STATUS:   ${passed("status")}/50`);
console.log(`POSITIONS: ${positions.length} returned, ${new Set(positions).size} unique`);
results.filter((result) => result.error).slice(0, 5).forEach((result) => console.log("FAILURE", result));
process.exit(passed("register") === 50 && passed("join") === 50 && passed("status") === 50 && new Set(positions).size === positions.length ? 0 : 1);
