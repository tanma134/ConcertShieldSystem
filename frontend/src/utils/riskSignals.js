// src/utils/riskSignals.js
// Thu tín hiệu phía trình duyệt cho Risk Engine và gửi kèm mọi request qua header.
// Backend đọc các header này ở RiskRequestContext (X-Session-Id, X-Device-Fp, X-Webdriver, X-Had-Input).

const SESSION_KEY = "riskSid";

let hadInput = false;
let tracking = false;
let cachedFp = null;

// Hash 53-bit đơn giản, đồng bộ (không cần thư viện)
function cyrb53(str, seed = 0) {
  let h1 = 0xdeadbeef ^ seed;
  let h2 = 0x41c6ce57 ^ seed;
  for (let i = 0; i < str.length; i++) {
    const ch = str.charCodeAt(i);
    h1 = Math.imul(h1 ^ ch, 2654435761);
    h2 = Math.imul(h2 ^ ch, 1597334677);
  }
  h1 = Math.imul(h1 ^ (h1 >>> 16), 2246822507) ^ Math.imul(h2 ^ (h2 >>> 13), 3266489909);
  h2 = Math.imul(h2 ^ (h2 >>> 16), 2246822507) ^ Math.imul(h1 ^ (h1 >>> 13), 3266489909);
  return 4294967296 * (2097151 & h2) + (h1 >>> 0);
}

function canvasSignature() {
  try {
    const c = document.createElement("canvas");
    c.width = 200;
    c.height = 40;
    const ctx = c.getContext("2d");
    ctx.textBaseline = "top";
    ctx.font = "14px Arial";
    ctx.fillStyle = "#f60";
    ctx.fillRect(10, 5, 80, 20);
    ctx.fillStyle = "#069";
    ctx.fillText("risk-fp \u{1F3AB} 1.0", 4, 8);
    return c.toDataURL();
  } catch {
    return "";
  }
}

// Vân tay thiết bị: ổn định giữa các lần mở trang / ẩn danh, khác nhau giữa các máy.
// Dùng để gom nhiều tài khoản chung một thiết bị (BOT_S01, FRD_M01, block theo DEVICE).
export function getDeviceFp() {
  if (cachedFp) return cachedFp;
  try {
    const parts = [
      navigator.userAgent,
      navigator.language,
      (navigator.languages || []).join(","),
      `${screen.width}x${screen.height}x${screen.colorDepth}`,
      Intl.DateTimeFormat().resolvedOptions().timeZone,
      navigator.hardwareConcurrency,
      navigator.platform,
      canvasSignature(),
    ].join("||");
    cachedFp = "fp_" + cyrb53(parts).toString(16) + cyrb53(parts, 7).toString(16);
  } catch {
    cachedFp = "fp_unknown";
  }
  return cachedFp;
}

export function getSessionId() {
  try {
    let sid = sessionStorage.getItem(SESSION_KEY);
    if (!sid) {
      sid =
        typeof crypto !== "undefined" && crypto.randomUUID
          ? crypto.randomUUID()
          : `s_${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`;
      sessionStorage.setItem(SESSION_KEY, sid);
    }
    return sid;
  } catch {
    return "s_unavailable";
  }
}

// Gọi một lần khi app khởi động (main.jsx). Có thao tác thật của người dùng hay chưa
// (di chuột / chạm / cuộn / gõ phím) => header X-Had-Input, dùng cho BOT_E03.
export function startRiskTracking() {
  if (tracking || typeof window === "undefined") return;
  tracking = true;
  const events = ["mousemove", "pointerdown", "touchstart", "scroll", "keydown", "wheel"];
  const onInput = () => {
    hadInput = true;
    events.forEach((e) => window.removeEventListener(e, onInput, true));
  };
  events.forEach((e) => window.addEventListener(e, onInput, { capture: true, passive: true }));
}

// Header gửi kèm mọi request tới backend.
export function riskHeaders() {
  return {
    "X-Session-Id": getSessionId(),
    "X-Device-Fp": getDeviceFp(),
    "X-Webdriver": typeof navigator !== "undefined" && navigator.webdriver ? "1" : "0",
    "X-Had-Input": hadInput ? "1" : "0",
  };
}
