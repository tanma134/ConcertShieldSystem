-- Chạy trong ticket_db sau schema orders/tickets/ticket_return_requests hiện có.
BEGIN;
CREATE TABLE IF NOT EXISTS applied_event_changes (
 change_id bigint PRIMARY KEY, event_id integer NOT NULL,
 type text NOT NULL CHECK(type IN ('Postpone','Reschedule')),
 starts_at timestamptz, ends_at timestamptz, schedule_version integer NOT NULL,
 reason text NOT NULL, title text NOT NULL, slug text NOT NULL, applied_at timestamptz NOT NULL,
 UNIQUE(event_id,schedule_version)
);
CREATE TABLE IF NOT EXISTS affected_tickets (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 change_id bigint NOT NULL REFERENCES applied_event_changes(change_id),
 ticket_id integer NOT NULL REFERENCES tickets(ticket_id), order_id integer NOT NULL REFERENCES orders(order_id),
 customer_id integer NOT NULL, paid_amount bigint NOT NULL CHECK(paid_amount >= 0), notified_at timestamptz,
 UNIQUE(change_id,ticket_id)
);
CREATE INDEX IF NOT EXISTS ix_affected_notification_pending ON affected_tickets(id) WHERE notified_at IS NULL;
COMMIT;
