CREATE TABLE IF NOT EXISTS public.reviews
(
    review_id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
    event_id integer NOT NULL,
    user_id integer NOT NULL,
    rating integer NOT NULL CHECK (rating BETWEEN 1 AND 5),
    comment character varying(1000),
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone,
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,
    CONSTRAINT reviews_pkey PRIMARY KEY (review_id),
    CONSTRAINT uq_reviews_event_user UNIQUE (event_id, user_id)
);
CREATE INDEX IF NOT EXISTS ix_reviews_event_id ON public.reviews(event_id);
CREATE TABLE IF NOT EXISTS public.review_replies
(
    reply_id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
    review_id integer NOT NULL,
    user_id integer NOT NULL,
    role character varying(30) NOT NULL,
    comment character varying(1000) NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    updated_at timestamp with time zone,
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,
    CONSTRAINT review_replies_pkey PRIMARY KEY (reply_id),
    CONSTRAINT fk_review_replies_review FOREIGN KEY (review_id) REFERENCES public.reviews(review_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_review_replies_review_id ON public.review_replies(review_id);
