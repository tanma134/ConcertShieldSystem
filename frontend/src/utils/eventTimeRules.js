// So sánh timestamp đầy đủ, kể cả giờ/phút; gọi lại ngay trước khi gửi API.
export function startTimeError(value, now = Date.now()) {
  const time = Date.parse(value);
  if (!value || !Number.isFinite(time)) return 'Choose a valid start date and time.';
  return time <= now ? 'Start date and time must be later than now (including hours and minutes).' : '';
}

// Rule theo thời gian phải nằm trong khoảng bán vé và trước lúc concert bắt đầu.
export function pricingTimeError(rule, ticket, event, now = Date.now()) {
  if (rule.ruleType === 'QuantityBased') return '';
  const from = Date.parse(rule.triggerFrom), to = Date.parse(rule.triggerTo);
  if (!Number.isFinite(from) || !Number.isFinite(to)) return 'Choose both discount start and end date/time.';
  if (from <= now) return 'Discount start date and time must be later than now.';
  if (to <= from) return 'Discount end date and time must be after its start.';
  const saleStart = Date.parse(ticket?.salesStartsAt), saleEnd = Date.parse(ticket?.salesEndsAt);
  const concertStart = Date.parse(event?.startsAt);
  if (Number.isFinite(saleStart) && from < saleStart) return 'Discount cannot start before ticket sales open.';
  if (Number.isFinite(saleEnd) && to > saleEnd) return 'Discount cannot end after ticket sales close.';
  if (Number.isFinite(concertStart) && to > concertStart) return 'Discount must end by the concert start time.';
  return '';
}

// Làm tròn lên phút tiếp theo để datetime-local không chọn phút hiện tại đã trôi qua.
export function nextLocalMinute(now = Date.now()) {
  const d = new Date(Math.floor(now / 60000) * 60000 + 60000);
  const pad = n => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
