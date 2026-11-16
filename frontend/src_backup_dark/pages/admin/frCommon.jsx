import AdminShell from "./AdminShell";
import "./fraudRisk.css";

export const fmt = (v) => (v ? new Date(v).toLocaleString("en-GB") : "—");

// Lấy giá trị đầu tiên tồn tại trong nhiều tên field (backend DTO có thể đặt tên khác nhau).
// Chỉ cần sửa tên field ở các trang, không phải sửa logic.
export const g = (o, ...keys) => {
  for (const k of keys) if (o?.[k] != null) return o[k];
  return undefined;
};

export const errMsg = (e, fallback) => {
  if (e.response?.status === 403) return "Only Admin accounts with fraud/risk access can do this.";
  return e.response?.data?.message || fallback;
};

export const dash = (v) => (v == null || v === "" ? "—" : String(v));

export function Shell({ title, children }) {
  return (
    <AdminShell title={title}>
      <div className="fr-page">{children}</div>
    </AdminShell>
  );
}

export function Banner({ error, notice, onClearError, onClearNotice }) {
  return (
    <>
      {error && (
        <div className="auth-message error">
          {error}
          <button type="button" onClick={onClearError}>Dismiss</button>
        </div>
      )}
      {notice && (
        <div className="auth-message">
          {notice}
          <button type="button" onClick={onClearNotice}>Dismiss</button>
        </div>
      )}
    </>
  );
}

export function levelPill(level) {
  if (level === "Critical" || level === "High") return "kyc-pill--bad";
  if (level === "Medium") return "kyc-pill--pending";
  if (level === "Low") return "kyc-pill--view";
  return "kyc-pill--muted";
}

export function statusPill(status) {
  switch (status) {
    case "Active":
    case "Open":
    case "Rejected":
      return "kyc-pill--bad";
    case "Pending":
    case "InReview":
      return "kyc-pill--pending";
    case "Accepted":
    case "Overturned":
    case "Restored":
    case "Resolved":
    case "FalsePositive":
      return "kyc-pill--ok";
    default:
      return "kyc-pill--muted";
  }
}

// Mảng / object / chuỗi JSON -> mảng chuỗi để hiển thị thành chip
export function toList(value) {
  let v = value;
  if (typeof v === "string") {
    try { v = JSON.parse(v); } catch { return v.trim() ? [v] : []; }
  }
  if (v == null) return [];
  if (!Array.isArray(v)) {
    if (typeof v === "object") {
      return Object.entries(v).map(([k, val]) =>
        typeof val === "object" ? k : `${k}: ${val}`);
    }
    return [String(v)];
  }
  return v.map((x) => {
    if (x == null || typeof x !== "object") return String(x);
    const title = g(x, "code", "ruleCode", "rule", "ruleName", "name", "id");
    const desc = g(x, "description", "reason", "label");
    const extra = g(x, "score", "value", "weight");
    return [title, desc, extra != null ? `(${extra})` : null].filter(Boolean).join(" ");
  });
}

export function Chips({ value }) {
  const items = toList(value);
  if (items.length === 0) return <span className="fr-hint">—</span>;
  return <div>{items.map((t, i) => <i key={i} className="fr-chip" style={{ fontStyle: "normal" }}>{t}</i>)}</div>;
}

// Giờ còn lại tới hạn SLA
export function slaInfo(dueAt) {
  if (!dueAt) return null;
  const hours = Math.round((new Date(dueAt).getTime() - Date.now()) / 36e5);
  return hours >= 0
    ? { text: `SLA due in ${hours} h`, cls: hours <= 6 ? "kyc-pill--bad" : "kyc-pill--pending" }
    : { text: `SLA overdue ${Math.abs(hours)} h`, cls: "kyc-pill--bad" };
}
