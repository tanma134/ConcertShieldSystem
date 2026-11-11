-- ConcertShield sample seed, verified from public event pages on 2026-11-11.
-- Run against event_db. This script is repeatable by slug.
-- Source pages: https://taomeoticket.com/en/show/...
-- Capacities below are SMALL LOCAL DEMO capacities because the public sources
-- publish zone structure/prices but do not publish complete numeric capacities.
BEGIN;

CREATE TEMP TABLE sample_events(slug text PRIMARY KEY, event_id int) ON COMMIT DROP;

INSERT INTO events
(organizer_id, category_id, title, slug, short_description, description,
 poster_url, poster_public_id, banner_url, banner_public_id,
 location_name, address, city, starts_at, ends_at, timezone,
 has_seating_chart, seating_mode, requires_virtual_queue, status,
 is_featured, total_tickets, sold_tickets, min_tickets_per_account,
 max_tickets_per_account, meta_title, meta_description,
 compliance_status, compliance_version, compliance_reviewed_version,
 submitted_at, approved_at, published_at, created_by, updated_by)
VALUES
(3,1,'2026 ANNYEONG HO CHI MINH CITY MUSIC FESTIVAL','real-annyeong-hcmc-2026',
 'SUPER JUNIOR-83z, CHANYEOL và NCT DREAM tại Vạn Phúc City.',
 'Sự kiện ngày 29/11/2026 lúc 18:00 tại Vạn Phúc City. Ban tổ chức: Applewood và Applewood Vietnam. Nguồn công khai: https://taomeoticket.com/en/show/2026-annyeong-ho-chi-minh-city-music-festival. TicketBox đã mở bán ngày 30/10/2026; dữ liệu mẫu giữ trạng thái hết vé theo nguồn đối chiếu.',
 'https://taomeoticket.com/img/shows/annyeong-ho-chi-minh-city-2026.jpg','source/annyeong-ho-chi-minh-city-2026',
 'https://taomeoticket.com/img/shows/annyeong-ho-chi-minh-city-2026.jpg','source/annyeong-ho-chi-minh-city-2026',
 'Vạn Phúc City','Khu đô thị Vạn Phúc, TP. Hồ Chí Minh','Hồ Chí Minh','2026-11-29 18:00:00+07'::timestamptz,'2026-11-29 22:00:00+07'::timestamptz,'SE Asia Standard Time',
 true,'StandingZones',true,'Published',true,24,24,1,4,'ANNYEONG HCMC 2026','Applewood · TicketBox · 29/11/2026','Approved',1,1,now(),now(),now(),3,3),
(3,1,'Hoàng Dũng Fanmeeting: HOMECOMING','real-hoang-dung-homecoming-2026',
 'Fanmeeting Hoàng Dũng tại Nhà hát Hòa Bình.',
 'Sự kiện ngày 22/11/2026 tại Nhà hát Hòa Bình, 240 đường 3/2, TP. Hồ Chí Minh. Bốn hạng Tổ 1 đến Tổ 4. Nguồn công khai: https://taomeoticket.com/en/show/hoang-dung-fanmeeting-homecoming. TicketBox đã mở bán ngày 05/11/2026; dữ liệu mẫu giữ trạng thái hết vé theo nguồn đối chiếu.',
 'https://taomeoticket.com/img/shows/hd-hc-poster.jpg','source/hd-hc-poster',
 'https://taomeoticket.com/img/shows/hd-hc-poster.jpg','source/hd-hc-poster',
 'Nhà hát Hòa Bình','240 đường 3/2, phường Hòa Hưng','Hồ Chí Minh','2026-11-22 19:00:00+07'::timestamptz,'2026-11-22 22:30:00+07'::timestamptz,'SE Asia Standard Time',
 true,'ReservedSeating',true,'Published',true,24,24,1,4,'Hoàng Dũng HOMECOMING','TicketBox · 22/11/2026','Approved',1,1,now(),now(),now(),3,3),
(3,1,'MỸ TÂM LIVE CONCERT – SEE THE LIGHT DAY 2','real-my-tam-see-the-light-day2-2026',
 'Mỹ Tâm Live Concert tại Vạn Phúc City.',
 'Sự kiện ngày 21/11/2026 tại Vạn Phúc City, TP. Hồ Chí Minh. Nguồn công khai: https://taomeoticket.com/en/show/my-tam-live-concert-see-the-light-day-2. CTicket đã hiển thị sold out; giá được công bố từ GA 1.500.000 đến SVIP 4.600.000 VND. Sơ đồ chi tiết chưa được ban tổ chức công bố trong nguồn đối chiếu.',
 'https://taomeoticket.com/img/shows/my-tam-live-concert-see-the-light-day-2.jpg','source/my-tam-see-the-light-day2',
 'https://taomeoticket.com/img/shows/my-tam-live-concert-see-the-light-day-2.jpg','source/my-tam-see-the-light-day2',
 'Vạn Phúc City','Khu đô thị Vạn Phúc, TP. Hồ Chí Minh','Hồ Chí Minh','2026-11-21 19:00:00+07'::timestamptz,'2026-11-21 23:00:00+07'::timestamptz,'SE Asia Standard Time',
 false,'GeneralAdmission',true,'Published',false,20,20,1,4,'Mỹ Tâm See The Light Day 2','CTicket · 21/11/2026','Approved',1,1,now(),now(),now(),3,3),
(3,1,'Vương Bình – Tiệc Thành Thân (Mâm 2)','real-vuong-binh-mam2-2026',
 'Fan gathering Vương Bình tại Cung Thiếu nhi Hà Nội.',
 'Sự kiện lúc 11:00 ngày 21/11/2026 tại Cung Thiếu nhi Hà Nội cơ sở 2, 17 Phạm Hùng. Nguồn công khai: https://taomeoticket.com/en/show/vuong-binh-tiec-thanh-than-mam-2. Bốn hạng vé HAI QUÝ, BA THƯƠNG, TƯ MẾN và ÚT CƯNG bán trên 1ZONE.',
 'https://taomeoticket.com/img/shows/vuong-binh-tiec-thanh-than-mam-2.jpg','source/vuong-binh-tiec-thanh-than-mam-2',
 'https://taomeoticket.com/img/shows/vuong-binh-tiec-thanh-than-mam-2.jpg','source/vuong-binh-tiec-thanh-than-mam-2',
 'Cung Thiếu nhi Hà Nội (cơ sở 2)','17 Phạm Hùng, phường Cầu Giấy','Hà Nội','2026-11-21 11:00:00+07'::timestamptz,'2026-11-21 15:00:00+07'::timestamptz,'SE Asia Standard Time',
 true,'ReservedSeating',true,'Published',false,24,0,1,4,'Vương Bình Tiệc Thành Thân Mâm 2','1ZONE · 21/11/2026','Approved',1,1,now(),now(),now(),3,3),
(3,1,'OFF THE RECORD: Mason Nguyễn Music Showcase','real-mason-off-the-record-2026',
 'Music showcase Mason Nguyễn tại 8889 Station.',
 'Sự kiện ngày 19/12/2026 tại 8889 Station, Vạn Phúc City, TP. Hồ Chí Minh. Nguồn công khai: https://taomeoticket.com/en/show/off-the-record-mason-nguyen-music-showcase. Mở bán lúc 12:00 ngày 14/11/2026 trên 1ZONE; dữ liệu mẫu thể hiện Coming Soon trước giờ mở bán.',
 'https://taomeoticket.com/img/shows/mason-nguyen-music-showcase.jpg','source/mason-nguyen-music-showcase',
 'https://taomeoticket.com/img/shows/mason-nguyen-music-showcase.jpg','source/mason-nguyen-music-showcase',
 '8889 Station','3 Đường số 10, Vạn Phúc City, phường Hiệp Bình','Hồ Chí Minh','2026-12-19 19:00:00+07'::timestamptz,'2026-12-19 23:00:00+07'::timestamptz,'SE Asia Standard Time',
 true,'StandingZones',true,'Published',false,25,0,1,4,'Mason Nguyễn OFF THE RECORD','1ZONE · mở bán 14/11/2026','Approved',1,1,now(),now(),now(),3,3),
(3,1,'[TEST] Concert Draft – Annyeong workflow','test-concert-draft-2026','Bản sao local để chụp trạng thái Draft.','Bản sao kiểm thử local dựa trên nguồn Annyeong; không phải sự kiện thị trường.','https://taomeoticket.com/img/shows/annyeong-ho-chi-minh-city-2026.jpg','source/test-annyeong','https://taomeoticket.com/img/shows/annyeong-ho-chi-minh-city-2026.jpg','source/test-annyeong','Vạn Phúc City','Hồ Chí Minh','Hồ Chí Minh','2026-11-29 18:00:00+07'::timestamptz,'2026-11-29 22:00:00+07'::timestamptz,'SE Asia Standard Time',true,'StandingZones',true,'Draft',false,8,0,1,4,'TEST Draft','Local UI workflow test','NotSubmitted',0,NULL,NULL,NULL,NULL,3,3),
(3,1,'[TEST] Concert Pending – Admin review','test-concert-pending-2026','Bản sao local để chụp trạng thái Pending.','Bản sao kiểm thử local; dùng để kiểm tra hàng đợi Admin.','https://taomeoticket.com/img/shows/hd-hc-poster.jpg','source/test-hoang-dung','https://taomeoticket.com/img/shows/hd-hc-poster.jpg','source/test-hoang-dung','Nhà hát Hòa Bình','Hồ Chí Minh','Hồ Chí Minh','2026-11-22 19:00:00+07'::timestamptz,'2026-11-22 22:30:00+07'::timestamptz,'SE Asia Standard Time',true,'ReservedSeating',true,'Pending',false,8,0,1,4,'TEST Pending','Local UI workflow test','Approved',1,1,now(),NULL,NULL,3,3),
(3,1,'[TEST] Concert Rejected – Resubmit','test-concert-rejected-2026','Bản sao local để chụp trạng thái Rejected.','Bản sao kiểm thử local; lý do từ chối để Organizer sửa và gửi lại.','https://taomeoticket.com/img/shows/my-tam-live-concert-see-the-light-day-2.jpg','source/test-my-tam','https://taomeoticket.com/img/shows/my-tam-live-concert-see-the-light-day-2.jpg','source/test-my-tam','Vạn Phúc City','Hồ Chí Minh','Hồ Chí Minh','2026-11-21 19:00:00+07'::timestamptz,'2026-11-21 23:00:00+07'::timestamptz,'SE Asia Standard Time',false,'GeneralAdmission',true,'Rejected',false,8,0,1,4,'TEST Rejected','Local UI workflow test','NotSubmitted',0,NULL,NULL,NULL,NULL,3,3),
(3,1,'[TEST] Concert Cancelled – Governance','test-concert-cancelled-2026','Bản sao local để chụp trạng thái Cancelled.','Bản sao kiểm thử local; không đại diện cho việc hủy sự kiện thật.','https://taomeoticket.com/img/shows/mason-nguyen-music-showcase.jpg','source/test-mason','https://taomeoticket.com/img/shows/mason-nguyen-music-showcase.jpg','source/test-mason','8889 Station','Hồ Chí Minh','Hồ Chí Minh','2026-12-19 19:00:00+07'::timestamptz,'2026-12-19 23:00:00+07'::timestamptz,'SE Asia Standard Time',false,'GeneralAdmission',true,'Cancelled',false,8,0,1,4,'TEST Cancelled','Local UI workflow test','NotSubmitted',0,NULL,NULL,NULL,NULL,3,3)
ON CONFLICT (slug) WHERE is_deleted = false DO UPDATE SET
 title=EXCLUDED.title, short_description=EXCLUDED.short_description, description=EXCLUDED.description,
 poster_url=EXCLUDED.poster_url,poster_public_id=EXCLUDED.poster_public_id,banner_url=EXCLUDED.banner_url,banner_public_id=EXCLUDED.banner_public_id,
 location_name=EXCLUDED.location_name,address=EXCLUDED.address,city=EXCLUDED.city,starts_at=EXCLUDED.starts_at,ends_at=EXCLUDED.ends_at,
 has_seating_chart=EXCLUDED.has_seating_chart,seating_mode=EXCLUDED.seating_mode,requires_virtual_queue=EXCLUDED.requires_virtual_queue,
 status=EXCLUDED.status,is_featured=EXCLUDED.is_featured,total_tickets=EXCLUDED.total_tickets,sold_tickets=EXCLUDED.sold_tickets,
 min_tickets_per_account=EXCLUDED.min_tickets_per_account,max_tickets_per_account=EXCLUDED.max_tickets_per_account,
 meta_title=EXCLUDED.meta_title,meta_description=EXCLUDED.meta_description,compliance_status=EXCLUDED.compliance_status,
 compliance_version=EXCLUDED.compliance_version,compliance_reviewed_version=EXCLUDED.compliance_reviewed_version,
 submitted_at=EXCLUDED.submitted_at,approved_at=EXCLUDED.approved_at,published_at=EXCLUDED.published_at,rejected_reason=EXCLUDED.rejected_reason,updated_at=now();

INSERT INTO sample_events SELECT slug,event_id FROM events WHERE slug LIKE 'real-%-2026' OR slug LIKE 'test-concert-%-2026';

-- Remove children of only these sample slugs so the script can be rerun cleanly.
DELETE FROM seats s USING seat_zones z, seat_maps m, sample_events e WHERE s.seat_zone_id=z.seat_zone_id AND z.seat_map_id=m.seat_map_id AND m.event_id=e.event_id;
DELETE FROM seat_zones z USING seat_maps m, sample_events e WHERE z.seat_map_id=m.seat_map_id AND m.event_id=e.event_id;
DELETE FROM seat_maps m USING sample_events e WHERE m.event_id=e.event_id;
DELETE FROM refund_policies r USING sample_events e WHERE r.event_id=e.event_id;
DELETE FROM event_images i USING sample_events e WHERE i.event_id=e.event_id;
DELETE FROM ticket_types t USING sample_events e WHERE t.event_id=e.event_id;

-- Images: source poster and, where available, source seat map.
INSERT INTO event_images(event_id,image_url,public_id,sort_order,is_main)
SELECT e.event_id,x.url,x.public_id,x.sort_order,x.is_main
FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('https://taomeoticket.com/img/shows/annyeong-ho-chi-minh-city-2026.jpg','source/annyeong-ho-chi-minh-city-2026',1,true),
 ('https://taomeoticket.com/img/shows/annyeong-ho-chi-minh-city-2026-seat-map.jpg','source/annyeong-ho-chi-minh-city-2026-seat-map',2,false)
) x(url,public_id,sort_order,is_main) WHERE e.slug='real-annyeong-hcmc-2026'
UNION ALL SELECT e.event_id,x.url,x.public_id,x.sort_order,x.is_main FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('https://taomeoticket.com/img/shows/hd-hc-poster.jpg','source/hd-hc-poster',1,true),
 ('https://taomeoticket.com/img/shows/hd-hc-seat-map.jpg','source/hd-hc-seat-map',2,false)
) x(url,public_id,sort_order,is_main) WHERE e.slug='real-hoang-dung-homecoming-2026'
UNION ALL SELECT e.event_id,x.url,x.public_id,x.sort_order,x.is_main FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('https://taomeoticket.com/img/shows/my-tam-live-concert-see-the-light-day-2.jpg','source/my-tam-see-the-light-day2',1,true)
) x(url,public_id,sort_order,is_main) WHERE e.slug='real-my-tam-see-the-light-day2-2026'
UNION ALL SELECT e.event_id,x.url,x.public_id,x.sort_order,x.is_main FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('https://taomeoticket.com/img/shows/vuong-binh-tiec-thanh-than-mam-2.jpg','source/vuong-binh-tiec-thanh-than-mam-2',1,true),
 ('https://taomeoticket.com/img/shows/vuong-binh-tiec-thanh-than-seat-map.jpg','source/vuong-binh-tiec-thanh-than-seat-map',2,false)
) x(url,public_id,sort_order,is_main) WHERE e.slug='real-vuong-binh-mam2-2026'
UNION ALL SELECT e.event_id,x.url,x.public_id,x.sort_order,x.is_main FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('https://taomeoticket.com/img/shows/mason-nguyen-music-showcase.jpg','source/mason-nguyen-music-showcase',1,true),
 ('https://taomeoticket.com/img/shows/off-the-record-mason-nguyen-seat-map.jpg','source/off-the-record-mason-nguyen-seat-map',2,false)
) x(url,public_id,sort_order,is_main) WHERE e.slug='real-mason-off-the-record-2026'
UNION ALL SELECT e.event_id,x.url,x.public_id,x.sort_order,x.is_main FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('https://taomeoticket.com/img/shows/annyeong-ho-chi-minh-city-2026.jpg','source/test-annyeong',1,true)
) x(url,public_id,sort_order,is_main) WHERE e.slug='test-concert-draft-2026'
UNION ALL SELECT e.event_id,x.url,x.public_id,x.sort_order,x.is_main FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('https://taomeoticket.com/img/shows/hd-hc-poster.jpg','source/test-hoang-dung',1,true)
) x(url,public_id,sort_order,is_main) WHERE e.slug='test-concert-pending-2026'
UNION ALL SELECT e.event_id,x.url,x.public_id,x.sort_order,x.is_main FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('https://taomeoticket.com/img/shows/my-tam-live-concert-see-the-light-day-2.jpg','source/test-my-tam',1,true)
) x(url,public_id,sort_order,is_main) WHERE e.slug='test-concert-rejected-2026'
UNION ALL SELECT e.event_id,x.url,x.public_id,x.sort_order,x.is_main FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('https://taomeoticket.com/img/shows/mason-nguyen-music-showcase.jpg','source/test-mason',1,true)
) x(url,public_id,sort_order,is_main) WHERE e.slug='test-concert-cancelled-2026';

-- Ticket tiers. Published data uses the prices reported by the public source.
INSERT INTO ticket_types(event_id,type_name,description,price,original_price,quantity,sold_quantity,min_per_order,max_per_order,color_code,sort_order,status,sales_starts_at,sales_ends_at)
SELECT e.event_id,v.* FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('VIP 1–4','Standing pens',4800000,NULL::bigint,6,6,1,4,'#ec4899',1,'Active','2026-10-30 12:00+07'::timestamptz,'2026-11-29 17:00+07'::timestamptz),
 ('CAT 1A–1D','Assigned seating demo',2800000,NULL::bigint,8,8,1,4,'#7057b5',2,'Active','2026-10-30 12:00+07'::timestamptz,'2026-11-29 17:00+07'::timestamptz),
 ('CAT 2A–2D','Assigned seating demo',1800000,NULL::bigint,10,10,1,4,'#e88a40',3,'Active','2026-10-30 12:00+07'::timestamptz,'2026-11-29 17:00+07'::timestamptz)
) v(type_name,description,price,original_price,quantity,sold_quantity,min_per_order,max_per_order,color_code,sort_order,status,sales_starts_at,sales_ends_at) WHERE e.slug='real-annyeong-hcmc-2026'
UNION ALL SELECT e.event_id,v.* FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('Tổ 1','Group Photo 1:5 and hi-bye',1999000,NULL::bigint,6,6,1,4,'#ef4444',1,'Active','2026-11-05 20:00+07'::timestamptz,'2026-11-22 18:00+07'::timestamptz),('Tổ 2','Group Photo 1:10 and hi-bye',1499000,NULL::bigint,6,6,1,4,'#22c55e',2,'Active','2026-11-05 20:00+07'::timestamptz,'2026-11-22 18:00+07'::timestamptz),('Tổ 3','Group Photo 1:25 and hi-bye',1099000,NULL::bigint,6,6,1,4,'#8b5cf6',3,'Active','2026-11-05 20:00+07'::timestamptz,'2026-11-22 18:00+07'::timestamptz),('Tổ 4','Hi-bye and sticker pack',799000,NULL::bigint,6,6,1,4,'#f97316',4,'Active','2026-11-05 20:00+07'::timestamptz,'2026-11-22 18:00+07'::timestamptz)
) v(type_name,description,price,original_price,quantity,sold_quantity,min_per_order,max_per_order,color_code,sort_order,status,sales_starts_at,sales_ends_at) WHERE e.slug='real-hoang-dung-homecoming-2026'
UNION ALL SELECT e.event_id,v.* FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('GA','General admission standing',1500000,NULL::bigint,8,8,1,4,'#64748b',1,'Active','2026-10-15 11:00+07'::timestamptz,'2026-11-21 18:00+07'::timestamptz),('FANZONE','Standing fan zone',2500000,NULL::bigint,6,6,1,4,'#ec4899',2,'Active','2026-10-15 11:00+07'::timestamptz,'2026-11-21 18:00+07'::timestamptz),('CAT','Seated category',2700000,NULL::bigint,6,6,1,4,'#8b5cf6',3,'Active','2026-10-15 11:00+07'::timestamptz,'2026-11-21 18:00+07'::timestamptz)
) v(type_name,description,price,original_price,quantity,sold_quantity,min_per_order,max_per_order,color_code,sort_order,status,sales_starts_at,sales_ends_at) WHERE e.slug='real-my-tam-see-the-light-day2-2026'
UNION ALL SELECT e.event_id,v.* FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('HAI QUÝ','Signing, meet and photo 1:1',2996000,NULL::bigint,6,0,1,4,'#e7dc47',1,'Active','2026-10-18 00:00+07'::timestamptz,'2026-11-21 09:00+07'::timestamptz),('BA THƯƠNG','Group photo benefits',2196000,NULL::bigint,6,0,1,4,'#5cc5df',2,'Active','2026-10-18 00:00+07'::timestamptz,'2026-11-21 09:00+07'::timestamptz),('TƯ MẾN','Group photo 1:10',1696000,NULL::bigint,6,0,1,4,'#b68ad9',3,'Active','2026-10-18 00:00+07'::timestamptz,'2026-11-21 09:00+07'::timestamptz),('ÚT CƯNG','Group photo 1:20',996000,NULL::bigint,6,0,1,4,'#ee8d93',4,'Active','2026-10-18 00:00+07'::timestamptz,'2026-11-21 09:00+07'::timestamptz)
) v(type_name,description,price,original_price,quantity,sold_quantity,min_per_order,max_per_order,color_code,sort_order,status,sales_starts_at,sales_ends_at) WHERE e.slug='real-vuong-binh-mam2-2026'
UNION ALL SELECT e.event_id,v.* FROM sample_events e CROSS JOIN LATERAL (VALUES
 ('ClickIt','Standing zone',772000,NULL::bigint,5,0,1,4,'#ec4899',1,'Active','2026-11-14 12:00+07'::timestamptz,'2026-12-19 18:00+07'::timestamptz),('CanhTác HàoQuang','Standing zone',1072000,NULL::bigint,5,0,1,4,'#f59e0b',2,'Active','2026-11-14 12:00+07'::timestamptz,'2026-12-19 18:00+07'::timestamptz),('LáĐa','Seated zone',2272000,NULL::bigint,5,0,1,4,'#22c55e',3,'Active','2026-11-14 12:00+07'::timestamptz,'2026-12-19 18:00+07'::timestamptz),('Nếu Như Mà','VIP seated zone',3272000,NULL::bigint,5,0,1,4,'#8b5cf6',4,'Active','2026-11-14 12:00+07'::timestamptz,'2026-12-19 18:00+07'::timestamptz),('ViếtChoEm','VIP boxset seated zone',5272000,NULL::bigint,5,0,1,4,'#eab308',5,'Active','2026-11-14 12:00+07'::timestamptz,'2026-12-19 18:00+07'::timestamptz)
) v(type_name,description,price,original_price,quantity,sold_quantity,min_per_order,max_per_order,color_code,sort_order,status,sales_starts_at,sales_ends_at) WHERE e.slug='real-mason-off-the-record-2026'
UNION ALL SELECT e.event_id, 'Demo', 'Local workflow demo', 100000, NULL::bigint, 8, 0, 1, 4, '#a855f7', 1, 'Active', now(), now()+interval '30 days' FROM sample_events e WHERE e.slug LIKE 'test-concert-%-2026';

-- Seat maps and small source-shaped local layouts. Standing zones have no seats.
INSERT INTO seat_maps(event_id,name,layout_json)
SELECT event_id,'Official source layout (local demo)',jsonb_build_object('dimensions',jsonb_build_object('width',1000,'height',700),'source','Public event seat map; local demo capacities') FROM sample_events WHERE slug IN ('real-annyeong-hcmc-2026','real-hoang-dung-homecoming-2026','real-vuong-binh-mam2-2026','real-mason-off-the-record-2026');

-- Use ticket type order per event to create zones; capacities match ticket quantities.
INSERT INTO seat_zones(seat_map_id,ticket_type_id,zone_name,zone_type,capacity,shape_json)
SELECT m.seat_map_id,t.ticket_type_id,t.type_name,CASE WHEN t.type_name IN ('VIP 1–4','ClickIt','CanhTác HàoQuang') THEN 'Standing' ELSE 'Seated' END,t.quantity,
 jsonb_build_object('type','rectangle','x',(row_number() over(partition by m.seat_map_id order by t.sort_order)-1)*22+5,'y',25,'w',18,'h',18,'rot',0)
FROM seat_maps m JOIN sample_events e ON e.event_id=m.event_id JOIN ticket_types t ON t.event_id=e.event_id;

-- Generate numbered demo seats for seated zones. Public sources describe zones but not full seat counts.
INSERT INTO seats(seat_zone_id,row_label,seat_number,x_coordinate,y_coordinate,status)
SELECT z.seat_zone_id,'R'||r,'S'||s,5+s*10,30+r*12,'Available'
FROM seat_zones z CROSS JOIN generate_series(1,2) r CROSS JOIN generate_series(1,3) s
WHERE z.zone_type='Seated' AND z.capacity >= 6;

-- Refund policy required by publication validation.
INSERT INTO refund_policies(event_id,policy_name,description,deadline_before_event_hours,refund_percent,requires_organizer_approval,is_active)
SELECT event_id,'Source policy','Refund follows the original ticket platform/organizer terms; this local sample requires review.',48,0,true,true FROM sample_events;

-- Make quantities reflect the generated seated/standing zones. This is needed by EventSubmissionValidator.
UPDATE ticket_types t SET quantity = q.capacity, updated_at=now()
FROM (SELECT ticket_type_id, SUM(capacity) capacity FROM seat_zones GROUP BY ticket_type_id) q
WHERE t.ticket_type_id=q.ticket_type_id AND t.event_id IN (SELECT event_id FROM sample_events) AND t.sold_quantity <= q.capacity;

SELECT e.event_id,e.status,e.slug,e.title,e.starts_at,e.ends_at,count(DISTINCT t.ticket_type_id) ticket_types,count(DISTINCT i.image_id) images
FROM events e JOIN sample_events s ON s.event_id=e.event_id LEFT JOIN ticket_types t ON t.event_id=e.event_id AND NOT t.is_deleted LEFT JOIN event_images i ON i.event_id=e.event_id AND NOT i.is_deleted
GROUP BY e.event_id,e.status,e.slug,e.title,e.starts_at,e.ends_at ORDER BY e.event_id;

COMMIT;


