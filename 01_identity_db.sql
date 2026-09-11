-- ============================================================
-- identity_db  (IdentityAPI)
-- Tables: roles, users, user_roles, refresh_tokens,
--         ekyc_verifications, organizer_requests
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.roles
(
    role_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    role_name character varying(50) COLLATE pg_catalog."default" NOT NULL,
    CONSTRAINT roles_pkey PRIMARY KEY (role_id),
    CONSTRAINT roles_role_name_key UNIQUE (role_name)
);

CREATE TABLE IF NOT EXISTS public.users
(
    user_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    email character varying(256) COLLATE pg_catalog."default" NOT NULL,
    password_hash text COLLATE pg_catalog."default" NOT NULL,
    full_name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    phone_number character varying(20) COLLATE pg_catalog."default",
    avatar_url text COLLATE pg_catalog."default",
    is_verified boolean NOT NULL DEFAULT false,
    otp_hash character varying(256) COLLATE pg_catalog."default",
    otp_expired_at timestamp with time zone,
    ekyc_status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'NotStarted'::character varying,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT users_pkey PRIMARY KEY (user_id),
    CONSTRAINT users_email_key UNIQUE (email)
);

CREATE TABLE IF NOT EXISTS public.user_roles
(
    user_id integer NOT NULL,
    role_id integer NOT NULL,
    assigned_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT user_roles_pkey PRIMARY KEY (user_id, role_id)
);

CREATE TABLE IF NOT EXISTS public.refresh_tokens
(
    refresh_token_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    user_id integer NOT NULL,
    token_hash character varying(64) COLLATE pg_catalog."default" NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    is_revoked boolean NOT NULL DEFAULT false,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT refresh_tokens_pkey PRIMARY KEY (refresh_token_id),
    CONSTRAINT refresh_tokens_token_hash_key UNIQUE (token_hash)
);

CREATE TABLE IF NOT EXISTS public.ekyc_verifications
(
    ekyc_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    user_id integer NOT NULL,
    cccd_number_encrypted text COLLATE pg_catalog."default",
    cccd_front_object_key character varying(500) COLLATE pg_catalog."default",
    cccd_back_object_key character varying(500) COLLATE pg_catalog."default",
    face_capture_object_key character varying(500) COLLATE pg_catalog."default",
    face_vector bytea,
    ocr_raw_data jsonb,
    face_match_score numeric(5, 2),
    liveness_score numeric(5, 2),
    liveness_passed boolean,
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Pending'::character varying,
    fail_reason character varying(500) COLLATE pg_catalog."default",
    verified_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ekyc_verifications_pkey PRIMARY KEY (ekyc_id)
);

CREATE TABLE IF NOT EXISTS public.organizer_requests
(
    request_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    user_id integer NOT NULL,
    reason character varying(1000) COLLATE pg_catalog."default" NOT NULL,
    company_name character varying(200) COLLATE pg_catalog."default",
    website character varying(200) COLLATE pg_catalog."default",
    phone_number character varying(20) COLLATE pg_catalog."default",
    experience character varying(2000) COLLATE pg_catalog."default",
    status character varying(20) COLLATE pg_catalog."default" NOT NULL DEFAULT 'Pending'::character varying,
    reviewed_by integer,
    reviewed_at timestamp with time zone,
    review_note character varying(1000) COLLATE pg_catalog."default",
    rejection_reason character varying(1000) COLLATE pg_catalog."default",
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone NOT NULL DEFAULT now(),
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,
    CONSTRAINT organizer_requests_pkey PRIMARY KEY (request_id)
);

-- ===== FK cùng DB (giữ nguyên) =====

ALTER TABLE IF EXISTS public.user_roles
    ADD CONSTRAINT fk_user_roles_role FOREIGN KEY (role_id)
    REFERENCES public.roles (role_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE CASCADE;

ALTER TABLE IF EXISTS public.user_roles
    ADD CONSTRAINT fk_user_roles_user FOREIGN KEY (user_id)
    REFERENCES public.users (user_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE CASCADE;

ALTER TABLE IF EXISTS public.refresh_tokens
    ADD CONSTRAINT fk_refresh_tokens_user FOREIGN KEY (user_id)
    REFERENCES public.users (user_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.ekyc_verifications
    ADD CONSTRAINT fk_ekyc_user FOREIGN KEY (user_id)
    REFERENCES public.users (user_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.organizer_requests
    ADD CONSTRAINT fk_organizer_requests_deleted_by FOREIGN KEY (deleted_by)
    REFERENCES public.users (user_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.organizer_requests
    ADD CONSTRAINT fk_organizer_requests_reviewed_by FOREIGN KEY (reviewed_by)
    REFERENCES public.users (user_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

ALTER TABLE IF EXISTS public.organizer_requests
    ADD CONSTRAINT fk_organizer_requests_user FOREIGN KEY (user_id)
    REFERENCES public.users (user_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

-- ===== Soft reference (KHÔNG có FK, chỉ giữ cột + index) =====
-- Không có tham chiếu xuyên DB trong identity_db.

END;
