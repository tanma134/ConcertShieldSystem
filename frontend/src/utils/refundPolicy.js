export const REFUND_REQUEST_SUCCESS_MESSAGE =
  "Your request was submitted successfully. Staff review the request within 3 business days. After approval, the refund may take 1–7 business days to reach your account, depending on the bank or payment provider.";

export const REFUND_POLICY_WARNING = (hours) =>
  `Customer-requested refunds close ${hours} hours before the event starts. Tickets purchased after this deadline may only be refunded if the event is cancelled, postponed, or materially changed under the event policy.`;

export function getRefundDeadlineHours(policies) {
  const deadlines = (policies || [])
    .map((policy) => Number(policy?.deadlineBeforeEventHours))
    .filter((hours) => Number.isFinite(hours) && hours >= 0);
  return deadlines.length ? Math.min(...deadlines) : null;
}

export function getCustomerRefundWarning(event, now = Date.now()) {
  const hours = getRefundDeadlineHours(event?.refundPolicies);
  const startsAt = event?.startsAt ? new Date(event.startsAt).getTime() : NaN;
  if (hours == null || !Number.isFinite(startsAt)) return null;

  const deadline = startsAt - hours * 60 * 60 * 1000;
  return now >= deadline ? REFUND_POLICY_WARNING(hours) : null;
}

export function maskAccountNumber(accountNumber) {
  if (accountNumber == null || String(accountNumber).trim() === "") return "";
  const value = String(accountNumber).trim();
  if (value.length <= 4) return "*".repeat(value.length);
  return `${"*".repeat(value.length - 4)}${value.slice(-4)}`;
}
