-- =============================================================================
-- ConcertShieldSystem / EventAPI
-- Migration: concert approval lifecycle + mixed seated/standing zones
--
-- Idempotent: safe to run more than once.
--
-- Preferred path is EF Core:
--     cd backend/EventAPI
--     dotnet ef migrations add ConcertLifecycleAndMixedZones
--     dotnet ef database update
--
-- Use this script when you'd rather apply the change straight to PostgreSQL:
--     psql -U postgres -d event_db -f Migrations/001_concert_lifecycle_and_zones.sql
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1. events: lifecycle audit columns
-- -----------------------------------------------------------------------------
ALTER TABLE events ADD COLUMN IF NOT EXISTS submitted_at timestamp with time zone NULL;
ALTER TABLE events ADD COLUMN IF NOT EXISTS approved_at  timestamp with time zone NULL;
ALTER TABLE events ADD COLUMN IF NOT EXISTS rejected_at  timestamp with time zone NULL;
ALTER TABLE events ADD COLUMN IF NOT EXISTS reviewed_by  integer NULL;

COMMENT ON COLUMN events.submitted_at IS 'Set when the owner submits Draft/Rejected -> Pending.';
COMMENT ON COLUMN events.approved_at  IS 'Set when an Admin approves Pending -> Published.';
COMMENT ON COLUMN events.rejected_at  IS 'Set when an Admin rejects Pending -> Rejected.';
COMMENT ON COLUMN events.reviewed_by  IS 'Admin user id that approved or rejected the concert.';

-- Status is filtered on every public list query.
CREATE INDEX IF NOT EXISTS ix_events_status ON events (status);

-- -----------------------------------------------------------------------------
-- 2. events: migrate the legacy "Approved" status to "Published"
--    The code also normalises this at runtime, so this is belt-and-braces.
-- -----------------------------------------------------------------------------
UPDATE events
SET    status = 'Published'
WHERE  status = 'Approved';

-- Backfill the new timestamps for rows that predate them, so the admin UI
-- doesn't show blanks for concerts that were already reviewed.
UPDATE events
SET    approved_at = COALESCE(approved_at, published_at, updated_at)
WHERE  status = 'Published'
AND    approved_at IS NULL;

UPDATE events
SET    rejected_at = COALESCE(rejected_at, updated_at)
WHERE  status = 'Rejected'
AND    rejected_at IS NULL;

UPDATE events
SET    submitted_at = COALESCE(submitted_at, updated_at)
WHERE  status IN ('Pending', 'Published', 'Rejected')
AND    submitted_at IS NULL;

-- -----------------------------------------------------------------------------
-- 3. seat_zones: zone-level seated/standing support
--
--    A concert can now mix both in one layout: numbered seats on the balcony
--    plus a standing pit at the front.
--      Seated   -> one row in `seats` per physical seat; buyers pick a seat.
--      Standing -> no `seats` rows at all; `capacity` is the headcount.
-- -----------------------------------------------------------------------------
ALTER TABLE seat_zones ADD COLUMN IF NOT EXISTS zone_type varchar(20) NOT NULL DEFAULT 'Seated';
ALTER TABLE seat_zones ADD COLUMN IF NOT EXISTS capacity  integer     NOT NULL DEFAULT 0;

COMMENT ON COLUMN seat_zones.zone_type IS 'Seated | Standing.';
COMMENT ON COLUMN seat_zones.capacity  IS 'Headcount. Seated: kept in sync with the seat count. Standing: set by the organizer.';

-- Guard against typos writing anything other than the two supported modes.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_seat_zones_zone_type'
    ) THEN
        ALTER TABLE seat_zones
            ADD CONSTRAINT ck_seat_zones_zone_type
            CHECK (zone_type IN ('Seated', 'Standing'));
    END IF;
END $$;

-- Existing zones were all seated, so backfill capacity from the seats they own.
UPDATE seat_zones z
SET    capacity = COALESCE(s.seat_count, 0)
FROM   (
           SELECT seat_zone_id, COUNT(*) AS seat_count
           FROM   seats
           GROUP  BY seat_zone_id
       ) s
WHERE  z.seat_zone_id = s.seat_zone_id
AND    z.capacity = 0;

CREATE INDEX IF NOT EXISTS ix_seat_zones_ticket_type_id ON seat_zones (ticket_type_id);

-- -----------------------------------------------------------------------------
-- 4. ticket_types: protect names inside one concert.
--    Quantity remains the organizer's declared quota; zone capacity is checked
--    against it by SeatingService and must match before submission.
-- -----------------------------------------------------------------------------
CREATE UNIQUE INDEX IF NOT EXISTS uq_ticket_types_event_name_active
ON ticket_types(event_id, lower(trim(type_name)))
WHERE is_deleted = false;

COMMIT;

-- -----------------------------------------------------------------------------
-- Verification
-- -----------------------------------------------------------------------------
-- SELECT status, COUNT(*) FROM events GROUP BY status;
-- SELECT zone_type, COUNT(*), SUM(capacity) FROM seat_zones GROUP BY zone_type;
