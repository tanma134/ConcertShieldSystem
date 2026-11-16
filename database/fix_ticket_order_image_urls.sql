-- Run against ticket_db, NOT event_db.
-- Ticket details, My Tickets and order history read orders.poster_url.
-- Poster URLs copied from the verified event_db records. Safe to run again.
BEGIN;
WITH poster_fixes(event_id, poster_url) AS (
VALUES
(2, 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546566/events/atvncg-s1-finale-poster.png'),
(4, 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546578/events/denvau-tour-2025-poster.png'),
(6, 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546567/events/atvncg-hanoi-poster.jpg'),
(10, 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546575/events/exsh-s1-danang-poster.jpg'),
(12, 'https://res.cloudinary.com/pzccgu3t/image/upload/v1783546576/events/sontung-skytour-2025-poster.jpg')
)
UPDATE orders o SET poster_url = f.poster_url
FROM poster_fixes f
WHERE o.event_id = f.event_id AND NOT o.is_deleted
  AND o.poster_url IS DISTINCT FROM f.poster_url
RETURNING o.order_id, o.event_id, o.poster_url;
COMMIT;
