-- UC_14.3 / UC_14.4: staff members assigned to an event. Apply once to event_db.
-- Unassigning keeps the row (is_active = false) so the history is not lost.
-- The partial unique index allows a staff member to be assigned again later,
-- but never twice at the same time.
CREATE TABLE IF NOT EXISTS public.event_staff (
    event_staff_id  integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id        integer NOT NULL,
    staff_user_id   integer NOT NULL,
    gate_name       varchar(100) NOT NULL DEFAULT 'Main Gate',
    assigned_by     integer NOT NULL,
    assigned_at     timestamp with time zone NOT NULL DEFAULT now(),
    is_active       boolean NOT NULL DEFAULT true,
    unassigned_by   integer NULL,
    unassigned_at   timestamp with time zone NULL,
    CONSTRAINT fk_event_staff_event FOREIGN KEY (event_id) REFERENCES public.events (event_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_event_staff_active
    ON public.event_staff (event_id, staff_user_id) WHERE is_active = true;
CREATE INDEX IF NOT EXISTS ix_event_staff_staff_user
    ON public.event_staff (staff_user_id) WHERE is_active = true;

ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS gate_name varchar(100) NOT NULL DEFAULT 'Main Gate';
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS shift_date date NULL;
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS shift_start_time varchar(50) NULL;
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS shift_end_time varchar(50) NULL;
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS can_checkin boolean NOT NULL DEFAULT true;
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS can_review_returns boolean NOT NULL DEFAULT false;
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS can_manage_incidents boolean NOT NULL DEFAULT false;
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS can_manage_support_cases boolean NOT NULL DEFAULT false;
