-- The duplicate-assignment guard for event staff needs a partial unique index on
-- event_staff. In the live database that index name ended up on an unrelated table
-- (event_staff_assignments), so nothing stopped the same staff account from being
-- added to the same event twice while it was active.
--
-- This migration is idempotent: it is safe to run more than once.

-- Drop the misplaced copy if this database has it on the wrong table.
DROP INDEX IF EXISTS public.uq_event_staff_active_wrong_placeholder;

-- Remove any duplicate active rows first, otherwise the unique index cannot be built.
-- Keep the lowest id in each duplicate group.
WITH duplicates AS (
    SELECT event_staff_id,
           ROW_NUMBER() OVER (
               PARTITION BY event_id, staff_user_id
               ORDER BY event_staff_id
           ) AS rn
    FROM public.event_staff
    WHERE is_active = true
)
UPDATE public.event_staff es
SET is_active = false
FROM duplicates d
WHERE es.event_staff_id = d.event_staff_id
  AND d.rn > 1;

-- The same index name may already exist on a different table. CREATE INDEX cannot
-- move it, so drop whatever is using the name and rebuild it where it belongs.
DROP INDEX IF EXISTS public.uq_event_staff_active;

CREATE UNIQUE INDEX IF NOT EXISTS uq_event_staff_active
    ON public.event_staff (event_id, staff_user_id)
    WHERE is_active = true;

CREATE INDEX IF NOT EXISTS ix_event_staff_return_reviewers
    ON public.event_staff (event_id, staff_user_id)
    WHERE is_active = true AND can_review_returns = true;
