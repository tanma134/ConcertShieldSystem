// Validation FE báo sớm; backend luôn kiểm tra lại cùng quy tắc.
export function validateChange(input, now = Date.now()) {
  if (!['Postpone','Reschedule'].includes(input.type)) return 'Select a valid change type.';
  const reason = (input.reason || '').trim();
  if (reason.length < 10 || reason.length > 1000) return 'Reason must contain 10 to 1000 characters.';
  if (input.type === 'Postpone') return '';
  const start = Date.parse(input.newStartsAt), end = Date.parse(input.newEndsAt);
  if (!Number.isFinite(start) || !Number.isFinite(end)) return 'Enter the new start and end.';
  if (start <= now) return 'New start must be in the future.';
  if (end <= start) return 'New end must be after new start.';
  return '';
}
export function validateReview(decision, notes) {
  if (!['Approved','Rejected','RequestMoreInfo'].includes(decision)) return 'Select a decision.';
  const clean = (notes || '').trim();
  return !clean || clean.length > 2000 ? 'Review notes must contain 1 to 2000 characters.' : '';
}
// Report 3 (3.10.10): note is required only when rejecting (10-1000 characters); optional (max 1000) when approving.
export function validateChangeReview(decision, notes) {
  if (!['Approved','Rejected'].includes(decision)) return 'Select a decision.';
  const clean = (notes || '').trim();
  if (clean.length > 1000) return 'Admin note must contain at most 1000 characters.';
  if (decision === 'Rejected' && clean.length < 10) return 'Please enter a note of at least 10 characters when rejecting.';
  return '';
}
export function canPublish(record) { return record?.status === 'Approved' && record.version > 0 && record.version === record.reviewedVersion; }
export function fileError(file, maxBytes) {
  if (!file || file.size <= 0) return 'Select a non-empty file.';
  if (file.size > maxBytes) return 'File exceeds the maximum size.';
  if (!/\.(pdf|png|jpe?g)$/i.test(file.name)) return 'Only PDF, PNG and JPEG are supported.';
  return '';
}
// Chuyển giờ nhập theo timezone concert, không phụ thuộc timezone máy người dùng.
export function localToUtc(value, zone) {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value || '')) throw new Error('Enter a valid local date and time.');
  const timezone = zone === 'SE Asia Standard Time' ? 'Asia/Ho_Chi_Minh' : zone;
  const format = new Intl.DateTimeFormat('en-CA', {timeZone: timezone, year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',hourCycle:'h23'});
  const target = Date.parse(value + ':00Z'); let utc = target;
  const parts = (time) => Object.fromEntries(format.formatToParts(new Date(time)).filter(p=>p.type!=='literal').map(p=>[p.type,p.value]));
  for(let i=0;i<4;i++) {
    const p = parts(utc); const represented = Date.UTC(+p.year,+p.month-1,+p.day,+p.hour,+p.minute);
    const difference = target - represented; if (!difference) break; utc += difference;
  }
  const p = parts(utc);
  if (`${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}` !== value) throw new Error('This time does not exist in the concert timezone.');
  // Thời gian DST lặp phải được nhập bằng công cụ chọn offset rõ ràng, không chọn ngầm.
  for (const shift of [-3600000,3600000]) {
    const q=parts(utc+shift);
    if (`${q.year}-${q.month}-${q.day}T${q.hour}:${q.minute}` === value) throw new Error('Ambiguous daylight-saving time. Choose another time.');
  }
  return new Date(utc).toISOString();
}
