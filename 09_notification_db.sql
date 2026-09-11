-- ============================================================
-- notification_db  (NotificationAPI)
-- Tables: conversations, messages, notifications
-- ============================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.conversations
(
    conversation_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    event_id integer NOT NULL,                          -- soft ref -> event_db.events
    user_id integer NOT NULL,                             -- soft ref -> identity_db.users
    organizer_id integer NOT NULL,                          -- soft ref -> identity_db.users
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT conversations_pkey PRIMARY KEY (conversation_id),
    CONSTRAINT uq_conversations_event_user_organizer UNIQUE (event_id, user_id, organizer_id)
);
CREATE INDEX IF NOT EXISTS ix_conversations_event_id ON public.conversations(event_id);
CREATE INDEX IF NOT EXISTS ix_conversations_organizer_id ON public.conversations(organizer_id);
CREATE INDEX IF NOT EXISTS ix_conversations_user_id ON public.conversations(user_id);

CREATE TABLE IF NOT EXISTS public.messages
(
    message_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    conversation_id integer NOT NULL,
    sender_id integer NOT NULL,                          -- soft ref -> identity_db.users
    content text COLLATE pg_catalog."default" NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    is_read boolean NOT NULL DEFAULT false,
    CONSTRAINT messages_pkey PRIMARY KEY (message_id)
);
CREATE INDEX IF NOT EXISTS ix_messages_conversation_id ON public.messages(conversation_id);

CREATE TABLE IF NOT EXISTS public.notifications
(
    notification_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    user_id integer NOT NULL,                              -- soft ref -> identity_db.users
    title character varying(200) COLLATE pg_catalog."default" NOT NULL,
    content text COLLATE pg_catalog."default" NOT NULL,
    type character varying(50) COLLATE pg_catalog."default" NOT NULL,
    reference_type character varying(50) COLLATE pg_catalog."default",  -- vd: 'order','ticket','event'
    reference_id integer,                                    -- soft ref, tuỳ theo reference_type
    is_read boolean NOT NULL DEFAULT false,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT notifications_pkey PRIMARY KEY (notification_id)
);
CREATE INDEX IF NOT EXISTS ix_notifications_user_id ON public.notifications(user_id);

-- ===== FK cùng DB (giữ nguyên) =====

ALTER TABLE IF EXISTS public.messages
    ADD CONSTRAINT fk_messages_conversation FOREIGN KEY (conversation_id)
    REFERENCES public.conversations (conversation_id) MATCH SIMPLE
    ON UPDATE NO ACTION ON DELETE NO ACTION;

-- ===== Soft reference (KHÔNG có FK) =====
-- conversations.event_id -> event_db.events
-- conversations.user_id/organizer_id -> identity_db.users
-- messages.sender_id -> identity_db.users
-- notifications.user_id -> identity_db.users
-- notifications.reference_id -> đa hình theo reference_type, không
--   ràng buộc FK được; FE tự gọi API tương ứng khi user bấm vào.

END;
