-- Run against event_db. Safe to run again; does not change event titles/dates.
-- For existing tickets/orders, also run fix_ticket_order_image_urls.sql against ticket_db.
-- URLs and public IDs verified against the Cloudinary resource API.
-- Where no separate banner exists, reuse the verified poster.
BEGIN;

CREATE TEMP TABLE event_image_fixes (
    slug text PRIMARY KEY,
    poster_id text NOT NULL, poster_url text NOT NULL,
    banner_id text NOT NULL, banner_url text NOT NULL,
    detail_id text NOT NULL, detail_url text NOT NULL
) ON COMMIT DROP;

INSERT INTO event_image_fixes VALUES
('sontung-skytour-2026',
 'events/sontung-skytour-2025-poster',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546576/events/sontung-skytour-2025-poster.jpg',
 'events/sontung-skytour-2025-poster',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546576/events/sontung-skytour-2025-poster.jpg',
 'events/sontung-skytour-detail',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546577/events/sontung-skytour-detail.jpg'),
('denvau-tour-2026',
 'events/denvau-tour-2025-poster',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546578/events/denvau-tour-2025-poster.png',
 'events/denvau-tour-2025-poster',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546578/events/denvau-tour-2025-poster.png',
 'events/denvau-tour-detail',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546578/events/denvau-tour-detail.jpg'),
('exsh-mua1-danang',
 'events/exsh-s1-danang-poster',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546575/events/exsh-s1-danang-poster.jpg',
 'events/exsh-s1-danang-poster',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546575/events/exsh-s1-danang-poster.jpg',
 'events/exsh-danang-detail',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546574/events/exsh-danang-detail.webp'),
('atvncg-mua1-finale',
 'events/atvncg-s1-finale-poster',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546566/events/atvncg-s1-finale-poster.png',
 'events/atvncg-s1-finale-poster',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546566/events/atvncg-s1-finale-poster.png',
 'events/atvncg-s1-detail',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546565/events/atvncg-s1-detail.jpg'),
('atvncg-mua1-hanoi',
 'events/atvncg-hanoi-poster',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546567/events/atvncg-hanoi-poster.jpg',
 'events/posters/nhrwdcvxsh1wldvbpzsz',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1784074398/events/posters/nhrwdcvxsh1wldvbpzsz.jpg',
 'events/posters/nhrwdcvxsh1wldvbpzsz',
 'https://res.cloudinary.com/pzccgu3t/image/upload/v1784074398/events/posters/nhrwdcvxsh1wldvbpzsz.jpg');

-- Do not silently update a partial or ambiguous set of events.
DO $$
BEGIN
    IF EXISTS (
        SELECT f.slug FROM event_image_fixes f
        LEFT JOIN events e ON e.slug = f.slug AND NOT e.is_deleted
        GROUP BY f.slug HAVING count(e.event_id) <> 1
    ) THEN
        RAISE EXCEPTION 'Expected exactly one active event for each of the five slugs';
    END IF;
END $$;

UPDATE events e
SET poster_url = f.poster_url, poster_public_id = f.poster_id,
    banner_url = f.banner_url, banner_public_id = f.banner_id,
    updated_at = now()
FROM event_image_fixes f
WHERE e.slug = f.slug AND NOT e.is_deleted;

-- Repair the existing seeded gallery entries; retain ordering and main flags.
UPDATE event_images i
SET image_url = CASE
        WHEN i.public_id LIKE '%-detail' THEN f.detail_url
        WHEN i.public_id LIKE '%-banner' THEN f.banner_url
        WHEN i.public_id = f.banner_id AND f.banner_id <> f.poster_id THEN f.banner_url
        ELSE f.poster_url END,
    public_id = CASE
        WHEN i.public_id LIKE '%-detail' THEN f.detail_id
        WHEN i.public_id LIKE '%-banner' THEN f.banner_id
        WHEN i.public_id = f.banner_id AND f.banner_id <> f.poster_id THEN f.banner_id
        ELSE f.poster_id END
FROM events e JOIN event_image_fixes f ON f.slug = e.slug
WHERE i.event_id = e.event_id AND NOT e.is_deleted AND NOT i.is_deleted
  AND (i.public_id IN (f.poster_id, f.banner_id, f.detail_id)
       OR i.public_id = ANY (ARRAY[
           'events/sontung-skytour-2026-poster', 'events/sontung-skytour-2026-banner',
           'events/denvau-tour-2026-poster', 'events/denvau-tour-2026-banner',
           'events/exsh-s1-danang-detail', 'events/exsh-s1-danang-banner',
           'events/atvncg-s1-finale-detail', 'events/atvncg-s1-finale-banner',
           'events/atvncg-hanoi-detail', 'events/atvncg-s1-hanoi-poster',
           'events/atvncg-s1-hanoi-banner'
       ]));

SELECT e.event_id, e.slug, e.poster_url, e.banner_url
FROM events e JOIN event_image_fixes f ON e.slug = f.slug
WHERE NOT e.is_deleted ORDER BY e.event_id;

COMMIT;
