-- Run against event_db to open Mason Nguyễn ticket sales immediately.
UPDATE ticket_types t
SET sales_starts_at = '2026-11-11 00:00:00+07'::timestamptz,
    updated_at = now()
FROM events e
WHERE e.event_id = t.event_id
  AND e.slug = 'real-mason-off-the-record-2026'
  AND NOT t.is_deleted;

SELECT e.slug, e.status, e.compliance_status,
       e.compliance_version, e.compliance_reviewed_version,
       t.type_name, t.sales_starts_at, t.sales_ends_at,
       t.quantity, t.sold_quantity
FROM events e
JOIN ticket_types t ON t.event_id = e.event_id
WHERE e.slug = 'real-mason-off-the-record-2026'
ORDER BY t.sort_order;
