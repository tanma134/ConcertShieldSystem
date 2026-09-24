-- Prevent duplicate active ticket type names inside one concert.
-- Run this after resolving any rows returned by the pre-check query.

BEGIN;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM ticket_types
        WHERE is_deleted = false
        GROUP BY event_id, lower(trim(type_name))
        HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION
            'Duplicate active ticket type names exist. Rename or soft-delete duplicates before applying uq_ticket_types_event_name_active.';
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_ticket_types_event_name_active
ON ticket_types(event_id, lower(trim(type_name)))
WHERE is_deleted = false;

COMMIT;
