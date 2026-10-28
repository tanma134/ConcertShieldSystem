-- Apply once to an existing event_db after deploying the Event/Seating flow fix.
-- Starter layouts owned by the seeded Admin account must be visible to organizers.
UPDATE public.seating_templates
SET is_public = true,
    updated_at = NOW()
WHERE organizer_id = 1
  AND is_deleted = false
  AND is_public = false;

-- Repair old numbered zones whose capacity was left at zero.
UPDATE public.seat_zones z
SET capacity = counts.seat_count
FROM (
    SELECT seat_zone_id, COUNT(*)::integer AS seat_count
    FROM public.seats
    GROUP BY seat_zone_id
) counts
WHERE z.seat_zone_id = counts.seat_zone_id
  AND z.zone_type = 'Seated'
  AND z.capacity <> counts.seat_count;

-- Keep event flags/modes aligned with the actual map data.
UPDATE public.events e
SET has_seating_chart = EXISTS (
        SELECT 1 FROM public.seat_maps sm
        WHERE sm.event_id = e.event_id AND sm.is_deleted = false
    ),
    seating_mode = CASE
        WHEN NOT EXISTS (SELECT 1 FROM public.seat_maps sm WHERE sm.event_id = e.event_id AND sm.is_deleted = false)
            THEN 'GeneralAdmission'
        WHEN EXISTS (
            SELECT 1 FROM public.seat_maps sm
            JOIN public.seat_zones sz ON sz.seat_map_id = sm.seat_map_id
            WHERE sm.event_id = e.event_id AND sm.is_deleted = false AND sz.zone_type = 'Seated'
        ) THEN 'ReservedSeating'
        ELSE 'StandingZones'
    END;
