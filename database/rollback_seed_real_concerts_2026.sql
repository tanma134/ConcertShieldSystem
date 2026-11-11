-- Roll back only rows created by seed_real_concerts_2026.sql.
BEGIN;
CREATE TEMP TABLE seeded_events AS
SELECT event_id FROM events WHERE slug LIKE 'real-%-2026' OR slug LIKE 'test-concert-%-2026';
DELETE FROM seats WHERE seat_zone_id IN (SELECT z.seat_zone_id FROM seat_zones z JOIN seat_maps m ON m.seat_map_id=z.seat_map_id JOIN seeded_events e ON e.event_id=m.event_id);
DELETE FROM seat_zones WHERE seat_map_id IN (SELECT m.seat_map_id FROM seat_maps m JOIN seeded_events e ON e.event_id=m.event_id);
DELETE FROM seat_maps WHERE event_id IN (SELECT event_id FROM seeded_events);
DELETE FROM refund_policies WHERE event_id IN (SELECT event_id FROM seeded_events);
DELETE FROM event_images WHERE event_id IN (SELECT event_id FROM seeded_events);
DELETE FROM ticket_types WHERE event_id IN (SELECT event_id FROM seeded_events);
DELETE FROM events WHERE event_id IN (SELECT event_id FROM seeded_events);
COMMIT;
