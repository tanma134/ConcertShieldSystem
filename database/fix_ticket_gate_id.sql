-- TicketAPI maps tickets.gate_id as a nullable string, not an integer gate FK.
-- Apply to ticket_db. Safe to run again; existing ticket data is preserved.
BEGIN;
ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS gate_id varchar(50) NULL;
COMMIT;
