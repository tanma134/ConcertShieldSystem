const pad2 = (n) => String(n).padStart(2, "0");

const MONTHS = [
  "Jan", "Feb", "Mar", "Apr", "May", "Jun",
  "Jul", "Aug", "Sep", "Oct", "Nov", "Dec",
];

// Sep 12, 2026
export function formatDate(value) {
  if (!value) return "";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return "";
  return `${MONTHS[d.getMonth()]} ${d.getDate()}, ${d.getFullYear()}`;
}

// 12:00, Sep 12, 2026
export function formatDateTime(value) {
  if (!value) return "";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return "";
  return `${pad2(d.getHours())}:${pad2(d.getMinutes())}, ${formatDate(value)}`;
}

// 12:00, Sep 12, 2026 - 18:00, Oct 30, 2026
export function formatDateRange(startsAt, endsAt) {
  const start = formatDateTime(startsAt);
  const end = formatDateTime(endsAt);
  if (start && end && start !== end) return `${start} - ${end}`;
  return start || end;
}

// 250,000 VND
export function formatPrice(value) {
  if (value === null || value === undefined) return "Contact us";
  if (value === 0) return "Free";
  return `${Number(value).toLocaleString("en-US")} VND`;
}
