-- Reusable seating-chart templates (UC_26.2 Apply Seating Template / UC_26.3 Save
-- Seating Chart as Template). Non-destructive — safe to re-run.
--
-- Deliberately NOT linked to ticket_types: a template is reused across concerts
-- whose ticket types differ each time, so only zone geometry/rows/capacity is
-- stored here. The ticket type binding is chosen again at apply-time and lives in
-- seat_zones as usual (via SeatingService.BuildAsync).

CREATE TABLE IF NOT EXISTS public.seating_templates
(
    seating_template_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    organizer_id integer NOT NULL,                 -- soft ref -> identity_db.users; owner of the template
    name character varying(150) COLLATE pg_catalog."default" NOT NULL,
    description character varying(500) COLLATE pg_catalog."default",
    is_public boolean NOT NULL DEFAULT false,       -- true only for admin-provided starter templates
    layout_json jsonb,                              -- canvas/stage metadata, mirrors seat_maps.layout_json
    zones_json jsonb NOT NULL,                      -- serialized zone shapes + rows/seats-per-row/capacity
    created_by integer NOT NULL,                    -- soft ref -> identity_db.users
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone NOT NULL DEFAULT now(),
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    CONSTRAINT seating_templates_pkey PRIMARY KEY (seating_template_id)
);

CREATE INDEX IF NOT EXISTS ix_seating_templates_organizer_id ON public.seating_templates(organizer_id);
CREATE INDEX IF NOT EXISTS ix_seating_templates_is_public ON public.seating_templates(is_public);
