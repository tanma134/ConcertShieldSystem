-- =============================================================================
-- MIGRATION: Add shift_end_date to event_staff (event_db)
-- Purpose: Support multi-day / From-To datetime shift assignments for event staff
-- =============================================================================
\connect event_db

ALTER TABLE event_staff
    ADD COLUMN IF NOT EXISTS shift_end_date DATE;

-- Backfill existing records where shift_end_date is null
UPDATE event_staff
SET shift_end_date = shift_date
WHERE shift_end_date IS NULL
  AND shift_date IS NOT NULL;
