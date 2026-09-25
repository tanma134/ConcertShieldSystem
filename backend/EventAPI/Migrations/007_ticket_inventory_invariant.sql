-- Fixes the "event inventory" gap flagged by the review committee: SoldQuantity
-- existed on ticket_types but nothing ever incremented/decremented it safely, and
-- there was no DB-level guarantee that sold_quantity could never exceed quantity.
--
-- This migration adds:
--   1) A CHECK constraint — the invariant holds no matter which process writes the
--      row (EventAPI today, a future Booking/Payment service tomorrow).
--   2) An atomic reserve/release path is implemented in application code via a
--      single conditional UPDATE (see TicketTypeRepository.TryReserveAsync /
--      ReleaseAsync). Postgres's row-level locking on UPDATE already serializes
--      concurrent reservations for the same ticket_type_id, so no separate lock
--      table or advisory lock is needed for this piece.

BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM ticket_types WHERE sold_quantity < 0 OR sold_quantity > quantity
    ) THEN
        RAISE EXCEPTION
            'Existing rows violate sold_quantity <= quantity. Reconcile ticket_types before applying ck_ticket_types_sold_within_quantity.';
    END IF;
END $$;

ALTER TABLE public.ticket_types
    ADD CONSTRAINT ck_ticket_types_sold_within_quantity
    CHECK (sold_quantity >= 0 AND sold_quantity <= quantity);

COMMIT;
