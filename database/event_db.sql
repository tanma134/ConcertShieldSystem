

-- =================================================================
-- BASE EVENT SCHEMA AND EXISTING SEED (READ WARNING)
-- =================================================================
-- ============================================================
-- EVENT API - FULL POSTGRESQL SCHEMA + CLEAN CLOUDINARY SEED
-- Rerunnable seed version
-- Cloudinary: pzccgu3t
--
-- IMPORTANT:
-- 1. EventAPI owns event configuration only; Review is removed to ReviewAPI.
-- 2. Existing EventAPI seed rows are cleared in FK-safe order before reseeding.
-- 3. Lifecycle approval columns are added safely when upgrading an existing DB.
-- 4. Only the Cloudinary seed is kept; the older Picsum seed was removed.
-- ============================================================

-- ============================================================

-- event_db  (EventAPI) - PostgreSQL

-- Đã đối chiếu với backend/EventAPI/Data/EventDbContext.cs và

-- backend/EventAPI/Models/*.cs (17/09/2026) để đảm bảo schema

-- khớp 100% với code, rồi seed dữ liệu mẫu (dịch từ file SQL

-- Server "EventDB (1).sql" bạn gửi) với thời gian dời lên

-- tương lai (sau ngày hiện tại) để hiện trên trang chủ.

-- ============================================================

BEGIN;

-- ============================================================

-- PART 1: SCHEMA (aligned with EventAPI models)

-- ============================================================

-- Legacy Review table moved to its owning service.
-- Review data belongs to ReviewAPI/review_db.
DROP TABLE IF EXISTS public.reviews;

CREATE TABLE IF NOT EXISTS public.events

(

    event_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),

    organizer_id integer NOT NULL,               -- soft ref -> identity_db.users

    category_id integer NOT NULL DEFAULT 1,

    title character varying(200) COLLATE pg_catalog."default" NOT NULL,

    slug character varying(250) COLLATE pg_catalog."default" NOT NULL,

    short_description character varying(500) COLLATE pg_catalog."default",

    description text COLLATE pg_catalog."default",

    poster_url character varying(500) COLLATE pg_catalog."default",

    poster_public_id character varying(500) COLLATE pg_catalog."default",   -- matches Event.PosterPublicId

    banner_url character varying(500) COLLATE pg_catalog."default",

    banner_public_id character varying(500) COLLATE pg_catalog."default",   -- matches Event.BannerPublicId

    location_name character varying(200) COLLATE pg_catalog."default",

    address character varying(300) COLLATE pg_catalog."default",

    city character varying(100) COLLATE pg_catalog."default",

    longitude numeric(11, 8),

    latitude numeric(10, 8),

    starts_at timestamp with time zone NOT NULL,

    ends_at timestamp with time zone NOT NULL,

    timezone character varying(50) COLLATE pg_catalog."default" NOT NULL DEFAULT 'SE Asia Standard Time'::character varying,

    has_seating_chart boolean NOT NULL DEFAULT false,

    seating_mode character varying(30) COLLATE pg_catalog."default" NOT NULL DEFAULT 'ReservedSeating'::character varying,

    requires_virtual_queue boolean NOT NULL DEFAULT false,

    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Draft'::character varying, -- Draft, Pending, Approved, Rejected, Cancelled

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

    submitted_at timestamp with time zone,

    approved_at timestamp with time zone,

    rejected_at timestamp with time zone,

    reviewed_by integer,                          -- soft ref -> identity_db.users

    created_by integer,                            -- soft ref -> identity_db.users

    updated_by integer,                             -- soft ref -> identity_db.users

    is_deleted boolean NOT NULL DEFAULT false,

    deleted_at timestamp with time zone,

    deleted_by integer,                             -- soft ref -> identity_db.users

    CONSTRAINT events_pkey PRIMARY KEY (event_id)

);

CREATE INDEX IF NOT EXISTS ix_events_organizer_id ON public.events(organizer_id);

CREATE UNIQUE INDEX IF NOT EXISTS uq_events_slug ON public.events(slug) WHERE is_deleted = false;

-- CREATE TABLE IF NOT EXISTS does not add new columns to an existing database,
-- therefore lifecycle/media columns are migrated explicitly and safely.
ALTER TABLE public.events ADD COLUMN IF NOT EXISTS poster_public_id character varying(500);
ALTER TABLE public.events ADD COLUMN IF NOT EXISTS banner_public_id character varying(500);
ALTER TABLE public.events ADD COLUMN IF NOT EXISTS submitted_at timestamp with time zone;
ALTER TABLE public.events ADD COLUMN IF NOT EXISTS approved_at timestamp with time zone;
ALTER TABLE public.events ADD COLUMN IF NOT EXISTS rejected_at timestamp with time zone;
ALTER TABLE public.events ADD COLUMN IF NOT EXISTS reviewed_by integer;
ALTER TABLE public.events ADD COLUMN IF NOT EXISTS seating_mode character varying(30) NOT NULL DEFAULT 'ReservedSeating';

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

CREATE INDEX IF NOT EXISTS ix_events_status ON public.events(status);
CREATE TABLE IF NOT EXISTS public.event_images

(

    image_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),

    event_id integer NOT NULL,

    image_url character varying(500) COLLATE pg_catalog."default" NOT NULL,

    public_id character varying(500) COLLATE pg_catalog."default",          -- matches EventImage.PublicId

    sort_order integer NOT NULL DEFAULT 0,

    is_main boolean NOT NULL DEFAULT false,

    created_at timestamp with time zone NOT NULL DEFAULT now(),

    is_deleted boolean NOT NULL DEFAULT false,

    deleted_at timestamp with time zone,

    deleted_by integer,                             -- soft ref -> identity_db.users

    CONSTRAINT event_images_pkey PRIMARY KEY (image_id)

);

DROP INDEX IF EXISTS public.uq_event_main_image;
CREATE UNIQUE INDEX IF NOT EXISTS uq_event_main_image
ON public.event_images(event_id)
WHERE is_main = true AND is_deleted = false;

ALTER TABLE public.event_images ADD COLUMN IF NOT EXISTS public_id character varying(500);

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
CREATE UNIQUE INDEX IF NOT EXISTS uq_ticket_types_event_name_active
ON public.ticket_types(event_id, lower(trim(type_name)))
WHERE is_deleted = false;

ALTER TABLE public.ticket_types DROP CONSTRAINT IF EXISTS ck_ticket_types_sold_within_quantity;
ALTER TABLE public.ticket_types
    ADD CONSTRAINT ck_ticket_types_sold_within_quantity
    CHECK (sold_quantity >= 0 AND sold_quantity <= quantity);

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

    zone_type character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Seated',

    capacity integer NOT NULL DEFAULT 0,

    shape_json jsonb,

    CONSTRAINT seat_zones_pkey PRIMARY KEY (seat_zone_id),

    CONSTRAINT ck_seat_zones_zone_type CHECK (zone_type IN ('Seated', 'Standing'))

);

COMMENT ON COLUMN seat_zones.zone_type IS 'Seated | Standing.';
COMMENT ON COLUMN seat_zones.capacity IS 'Headcount. Seated: kept in sync with the seat count. Standing: set by the organizer.';

CREATE INDEX IF NOT EXISTS ix_seat_zones_seat_map_id ON public.seat_zones(seat_map_id);
CREATE INDEX IF NOT EXISTS ix_seat_zones_ticket_type_id ON public.seat_zones(ticket_type_id);

-- -----------------------------------------------------------------------------
-- seats: individual physical seats for a Seated zone.
--   Seated zones   -> one row per physical seat here; buyers pick their own seat.
--   Standing zones -> no rows here at all; headcount lives on seat_zones.capacity.
-- Owned by EventAPI (event_db) -- see team decision: seats stays with Event, not Queue.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.seats

(

    seat_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),

    seat_zone_id integer NOT NULL,

    row_label character varying(10) COLLATE pg_catalog."default" NOT NULL,

    seat_number character varying(10) COLLATE pg_catalog."default" NOT NULL,

    x_coordinate integer,

    y_coordinate integer,

    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Available',



    updated_at timestamp with time zone NOT NULL DEFAULT now(),

    CONSTRAINT seats_pkey PRIMARY KEY (seat_id),

    CONSTRAINT uq_seats_zone_row_number UNIQUE (seat_zone_id, row_label, seat_number)

);

CREATE INDEX IF NOT EXISTS ix_seats_seat_zone_id ON public.seats(seat_zone_id);

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

-- ------------------------------------------------------------------------------
-- seating_templates: reusable venue layouts (UC_26.2 Apply Seating Template /
-- UC_26.3 Save Seating Chart as Template). Added 21/09/2026 — fixes the gap where
-- the SRS already documented these use cases but no table/code backed them.
--
-- Deliberately NOT linked to ticket_types: a template is reused across concerts
-- whose ticket types differ each time, so only zone geometry/rows/capacity is
-- stored here. The ticket type binding is chosen again at apply-time.
-- ------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.seating_templates
(
    seating_template_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    organizer_id integer NOT NULL,                 -- soft ref -> identity_db.users; owner of the template
    name character varying(150) COLLATE pg_catalog."default" NOT NULL,
    description character varying(500) COLLATE pg_catalog."default",
    seating_mode character varying(30) COLLATE pg_catalog."default" NOT NULL DEFAULT 'ReservedSeating'::character varying,
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

ALTER TABLE public.seating_templates ADD COLUMN IF NOT EXISTS seating_mode character varying(30) NOT NULL DEFAULT 'ReservedSeating';

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

CREATE INDEX IF NOT EXISTS ix_seating_templates_organizer_id ON public.seating_templates(organizer_id);
CREATE INDEX IF NOT EXISTS ix_seating_templates_is_public ON public.seating_templates(is_public);

-- ===== Foreign keys inside event_db =====

ALTER TABLE IF EXISTS public.event_images

    DROP CONSTRAINT IF EXISTS fk_event_images_event;

ALTER TABLE IF EXISTS public.event_images

    ADD CONSTRAINT fk_event_images_event FOREIGN KEY (event_id)

    REFERENCES public.events (event_id) MATCH SIMPLE

    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.ticket_types

    DROP CONSTRAINT IF EXISTS fk_ticket_types_event;

ALTER TABLE IF EXISTS public.ticket_types

    ADD CONSTRAINT fk_ticket_types_event FOREIGN KEY (event_id)

    REFERENCES public.events (event_id) MATCH SIMPLE

    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.pricing_rules

    DROP CONSTRAINT IF EXISTS fk_pricing_rules_ticket_type;

ALTER TABLE IF EXISTS public.pricing_rules

    ADD CONSTRAINT fk_pricing_rules_ticket_type FOREIGN KEY (ticket_type_id)

    REFERENCES public.ticket_types (ticket_type_id) MATCH SIMPLE

    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.refund_policies

    DROP CONSTRAINT IF EXISTS fk_refund_policies_event;

ALTER TABLE IF EXISTS public.refund_policies

    ADD CONSTRAINT fk_refund_policies_event FOREIGN KEY (event_id)

    REFERENCES public.events (event_id) MATCH SIMPLE

    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.seat_maps

    DROP CONSTRAINT IF EXISTS fk_seat_maps_event;

ALTER TABLE IF EXISTS public.seat_maps

    ADD CONSTRAINT fk_seat_maps_event FOREIGN KEY (event_id)

    REFERENCES public.events (event_id) MATCH SIMPLE

    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.seat_zones

    DROP CONSTRAINT IF EXISTS fk_seat_zones_seat_map;

ALTER TABLE IF EXISTS public.seat_zones

    ADD CONSTRAINT fk_seat_zones_seat_map FOREIGN KEY (seat_map_id)

    REFERENCES public.seat_maps (seat_map_id) MATCH SIMPLE

    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.seat_zones

    DROP CONSTRAINT IF EXISTS fk_seat_zones_ticket_type;

ALTER TABLE IF EXISTS public.seat_zones

    ADD CONSTRAINT fk_seat_zones_ticket_type FOREIGN KEY (ticket_type_id)

    REFERENCES public.ticket_types (ticket_type_id) MATCH SIMPLE

    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.seats

    DROP CONSTRAINT IF EXISTS fk_seats_seat_zone;

ALTER TABLE IF EXISTS public.seats

    ADD CONSTRAINT fk_seats_seat_zone FOREIGN KEY (seat_zone_id)

    REFERENCES public.seat_zones (seat_zone_id) MATCH SIMPLE

    ON UPDATE NO ACTION ON DELETE CASCADE;

ALTER TABLE IF EXISTS public.wishlists

    DROP CONSTRAINT IF EXISTS fk_wishlists_event;

ALTER TABLE IF EXISTS public.wishlists

    ADD CONSTRAINT fk_wishlists_event FOREIGN KEY (event_id)

    REFERENCES public.events (event_id) MATCH SIMPLE

    ON UPDATE NO ACTION ON DELETE NO ACTION;

-- ===== Soft references (no cross-database FK) =====

-- events.organizer_id / created_by / updated_by / reviewed_by / deleted_by -> identity_db.users

-- event_images.deleted_by, ticket_types.deleted_by, seat_maps.deleted_by -> identity_db.users

-- wishlists.user_id -> identity_db.users

-- ============================================================

-- ============================================================
-- RESET SEED DATA ONLY
-- Does NOT drop, recreate, or alter table columns.
-- This makes the script safe to run again without duplicate slugs.
-- ============================================================
DELETE FROM public.wishlists;
DELETE FROM public.seat_zones;
DELETE FROM public.seat_maps;
DELETE FROM public.refund_policies;
DELETE FROM public.pricing_rules;
DELETE FROM public.ticket_types;
DELETE FROM public.event_images;
DELETE FROM public.events;

ALTER TABLE public.wishlists ALTER COLUMN wishlist_id RESTART WITH 1;
ALTER TABLE public.seat_zones ALTER COLUMN seat_zone_id RESTART WITH 1;
ALTER TABLE public.seat_maps ALTER COLUMN seat_map_id RESTART WITH 1;
ALTER TABLE public.refund_policies ALTER COLUMN refund_policy_id RESTART WITH 1;
ALTER TABLE public.pricing_rules ALTER COLUMN pricing_rule_id RESTART WITH 1;
ALTER TABLE public.ticket_types ALTER COLUMN ticket_type_id RESTART WITH 1;
ALTER TABLE public.event_images ALTER COLUMN image_id RESTART WITH 1;
ALTER TABLE public.events ALTER COLUMN event_id RESTART WITH 1;


-- PART 2: SEED DATA
-- Source: EventDB (1).sql
-- Cloudinary base:
-- https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/
--
-- Ticket-sale test groups (reference date: 2026-09-16):
--   Events 1-6   : SALE ENDED
--   Events 7-12  : ON SALE
--   Events 13-18 : COMING SOON
-- UI labels should be English; concert title/description may remain Vietnamese.
-- ============================================================

INSERT INTO public.events
(organizer_id, category_id, title, slug, short_description, description,
 poster_url, poster_public_id, banner_url, banner_public_id,
 location_name, address, city, longitude, latitude,
 starts_at, ends_at, timezone, has_seating_chart, requires_virtual_queue,
 status, rejected_reason, is_featured, view_count, total_tickets, sold_tickets,
 min_tickets_per_account, max_tickets_per_account,
 meta_title, meta_description, created_at, updated_at, published_at,
 created_by, updated_by, is_deleted, deleted_at, deleted_by)
VALUES
(1, 1, 'Anh Trai Say Hi - Mùa 1 Grand Finale', 'atsh-mua1-finale', 'Đêm chung kết mùa đầu tiên', 'Đêm chung kết bùng nổ',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atsh-s1-finale-poster.jpg', 'events/atsh-s1-finale-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atsh-s1-finale-banner.jpg', 'events/atsh-s1-finale-banner',
 'Nhà thi đấu Phú Thọ', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-09-30 19:00:00+07', '2026-09-30 22:30:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 7000, 0,
 1, 4, 'Anh Trai Say Hi - Mùa 1 Grand Finale', 'Đêm chung kết mùa đầu tiên', NOW(), NOW(), '2026-09-01 00:00:00+07',
 1, 1, false, NULL, NULL),
(1, 1, 'Anh Trai Vượt Ngàn Chông Gai - Mùa 1 Finale', 'atvncg-mua1-finale', 'Đêm chung kết bùng nổ với 16 anh trai', 'Show diễn hoành tráng',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atvncg-s1-finale-poster.jpg', 'events/atvncg-s1-finale-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atvncg-s1-finale-banner.jpg', 'events/atvncg-s1-finale-banner',
 'Nhà thi đấu Phú Thọ', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-10-05 19:00:00+07', '2026-10-05 22:30:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 8000, 0,
 1, 4, 'Anh Trai Vượt Ngàn Chông Gai - Mùa 1 Finale', 'Đêm chung kết bùng nổ với 16 anh trai', NOW(), NOW(), '2026-09-01 00:00:00+07',
 1, 1, false, NULL, NULL),
(5, 1, 'Jack - "J97" Live Show', 'jack-j97-liveshow', 'Show diễn đặc biệt', 'Live show độc quyền',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/jack-j97-poster.jpg', 'events/jack-j97-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/jack-j97-banner.jpg', 'events/jack-j97-banner',
 'Nhà hát Hòa Bình', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-10-10 19:00:00+07', '2026-10-10 22:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 4500, 0,
 1, 6, 'Jack - "J97" Live Show', 'Show diễn đặc biệt', NOW(), NOW(), '2026-09-01 00:00:00+07',
 5, 5, false, NULL, NULL),
(3, 1, 'Đen Vâu - Tour "Đi Đâu Cho Thiếp Theo Cùng"', 'denvau-tour-2026', 'Tour diễn acoustic đặc biệt', 'Acoustic tour',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/denvau-tour-2026-poster.jpg', 'events/denvau-tour-2026-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/denvau-tour-2026-banner.jpg', 'events/denvau-tour-2026-banner',
 'Nhà hát Lớn Hà Nội', NULL, 'Hà Nội', NULL, NULL,
 '2026-10-15 20:00:00+07', '2026-10-15 22:30:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 1500, 0,
 1, 4, 'Đen Vâu - Tour "Đi Đâu Cho Thiếp Theo Cùng"', 'Tour diễn acoustic đặc biệt', NOW(), NOW(), '2026-09-01 00:00:00+07',
 3, 3, false, NULL, NULL),
(6, 1, 'Mono - "Waiting For You" Concert', 'mono-waitingforyou', 'Concert debut', 'Concert đầu tiên',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/mono-waitingforyou-poster.jpg', 'events/mono-waitingforyou-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/mono-waitingforyou-banner.jpg', 'events/mono-waitingforyou-banner',
 'Nhà hát Lớn Hà Nội', NULL, 'Hà Nội', NULL, NULL,
 '2026-10-20 20:00:00+07', '2026-10-20 22:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, false, 0, 1200, 0,
 1, 4, 'Mono - "Waiting For You" Concert', 'Concert debut', NOW(), NOW(), '2026-09-01 00:00:00+07',
 6, 6, false, NULL, NULL),
(1, 1, 'Anh Trai Vượt Ngàn Chông Gai - Concert Tour Hà Nội', 'atvncg-mua1-hanoi', 'Tour diễn đầu tiên tại Hà Nội', 'Concert tại Mỹ Đình',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atvncg-s1-hanoi-poster.jpg', 'events/atvncg-s1-hanoi-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atvncg-s1-hanoi-banner.jpg', 'events/atvncg-s1-hanoi-banner',
 'Cung Thể thao Điền kinh Mỹ Đình', NULL, 'Hà Nội', NULL, NULL,
 '2026-10-25 19:30:00+07', '2026-10-25 22:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 10000, 0,
 1, 4, 'Anh Trai Vượt Ngàn Chông Gai - Concert Tour Hà Nội', 'Tour diễn đầu tiên tại Hà Nội', NOW(), NOW(), '2026-09-01 00:00:00+07',
 1, 1, false, NULL, NULL),
(2, 1, 'Em Xinh Say Hi - Mùa 1 Finale', 'exsh-mua1-finale', 'Đêm chung kết các em xinh', 'Finale hoành tráng',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/exsh-s1-finale-poster.jpg', 'events/exsh-s1-finale-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/exsh-s1-finale-banner.jpg', 'events/exsh-s1-finale-banner',
 'Nhà thi đấu Phú Thọ', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-10-30 19:00:00+07', '2026-10-30 22:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 6000, 0,
 1, 4, 'Em Xinh Say Hi - Mùa 1 Finale', 'Đêm chung kết các em xinh', NOW(), NOW(), '2026-09-01 00:00:00+07',
 2, 2, false, NULL, NULL),
(4, 1, 'Noo Phước Thịnh - "Dreamer" Concert', 'noophuocthinhdreamer', 'Concert concept mới', 'Concept độc đáo',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/noophuocthinhdreamer-poster.jpg', 'events/noophuocthinhdreamer-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/noophuocthinhdreamer-banner.jpg', 'events/noophuocthinhdreamer-banner',
 'Nhà thi đấu Phú Thọ', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-11-05 19:30:00+07', '2026-11-05 22:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, false, 0, 5000, 0,
 1, 4, 'Noo Phước Thịnh - "Dreamer" Concert', 'Concert concept mới', NOW(), NOW(), '2026-09-01 00:00:00+07',
 4, 4, false, NULL, NULL),
(3, 1, 'Hòa Minzy - "Yêu" Concert', 'hoaminzy-yeu-concert', 'Concert solo đầu tiên', 'Solo concert',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/hoaminzy-yeu-poster.jpg', 'events/hoaminzy-yeu-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/hoaminzy-yeu-banner.jpg', 'events/hoaminzy-yeu-banner',
 'Nhà thi đấu Phú Thọ', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-11-10 19:30:00+07', '2026-11-10 22:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 6000, 0,
 1, 4, 'Hòa Minzy - "Yêu" Concert', 'Concert solo đầu tiên', NOW(), NOW(), '2026-09-01 00:00:00+07',
 3, 3, false, NULL, NULL),
(2, 1, 'Em Xinh Say Hi - Concert Tour Đà Nẵng', 'exsh-mua1-danang', 'Tour diễn tại Đà Nẵng', 'Tour Đà Nẵng',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/exsh-s1-danang-poster.jpg', 'events/exsh-s1-danang-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/exsh-s1-danang-banner.jpg', 'events/exsh-s1-danang-banner',
 'Cung Thể thao Tiên Sơn', NULL, 'Đà Nẵng', NULL, NULL,
 '2026-11-15 19:30:00+07', '2026-11-15 22:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, false, 0, 4000, 0,
 1, 4, 'Em Xinh Say Hi - Concert Tour Đà Nẵng', 'Tour diễn tại Đà Nẵng', NOW(), NOW(), '2026-09-01 00:00:00+07',
 2, 2, false, NULL, NULL),
(4, 1, 'Đàm Vĩnh Hưng - "Dạ Khúc Tình Yêu"', 'damvinhung-dakhuc', 'Live show đặc biệt', 'Dạ khúc tình yêu',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/damvinhung-dakhuc-poster.jpg', 'events/damvinhung-dakhuc-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/damvinhung-dakhuc-banner.jpg', 'events/damvinhung-dakhuc-banner',
 'Cung Văn hóa Hữu nghị', NULL, 'Hà Nội', NULL, NULL,
 '2026-11-20 20:00:00+07', '2026-11-20 23:30:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 3000, 0,
 1, 4, 'Đàm Vĩnh Hưng - "Dạ Khúc Tình Yêu"', 'Live show đặc biệt', NOW(), NOW(), '2026-09-01 00:00:00+07',
 4, 4, false, NULL, NULL),
(3, 1, 'Sơn Tùng M-TP - Sky Tour 2026', 'sontung-skytour-2026', 'Tour diễn lớn nhất năm', 'Sky Tour hoành tráng',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/sontung-skytour-2026-poster.jpg', 'events/sontung-skytour-2026-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/sontung-skytour-2026-banner.jpg', 'events/sontung-skytour-2026-banner',
 'Sân vận động Quốc gia Mỹ Đình', NULL, 'Hà Nội', NULL, NULL,
 '2026-11-25 19:00:00+07', '2026-11-25 23:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 40000, 0,
 1, 2, 'Sơn Tùng M-TP - Sky Tour 2026', 'Tour diễn lớn nhất năm', NOW(), NOW(), '2026-09-01 00:00:00+07',
 3, 3, false, NULL, NULL),
(4, 1, 'Mỹ Tâm - "Họa Mi Tóc Nâu" Tour', 'mytam-hoamitocnau', 'Tour diễn kỷ niệm 20 năm', 'Kỷ niệm 20 năm',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/mytam-hoamitocnau-poster.jpg', 'events/mytam-hoamitocnau-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/mytam-hoamitocnau-banner.jpg', 'events/mytam-hoamitocnau-banner',
 'Sân vận động Thống Nhất', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-11-30 19:00:00+07', '2026-11-30 22:30:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 25000, 0,
 1, 4, 'Mỹ Tâm - "Họa Mi Tóc Nâu" Tour', 'Tour diễn kỷ niệm 20 năm', NOW(), NOW(), '2026-09-01 00:00:00+07',
 4, 4, false, NULL, NULL),
(3, 1, 'Bích Phương - "Bùa Yêu" Tour 2027', 'bichphuong-buayeou-tour', 'Tour diễn xuyên Việt', 'Xuyên Việt',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/bichphuong-buayeou-poster.jpg', 'events/bichphuong-buayeou-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/bichphuong-buayeou-banner.jpg', 'events/bichphuong-buayeou-banner',
 'Nhà hát Hòa Bình', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-12-05 19:30:00+07', '2026-12-05 22:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 4000, 0,
 1, 4, 'Bích Phương - "Bùa Yêu" Tour 2027', 'Tour diễn xuyên Việt', NOW(), NOW(), '2026-09-01 00:00:00+07',
 3, 3, false, NULL, NULL),
(1, 1, 'Anh Trai Say Hi - Mùa 2 Kickoff', 'atsh-mua2-kickoff', 'Ra mắt mùa 2 với dàn cast mới', 'Mùa 2 kickoff',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atsh-s2-kickoff-poster.jpg', 'events/atsh-s2-kickoff-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atsh-s2-kickoff-banner.jpg', 'events/atsh-s2-kickoff-banner',
 'Nhà hát Hòa Bình', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-12-10 19:00:00+07', '2026-12-10 22:30:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 5000, 0,
 1, 4, 'Anh Trai Say Hi - Mùa 2 Kickoff', 'Ra mắt mùa 2 với dàn cast mới', NOW(), NOW(), '2026-09-01 00:00:00+07',
 1, 1, false, NULL, NULL),
(5, 1, 'AMEE - "E11EVEN" Tour 2027', 'amee-e11even-tour', 'Tour diễn thế hệ mới', 'Gen Z tour',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/amee-e11even-poster.jpg', 'events/amee-e11even-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/amee-e11even-banner.jpg', 'events/amee-e11even-banner',
 'Cung Thể thao Tiên Sơn', NULL, 'Đà Nẵng', NULL, NULL,
 '2026-12-15 19:30:00+07', '2026-12-15 22:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, false, 0, 3500, 0,
 1, 4, 'AMEE - "E11EVEN" Tour 2027', 'Tour diễn thế hệ mới', NOW(), NOW(), '2026-09-01 00:00:00+07',
 5, 5, false, NULL, NULL),
(5, 1, 'Hoàng Thùy Linh - "See Tình" World Tour VN', 'hoangthuylinh-seetinh', 'Tour diễn quốc tế tại Việt Nam', 'World Tour',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/hoangthuylinh-seetinh-poster.jpg', 'events/hoangthuylinh-seetinh-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/hoangthuylinh-seetinh-banner.jpg', 'events/hoangthuylinh-seetinh-banner',
 'Sân vận động Thống Nhất', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-12-20 19:30:00+07', '2026-12-20 22:30:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 20000, 0,
 1, 4, 'Hoàng Thùy Linh - "See Tình" World Tour VN', 'Tour diễn quốc tế tại Việt Nam', NOW(), NOW(), '2026-09-01 00:00:00+07',
 5, 5, false, NULL, NULL),
(6, 1, 'Wren Evans - "Call Me" Tour', 'wrevans-callme-tour', 'Tour diễn Gen Z', 'Call Me Tour',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/wrevans-callme-poster.jpg', 'events/wrevans-callme-poster', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/wrevans-callme-banner.jpg', 'events/wrevans-callme-banner',
 'Nhà thi đấu Phú Thọ', NULL, 'Hồ Chí Minh', NULL, NULL,
 '2026-12-28 19:30:00+07', '2026-12-28 22:00:00+07', 'SE Asia Standard Time', false, false,
 'Approved', NULL, true, 0, 5500, 0,
 1, 4, 'Wren Evans - "Call Me" Tour', 'Tour diễn Gen Z', NOW(), NOW(), '2026-09-01 00:00:00+07',
 6, 6, false, NULL, NULL);

-- Seeded public events have already completed the approval lifecycle.
UPDATE public.events
SET submitted_at = COALESCE(submitted_at, published_at),
    approved_at = COALESCE(approved_at, published_at),
    reviewed_by = COALESCE(reviewed_by, 1)
WHERE status = 'Approved';

-- Event image gallery: detail, poster and banner.
INSERT INTO public.event_images (event_id, image_url, public_id, sort_order, is_main)
SELECT e.event_id, x.image_url, x.public_id, x.sort_order, x.is_main
FROM public.events e
JOIN (VALUES
('atsh-mua1-finale', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atsh-s1-finale-detail.jpg', 'events/atsh-s1-finale-detail', 1, true),
('atsh-mua1-finale', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atsh-s1-finale-poster.jpg', 'events/atsh-s1-finale-poster', 2, false),
('atsh-mua1-finale', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atsh-s1-finale-banner.jpg', 'events/atsh-s1-finale-banner', 3, false),
('atvncg-mua1-finale', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atvncg-s1-finale-detail.jpg', 'events/atvncg-s1-finale-detail', 1, true),
('atvncg-mua1-finale', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atvncg-s1-finale-poster.jpg', 'events/atvncg-s1-finale-poster', 2, false),
('atvncg-mua1-finale', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atvncg-s1-finale-banner.jpg', 'events/atvncg-s1-finale-banner', 3, false),
('jack-j97-liveshow', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/jack-j97-detail.jpg', 'events/jack-j97-detail', 1, true),
('jack-j97-liveshow', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/jack-j97-poster.jpg', 'events/jack-j97-poster', 2, false),
('jack-j97-liveshow', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/jack-j97-banner.jpg', 'events/jack-j97-banner', 3, false),
('denvau-tour-2026', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/denvau-tour-detail.jpg', 'events/denvau-tour-detail', 1, true),
('denvau-tour-2026', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/denvau-tour-2026-poster.jpg', 'events/denvau-tour-2026-poster', 2, false),
('denvau-tour-2026', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/denvau-tour-2026-banner.jpg', 'events/denvau-tour-2026-banner', 3, false),
('mono-waitingforyou', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/mono-waitingforyou-detail.jpg', 'events/mono-waitingforyou-detail', 1, true),
('mono-waitingforyou', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/mono-waitingforyou-poster.jpg', 'events/mono-waitingforyou-poster', 2, false),
('mono-waitingforyou', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/mono-waitingforyou-banner.jpg', 'events/mono-waitingforyou-banner', 3, false),
('atvncg-mua1-hanoi', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atvncg-hanoi-detail.jpg', 'events/atvncg-hanoi-detail', 1, true),
('atvncg-mua1-hanoi', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atvncg-s1-hanoi-poster.jpg', 'events/atvncg-s1-hanoi-poster', 2, false),
('atvncg-mua1-hanoi', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atvncg-s1-hanoi-banner.jpg', 'events/atvncg-s1-hanoi-banner', 3, false),
('exsh-mua1-finale', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/exsh-s1-finale-detail.jpg', 'events/exsh-s1-finale-detail', 1, true),
('exsh-mua1-finale', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/exsh-s1-finale-poster.jpg', 'events/exsh-s1-finale-poster', 2, false),
('exsh-mua1-finale', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/exsh-s1-finale-banner.jpg', 'events/exsh-s1-finale-banner', 3, false),
('noophuocthinhdreamer', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/noophuocthinhdreamer-detail.jpg', 'events/noophuocthinhdreamer-detail', 1, true),
('noophuocthinhdreamer', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/noophuocthinhdreamer-poster.jpg', 'events/noophuocthinhdreamer-poster', 2, false),
('noophuocthinhdreamer', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/noophuocthinhdreamer-banner.jpg', 'events/noophuocthinhdreamer-banner', 3, false),
('hoaminzy-yeu-concert', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/hoaminzy-yeu-detail.jpg', 'events/hoaminzy-yeu-detail', 1, true),
('hoaminzy-yeu-concert', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/hoaminzy-yeu-poster.jpg', 'events/hoaminzy-yeu-poster', 2, false),
('hoaminzy-yeu-concert', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/hoaminzy-yeu-banner.jpg', 'events/hoaminzy-yeu-banner', 3, false),
('exsh-mua1-danang', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/exsh-s1-danang-detail.jpg', 'events/exsh-s1-danang-detail', 1, true),
('exsh-mua1-danang', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/exsh-s1-danang-poster.jpg', 'events/exsh-s1-danang-poster', 2, false),
('exsh-mua1-danang', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/exsh-s1-danang-banner.jpg', 'events/exsh-s1-danang-banner', 3, false),
('damvinhung-dakhuc', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/damvinhung-dakhuc-detail.jpg', 'events/damvinhung-dakhuc-detail', 1, true),
('damvinhung-dakhuc', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/damvinhung-dakhuc-poster.jpg', 'events/damvinhung-dakhuc-poster', 2, false),
('damvinhung-dakhuc', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/damvinhung-dakhuc-banner.jpg', 'events/damvinhung-dakhuc-banner', 3, false),
('sontung-skytour-2026', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/sontung-skytour-detail.jpg', 'events/sontung-skytour-detail', 1, true),
('sontung-skytour-2026', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/sontung-skytour-2026-poster.jpg', 'events/sontung-skytour-2026-poster', 2, false),
('sontung-skytour-2026', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/sontung-skytour-2026-banner.jpg', 'events/sontung-skytour-2026-banner', 3, false),
('mytam-hoamitocnau', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/mytam-hoamitocnau-detail.jpg', 'events/mytam-hoamitocnau-detail', 1, true),
('mytam-hoamitocnau', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/mytam-hoamitocnau-poster.jpg', 'events/mytam-hoamitocnau-poster', 2, false),
('mytam-hoamitocnau', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/mytam-hoamitocnau-banner.jpg', 'events/mytam-hoamitocnau-banner', 3, false),
('bichphuong-buayeou-tour', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/bichphuong-buayeou-detail.jpg', 'events/bichphuong-buayeou-detail', 1, true),
('bichphuong-buayeou-tour', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/bichphuong-buayeou-poster.jpg', 'events/bichphuong-buayeou-poster', 2, false),
('bichphuong-buayeou-tour', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/bichphuong-buayeou-banner.jpg', 'events/bichphuong-buayeou-banner', 3, false),
('atsh-mua2-kickoff', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atsh-s2-kickoff-detail.jpg', 'events/atsh-s2-kickoff-detail', 1, true),
('atsh-mua2-kickoff', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atsh-s2-kickoff-poster.jpg', 'events/atsh-s2-kickoff-poster', 2, false),
('atsh-mua2-kickoff', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/atsh-s2-kickoff-banner.jpg', 'events/atsh-s2-kickoff-banner', 3, false),
('amee-e11even-tour', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/amee-e11even-detail.jpg', 'events/amee-e11even-detail', 1, true),
('amee-e11even-tour', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/amee-e11even-poster.jpg', 'events/amee-e11even-poster', 2, false),
('amee-e11even-tour', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/amee-e11even-banner.jpg', 'events/amee-e11even-banner', 3, false),
('hoangthuylinh-seetinh', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/hoangthuylinh-seetinh-detail.jpg', 'events/hoangthuylinh-seetinh-detail', 1, true),
('hoangthuylinh-seetinh', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/hoangthuylinh-seetinh-poster.jpg', 'events/hoangthuylinh-seetinh-poster', 2, false),
('hoangthuylinh-seetinh', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/hoangthuylinh-seetinh-banner.jpg', 'events/hoangthuylinh-seetinh-banner', 3, false),
('wrevans-callme-tour', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/wrevans-callme-detail.jpg', 'events/wrevans-callme-detail', 1, true),
('wrevans-callme-tour', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/wrevans-callme-poster.jpg', 'events/wrevans-callme-poster', 2, false),
('wrevans-callme-tour', 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546572/events/wrevans-callme-banner.jpg', 'events/wrevans-callme-banner', 3, false)
) AS x(slug, image_url, public_id, sort_order, is_main)
ON e.slug = x.slug;

-- Ticket types.
-- Sale state is derived by the API/UI from sales_starts_at / sales_ends_at.
INSERT INTO public.ticket_types
(event_id, type_name, description, price, original_price, quantity, sold_quantity,
 min_per_order, max_per_order, color_code, sort_order, status,
 sales_starts_at, sales_ends_at)
SELECT e.event_id, t.type_name, t.description, t.price, t.original_price, t.quantity,
       t.sold_quantity, t.min_per_order, t.max_per_order, t.color_code, t.sort_order,
       t.status, t.sales_starts_at::timestamptz, t.sales_ends_at::timestamptz
FROM public.events e
JOIN (VALUES
('atsh-mua1-finale', 'VIP', 'Ghế VIP + Backstage pass', 3000000, 3500000, 150, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-09-30 18:00:00+07'),
('atsh-mua1-finale', 'Gold', 'Khu vực vàng', 1200000, 1500000, 700, 0, 1, 6, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-09-30 18:00:00+07'),
('atsh-mua1-finale', 'Silver', 'Khu vực bạc', 600000, 800000, 2000, 0, 1, 10, NULL, 3, 'Active', '2026-09-30 00:00:00+07', '2026-09-30 18:00:00+07'),
('atsh-mua1-finale', 'Thường', 'Khu vực thường', 350000, 450000, 4150, 0, 1, 10, NULL, 4, 'Active', '2026-09-30 00:00:00+07', '2026-09-30 18:00:00+07'),
('atvncg-mua1-finale', 'VIP Meet & Greet', 'Gặp gỡ 16 anh trai + Quà tặng', 3500000, 4000000, 200, 0, 1, 10, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-10-05 18:00:00+07'),
('atvncg-mua1-finale', 'Gold', 'Khu vực vàng - Hàng ghế đầu', 1500000, 1800000, 800, 0, 1, 10, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-10-05 18:00:00+07'),
('atvncg-mua1-finale', 'Silver', 'Khu vực bạc', 800000, 1000000, 2000, 0, 1, 10, NULL, 3, 'Active', '2026-09-30 00:00:00+07', '2026-10-05 18:00:00+07'),
('atvncg-mua1-finale', 'Thường', 'Khu vực thường', 400000, 500000, 5000, 0, 1, 10, NULL, 4, 'Active', '2026-09-30 00:00:00+07', '2026-10-05 18:00:00+07'),
('jack-j97-liveshow', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 6, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-10-10 18:00:00+07'),
('jack-j97-liveshow', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 6, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-10-10 18:00:00+07'),
('denvau-tour-2026', 'VIP', NULL, 2000000, NULL, 100, 0, 1, 10, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-10-15 19:00:00+07'),
('denvau-tour-2026', 'Thường', NULL, 800000, NULL, 1400, 0, 1, 10, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-10-15 19:00:00+07'),
('mono-waitingforyou', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-10-20 19:00:00+07'),
('mono-waitingforyou', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-10-20 19:00:00+07'),
('atvncg-mua1-hanoi', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-10-25 18:30:00+07'),
('atvncg-mua1-hanoi', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-10-25 18:30:00+07'),
('exsh-mua1-finale', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-10-30 18:00:00+07'),
('exsh-mua1-finale', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-10-30 18:00:00+07'),
('noophuocthinhdreamer', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-11-05 18:30:00+07'),
('noophuocthinhdreamer', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-11-05 18:30:00+07'),
('hoaminzy-yeu-concert', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-11-10 18:30:00+07'),
('hoaminzy-yeu-concert', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-11-10 18:30:00+07'),
('exsh-mua1-danang', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-11-15 18:30:00+07'),
('exsh-mua1-danang', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-11-15 18:30:00+07'),
('damvinhung-dakhuc', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-11-20 19:00:00+07'),
('damvinhung-dakhuc', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-11-20 19:00:00+07'),
('sontung-skytour-2026', 'VIP Platinum', 'Hàng ghế đầu + Meet & Greet + Merchandise', 5000000, 6000000, 500, 0, 1, 2, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-11-25 18:00:00+07'),
('sontung-skytour-2026', 'Gold', 'Khu vực vàng A', 2500000, 3000000, 2000, 0, 1, 2, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-11-25 18:00:00+07'),
('sontung-skytour-2026', 'Silver', 'Khu vực vàng B', 1500000, 1800000, 5000, 0, 1, 2, NULL, 3, 'Active', '2026-09-30 00:00:00+07', '2026-11-25 18:00:00+07'),
('sontung-skytour-2026', 'Thường', 'Khu vực thường', 800000, 1000000, 32500, 0, 1, 2, NULL, 4, 'Active', '2026-09-30 00:00:00+07', '2026-11-25 18:00:00+07'),
('mytam-hoamitocnau', 'VIP Diamond', 'Gặp Mỹ Tâm + Chụp hình', 4000000, NULL, 300, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-11-30 18:00:00+07'),
('mytam-hoamitocnau', 'Gold', 'Khu vực vàng', 2000000, NULL, 3000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-11-30 18:00:00+07'),
('mytam-hoamitocnau', 'Silver', 'Khu vực bạc', 1000000, NULL, 8000, 0, 1, 4, NULL, 3, 'Active', '2026-09-30 00:00:00+07', '2026-11-30 18:00:00+07'),
('mytam-hoamitocnau', 'Thường', 'Khu vực thường', 500000, NULL, 13700, 0, 1, 4, NULL, 4, 'Active', '2026-09-30 00:00:00+07', '2026-11-30 18:00:00+07'),
('bichphuong-buayeou-tour', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-12-05 18:30:00+07'),
('bichphuong-buayeou-tour', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-12-05 18:30:00+07'),
('atsh-mua2-kickoff', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-12-10 18:00:00+07'),
('atsh-mua2-kickoff', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-12-10 18:00:00+07'),
('amee-e11even-tour', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-12-15 18:30:00+07'),
('amee-e11even-tour', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-12-15 18:30:00+07'),
('hoangthuylinh-seetinh', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-12-20 18:30:00+07'),
('hoangthuylinh-seetinh', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-12-20 18:30:00+07'),
('wrevans-callme-tour', 'VIP', NULL, 1500000, NULL, 500, 0, 1, 4, NULL, 1, 'Active', '2026-09-30 00:00:00+07', '2026-12-28 18:30:00+07'),
('wrevans-callme-tour', 'Thường', NULL, 500000, NULL, 2000, 0, 1, 4, NULL, 2, 'Active', '2026-09-30 00:00:00+07', '2026-12-28 18:30:00+07')
) AS t(slug, type_name, description, price, original_price, quantity, sold_quantity,
       min_per_order, max_per_order, color_code, sort_order, status, sales_starts_at, sales_ends_at)
ON e.slug = t.slug;

-- Default refund policy for demo/testing.
INSERT INTO public.refund_policies
(event_id, policy_name, description, deadline_before_event_hours, refund_percent,
 requires_organizer_approval, is_active)
SELECT event_id,
       'Standard Refund Policy',
       'Refund requests must be submitted at least 72 hours before the event.',
       72, 80.00, true, true
FROM public.events;

-- Sample pricing rule for VIP-class tickets.
INSERT INTO public.pricing_rules
(ticket_type_id, rule_name, rule_type, adjusted_price, discount_percent,
 trigger_from, trigger_to, quantity_threshold, priority, is_active)
SELECT ticket_type_id,
       'Early Bird',
       'TimeBased',
       NULL,
       10.00,
       sales_starts_at,
       sales_starts_at + interval '7 days',
       NULL,
       1,
       true
FROM public.ticket_types
WHERE type_name ILIKE 'VIP%';

-- Keep event totals synchronized with seeded ticket quantities where ticket data exists.
UPDATE public.events e
SET total_tickets = x.total_quantity,
    updated_at = NOW()
FROM (
    SELECT event_id, SUM(quantity)::integer AS total_quantity
    FROM public.ticket_types
    GROUP BY event_id
) x
WHERE e.event_id = x.event_id;

-- ============================================================
-- QUICK TEST QUERIES
-- ============================================================
-- 1) Verify Cloudinary URLs
-- SELECT event_id, title, poster_url, banner_url FROM public.events ORDER BY event_id;
--
-- 2) Verify sale states (reference logic used by UI/API)
-- SELECT e.title, tt.type_name, tt.sales_starts_at, tt.sales_ends_at,
--        CASE
--          WHEN NOW() < tt.sales_starts_at THEN 'Coming Soon'
--          WHEN NOW() BETWEEN tt.sales_starts_at AND tt.sales_ends_at THEN 'On Sale'
--          ELSE 'Sale Ended'
--        END AS sale_state
-- FROM public.ticket_types tt
-- JOIN public.events e ON e.event_id = tt.event_id
-- ORDER BY e.event_id, tt.sort_order;

COMMIT;

-- =============================================================================
-- WEEK TASKS: EventAPI owns organizer staff assignment in the current codebase.
-- This schema intentionally matches EventAPI.Models.EventStaff exactly.
-- =============================================================================
CREATE TABLE IF NOT EXISTS public.event_staff (
    event_staff_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id integer NOT NULL REFERENCES public.events(event_id) ON DELETE CASCADE,
    staff_user_id integer NOT NULL,
    gate_name varchar(100) NOT NULL DEFAULT 'Main Gate',
    assigned_by integer NOT NULL,
    assigned_at timestamp with time zone NOT NULL DEFAULT now(),
    is_active boolean NOT NULL DEFAULT true,
    unassigned_by integer NULL,
    unassigned_at timestamp with time zone NULL
);
ALTER TABLE public.event_staff ADD COLUMN IF NOT EXISTS gate_name varchar(100) NOT NULL DEFAULT 'Main Gate';

CREATE UNIQUE INDEX IF NOT EXISTS uq_event_staff_active
ON public.event_staff(event_id, staff_user_id) WHERE is_active = true;
CREATE INDEX IF NOT EXISTS ix_event_staff_staff_user
ON public.event_staff(staff_user_id) WHERE is_active = true;

-- Demo assignment for organizer user 3's event 4. Staff IDs are resolved by auth seed
-- in normal fresh DB order (4,5,6); soft references are intentional across databases.
INSERT INTO public.event_staff(event_id, staff_user_id, assigned_by, assigned_at, is_active)
SELECT 4, 4, 3, now() - interval '2 days', true
WHERE EXISTS (SELECT 1 FROM public.events WHERE event_id=4)
  AND NOT EXISTS (SELECT 1 FROM public.event_staff WHERE event_id=4 AND staff_user_id=4 AND is_active=true);
INSERT INTO public.event_staff(event_id, staff_user_id, assigned_by, assigned_at, is_active)
SELECT 4, 5, 3, now() - interval '1 day', true
WHERE EXISTS (SELECT 1 FROM public.events WHERE event_id=4)
  AND NOT EXISTS (SELECT 1 FROM public.event_staff WHERE event_id=4 AND staff_user_id=5 AND is_active=true);


-- =================================================================
-- CONSOLIDATED EVENT MIGRATIONS
-- =================================================================
-- CHẠY TRONG event_db
-- Chạy MỘT LẦN, an toàn khi chạy lại (CREATE/ALTER ... IF NOT EXISTS).


-- ---------- 007 event_staff ----------
-- UC_14.3 / UC_14.4: staff members assigned to an event. Apply once to event_db.
-- Unassigning keeps the row (is_active = false) so the history is not lost.
-- The partial unique index allows a staff member to be assigned again later,
-- but never twice at the same time.
CREATE TABLE IF NOT EXISTS public.event_staff (
    event_staff_id  integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id        integer NOT NULL,
    staff_user_id   integer NOT NULL,
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

-- ---------- 008 governance ----------
-- Chạy trong event_db sau database/event_db.sql. Không mở bán tự động cho dữ liệu legacy.
BEGIN;
ALTER TABLE events ADD COLUMN IF NOT EXISTS compliance_status text NOT NULL DEFAULT 'NotSubmitted';
ALTER TABLE events ADD COLUMN IF NOT EXISTS compliance_version integer NOT NULL DEFAULT 0;
ALTER TABLE events ADD COLUMN IF NOT EXISTS compliance_reviewed_version integer;
ALTER TABLE events ADD COLUMN IF NOT EXISTS schedule_version integer NOT NULL DEFAULT 0;
ALTER TABLE events ADD COLUMN IF NOT EXISTS sales_frozen boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS event_compliance_documents (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 event_id integer NOT NULL REFERENCES events(event_id), version integer NOT NULL CHECK(version > 0),
 document_type text NOT NULL, file_name text NOT NULL, content_type text NOT NULL,
 public_id text NOT NULL, secure_url text NOT NULL, resource_type text NOT NULL DEFAULT 'raw',
 size bigint NOT NULL CHECK(size > 0), submitted_by integer NOT NULL, submitted_at timestamptz NOT NULL,
 UNIQUE(event_id,version,document_type)
);
CREATE TABLE IF NOT EXISTS event_compliance_reviews (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 event_id integer NOT NULL REFERENCES events(event_id), version integer NOT NULL,
 decision text NOT NULL CHECK(decision IN ('Approved','Rejected','RequestMoreInfo')),
 notes text NOT NULL CHECK(length(trim(notes)) BETWEEN 1 AND 2000), reviewed_by integer NOT NULL, reviewed_at timestamptz NOT NULL,
 UNIQUE(event_id,version)
);
CREATE TABLE IF NOT EXISTS event_change_requests (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 event_id integer NOT NULL REFERENCES events(event_id), type text NOT NULL CHECK(type IN ('Postpone','Reschedule')),
 status text NOT NULL DEFAULT 'Pending' CHECK(status IN ('Pending','Approved','Rejected')),
 reason text NOT NULL CHECK(length(trim(reason)) BETWEEN 10 AND 1000),
 old_starts_at timestamptz NOT NULL, old_ends_at timestamptz NOT NULL,
 new_starts_at timestamptz, new_ends_at timestamptz, old_status text NOT NULL,
 event_version integer NOT NULL, submitted_by integer NOT NULL, submitted_at timestamptz NOT NULL,
 reviewed_by integer, reviewed_at timestamptz, review_notes text, processing_status text NOT NULL DEFAULT 'NotApplied',
 CHECK((type='Postpone' AND new_starts_at IS NULL AND new_ends_at IS NULL) OR
       (type='Reschedule' AND new_starts_at IS NOT NULL AND new_ends_at > new_starts_at))
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_event_change_pending ON event_change_requests(event_id) WHERE status='Pending';
CREATE TABLE IF NOT EXISTS governance_outbox (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, kind text NOT NULL, payload text NOT NULL,
 created_at timestamptz NOT NULL, completed_at timestamptz, next_attempt_at timestamptz NOT NULL,
 attempts integer NOT NULL DEFAULT 0, last_error text
);
CREATE INDEX IF NOT EXISTS ix_governance_outbox_pending ON governance_outbox(next_attempt_at,id) WHERE completed_at IS NULL;
COMMIT;

-- ---------- 011 return review (event) ----------
-- Organizer chọn staff nào được duyệt yêu cầu hoàn vé của sự kiện này.
ALTER TABLE public.event_staff
    ADD COLUMN IF NOT EXISTS can_review_returns boolean NOT NULL DEFAULT false;
CREATE INDEX IF NOT EXISTS ix_event_staff_return_reviewers
    ON public.event_staff (staff_user_id) WHERE is_active = true AND can_review_returns = true;
