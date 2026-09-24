-- =============================================================================
-- ConcertShieldSystem / EventAPI
-- Migration: 005_seating_mode_and_shape_versioning.sql
-- Idempotent: safe to run more than once.
--
-- 1. Adds seating_mode to events ('GeneralAdmission', 'StandingZones', 'ReservedSeating')
-- 2. Adds seating_mode to seating_templates ('StandingZones', 'ReservedSeating')
-- 3. Backfills existing events & templates
-- 4. Ensures seated zone capacity matches count of seats where capacity was 0
-- =============================================================================

BEGIN;

-- -----------------------------------------------------------------------------
-- 1. events.seating_mode
-- -----------------------------------------------------------------------------
ALTER TABLE public.events
ADD COLUMN IF NOT EXISTS seating_mode varchar(30) NOT NULL DEFAULT 'ReservedSeating';

COMMENT ON COLUMN public.events.seating_mode IS 'GeneralAdmission | StandingZones | ReservedSeating. Exclusive per event.';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_events_seating_mode'
    ) THEN
        ALTER TABLE public.events
            ADD CONSTRAINT ck_events_seating_mode
            CHECK (seating_mode IN ('GeneralAdmission', 'StandingZones', 'ReservedSeating'));
    END IF;
END $$;

-- Backfill seating_mode for existing events
UPDATE public.events
SET seating_mode = 'GeneralAdmission'
WHERE has_seating_chart = false;

UPDATE public.events e
SET seating_mode = CASE
    WHEN EXISTS (
        SELECT 1 FROM public.seat_maps m
        JOIN public.seat_zones z ON z.seat_map_id = m.seat_map_id
        WHERE m.event_id = e.event_id AND z.zone_type = 'Standing'
    ) AND NOT EXISTS (
        SELECT 1 FROM public.seat_maps m
        JOIN public.seat_zones z ON z.seat_map_id = m.seat_map_id
        WHERE m.event_id = e.event_id AND z.zone_type = 'Seated'
    ) THEN 'StandingZones'
    ELSE 'ReservedSeating'
END
WHERE has_seating_chart = true;

-- -----------------------------------------------------------------------------
-- 2. seating_templates.seating_mode
-- -----------------------------------------------------------------------------
ALTER TABLE public.seating_templates
ADD COLUMN IF NOT EXISTS seating_mode varchar(30) NOT NULL DEFAULT 'ReservedSeating';

COMMENT ON COLUMN public.seating_templates.seating_mode IS 'StandingZones | ReservedSeating. Exclusive per template.';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_seating_templates_seating_mode'
    ) THEN
        ALTER TABLE public.seating_templates
            ADD CONSTRAINT ck_seating_templates_seating_mode
            CHECK (seating_mode IN ('StandingZones', 'ReservedSeating'));
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 3. Fix legacy data where Seated zone capacity was 0 but had seat records
-- -----------------------------------------------------------------------------
UPDATE public.seat_zones z
SET capacity = COALESCE(s.seat_count, 0)
FROM (
    SELECT seat_zone_id, COUNT(*) AS seat_count
    FROM public.seats
    GROUP BY seat_zone_id
) s
WHERE z.seat_zone_id = s.seat_zone_id
  AND z.zone_type = 'Seated'
  AND (z.capacity = 0 OR z.capacity IS NULL);

COMMIT;
