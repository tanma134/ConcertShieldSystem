-- ============================================================
-- event_db  (EventAPI)
-- Tables: events, event_images, ticket_types, pricing_rules,
--         refund_policies, seat_maps, seat_zones, wishlists
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.events
(
    event_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    organizer_id integer NOT NULL,               -- soft ref -> identity_db.users
    category_id integer NOT NULL,
    title character varying(200) COLLATE pg_catalog."default" NOT NULL,
    slug character varying(250) COLLATE pg_catalog."default" NOT NULL,
    short_description character varying(500) COLLATE pg_catalog."default",
    description text COLLATE pg_catalog."default",
    poster_url character varying(500) COLLATE pg_catalog."default",
    banner_url character varying(500) COLLATE pg_catalog."default",
    location_name character varying(200) COLLATE pg_catalog."default",
    address character varying(300) COLLATE pg_catalog."default",
    city character varying(100) COLLATE pg_catalog."default",
    longitude numeric(11, 8),
    latitude numeric(10, 8),
    starts_at timestamp with time zone NOT NULL,
    ends_at timestamp with time zone NOT NULL,
    timezone character varying(50) COLLATE pg_catalog."default" NOT NULL DEFAULT 'SE Asia Standard Time'::character varying,
    has_seating_chart boolean NOT NULL DEFAULT false,
    requires_virtual_queue boolean NOT NULL DEFAULT false,
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Draft'::character varying,
    rejected_reason character varying(500) COLLATE pg_catalog."default",
    is_featured boolean NOT NULL DEFAULT false,
    view_count integer NOT NULL DEFAULT 0,
    total_tickets integer NOT NULL DEFAULT 0,
    sold_tickets integer NOT NULL DEFAULT 0,
    min_tickets_per_account integer,
    max_tickets_per_account integer,
    meta_title character varying(200) COLLATE pg_catalog."default",
    meta_description character varying(500) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone NOT NULL DEFAULT now(),
    published_at timestamp with time zone,
    created_by integer,                            -- soft ref -> identity_db.users
    updated_by integer,                             -- soft ref -> identity_db.users
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                             -- soft ref -> identity_db.users
    CONSTRAINT events_pkey PRIMARY KEY (event_id)
);
CREATE INDEX IF NOT EXISTS ix_events_organizer_id ON public.events(organizer_id);

CREATE TABLE IF NOT EXISTS public.event_images
(
    image_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,
    image_url character varying(500) COLLATE pg_catalog."default" NOT NULL,
    sort_order integer NOT NULL DEFAULT 0,
    is_main boolean NOT NULL DEFAULT false,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                             -- soft ref -> identity_db.users
    CONSTRAINT event_images_pkey PRIMARY KEY (image_id)
);
CREATE INDEX IF NOT EXISTS uq_event_main_image ON public.event_images(event_id);

CREATE TABLE IF NOT EXISTS public.ticket_types
(
    ticket_type_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,
    type_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    description character varying(500) COLLATE pg_catalog."default",
    price bigint NOT NULL,
    original_price bigint,
    quantity integer NOT NULL,
    sold_quantity integer NOT NULL DEFAULT 0,
    min_per_order integer NOT NULL DEFAULT 1,
    max_per_order integer NOT NULL DEFAULT 10,
    color_code character varying(20) COLLATE pg_catalog."default",
    sort_order integer NOT NULL DEFAULT 0,
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Active'::character varying,
    sales_starts_at timestamp with time zone,
    sales_ends_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone NOT NULL DEFAULT now(),
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                             -- soft ref -> identity_db.users
    CONSTRAINT ticket_types_pkey PRIMARY KEY (ticket_type_id)
);
CREATE INDEX IF NOT EXISTS ix_ticket_types_event_id ON public.ticket_types(event_id);

CREATE TABLE IF NOT EXISTS public.pricing_rules
(
    pricing_rule_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    ticket_type_id integer NOT NULL,
    rule_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    rule_type character varying(30) COLLATE pg_catalog."default" NOT NULL,
    adjusted_price bigint,
    discount_percent numeric(5, 2),
    trigger_from timestamp with time zone,
    trigger_to timestamp with time zone,
    quantity_threshold integer,
    priority integer NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT pricing_rules_pkey PRIMARY KEY (pricing_rule_id)
);
CREATE INDEX IF NOT EXISTS ix_pricing_rules_ticket_type_id ON public.pricing_rules(ticket_type_id);

CREATE TABLE IF NOT EXISTS public.refund_policies
(
    refund_policy_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,
    policy_name character varying(150) COLLATE pg_catalog."default" NOT NULL,
    description character varying(1000) COLLATE pg_catalog."default",
    deadline_before_event_hours integer NOT NULL,
    refund_percent numeric(5, 2) NOT NULL,
    requires_organizer_approval boolean NOT NULL DEFAULT true,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT refund_policies_pkey PRIMARY KEY (refund_policy_id)
);
CREATE INDEX IF NOT EXISTS ix_refund_policies_event_id ON public.refund_policies(event_id);

CREATE TABLE IF NOT EXISTS public.seat_maps
(
    seat_map_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,
    name character varying(150) COLLATE pg_catalog."default" NOT NULL,
    layout_json jsonb,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone NOT NULL DEFAULT now(),
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                             -- soft ref -> identity_db.users
    CONSTRAINT seat_maps_pkey PRIMARY KEY (seat_map_id)
);
CREATE INDEX IF NOT EXISTS ix_seat_maps_event_id ON public.seat_maps(event_id);

CREATE TABLE IF NOT EXISTS public.seat_zones
(
    seat_zone_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    seat_map_id integer NOT NULL,
    ticket_type_id integer NOT NULL,
    zone_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    shape_json jsonb,
    CONSTRAINT seat_zones_pkey PRIMARY KEY (seat_zone_id)
);
CREATE INDEX IF NOT EXISTS ix_seat_zones_seat_map_id ON public.seat_zones(seat_map_id);

CREATE TABLE IF NOT EXISTS public.wishlists
(
    wishlist_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,
    user_id integer NOT NULL,                       -- soft ref -> identity_db.users
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT wishlists_pkey PRIMARY KEY (wishlist_id),
    CONSTRAINT uq_wishlists_event_user UNIQUE (event_id, user_id)
);
CREATE INDEX IF NOT EXISTS ix_wishlists_user_id ON public.wishlists(user_id);

-- ===== FK cùng DB (giữ nguyên) =====

ALTER TABLE IF EXISTS public.event_images
    ADD CONSTRAINT fk_event_images_event FOREIGN KEY (event_id)
    REFERENCES public.events (event_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.ticket_types
    ADD CONSTRAINT fk_ticket_types_event FOREIGN KEY (event_id)
    REFERENCES public.events (event_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.pricing_rules
    ADD CONSTRAINT fk_pricing_rules_ticket_type FOREIGN KEY (ticket_type_id)
    REFERENCES public.ticket_types (ticket_type_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.refund_policies
    ADD CONSTRAINT fk_refund_policies_event FOREIGN KEY (event_id)
    REFERENCES public.events (event_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.seat_maps
    ADD CONSTRAINT fk_seat_maps_event FOREIGN KEY (event_id)
    REFERENCES public.events (event_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.seat_zones
    ADD CONSTRAINT fk_seat_zones_seat_map FOREIGN KEY (seat_map_id)
    REFERENCES public.seat_maps (seat_map_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.seat_zones
    ADD CONSTRAINT fk_seat_zones_ticket_type FOREIGN KEY (ticket_type_id)
    REFERENCES public.ticket_types (ticket_type_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.wishlists
    ADD CONSTRAINT fk_wishlists_event FOREIGN KEY (event_id)
    REFERENCES public.events (event_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

-- ===== Soft reference (KHÔNG có FK) =====
-- events.organizer_id / created_by / updated_by / deleted_by -> identity_db.users
-- event_images.deleted_by -> identity_db.users
-- ticket_types.deleted_by -> identity_db.users
-- seat_maps.deleted_by -> identity_db.users
-- wishlists.user_id -> identity_db.users

END;
