

-- =================================================================
-- BASE TICKET SCHEMA (REMOVED LEADING ROLLBACK)
-- =================================================================
-- ============================================================
-- ticket_db (TicketAPI) - FULL SCRIPT
-- Bao gồm: Orders, Order Details, Attendees, Tickets, QR Tokens
-- ============================================================
BEGIN;

-- ==========================================
-- 1. BẢNG ORDERS (Đơn hàng chính)
-- ==========================================
CREATE TABLE IF NOT EXISTS public.orders
(
    order_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MINVALUE 1 MAXVALUE 2147483647 CACHE 1 ),
    customer_id integer NOT NULL,                      -- ID người mua (chủ tài khoản)
    event_id integer NOT NULL,                         
    queue_session_id integer,                          -- Đối soát với QueueDB nếu có
    voucher_id integer,                                -- Đối soát với PaymentDB nếu có
    
    -- [MỚI] Đối soát và Hold
    hold_token character varying(255),                 -- Token từ Redis để đối soát nếu có sự cố
    payment_gateway_ref character varying(255),        -- Mã giao dịch từ VNPay (Vnp_TransactionNo)
    
    -- Snapshot thông tin người mua & sự kiện (tránh bị thay đổi sau này)
    full_name character varying(255),
    email character varying(255),
    phone character varying(50),
    event_name text,                                     
    poster_url text,          
    order_date timestamp with time zone DEFAULT now(),
    starts_at timestamp with time zone,
    ends_at timestamp with time zone,
    
    -- Tài chính
    total_amount bigint NOT NULL,
    discount_amount bigint DEFAULT 0,
    final_amount bigint NOT NULL,
    payment_method character varying(50),
    
    -- Trạng thái: Pending, Paid, Cancelled, Expired, Refunded
    status character varying(20) DEFAULT 'Pending', 
    
    -- [QUAN TRỌNG] Khớp với TTL của Redis (VD: NOW() + 10 phút)
    expires_at timestamp with time zone,                 

    -- Soft delete
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                                    
    CONSTRAINT orders_pkey PRIMARY KEY (order_id)
);

CREATE INDEX IF NOT EXISTS ix_orders_customer_id ON public.orders(customer_id);
CREATE INDEX IF NOT EXISTS ix_orders_event_id ON public.orders(event_id);
CREATE INDEX IF NOT EXISTS ix_orders_status ON public.orders(status); -- Tối ưu query tìm đơn Pending/Expired


-- ==========================================
-- 2. BẢNG ORDER_DETAILS (Chi tiết đơn hàng)
-- ==========================================
CREATE TABLE IF NOT EXISTS public.order_details
(
    order_detail_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MAXVALUE 2147483647 CACHE 1 ),
    order_id integer NOT NULL,
    ticket_type_id integer NOT NULL,                     
    ticket_type_name character varying(100),             -- Snapshot tên loại vé
    
    quantity integer NOT NULL,
    unit_price bigint NOT NULL,
    
    -- [TÙY CHỌN NHƯNG KHUYÊN DÙNG] Lưu danh sách ghế nếu là Reserved Seating (dự phòng nếu Redis lỗi)
    -- Ví dụ dữ liệu: [101, 102, 105]
    seat_ids jsonb, 
    
    CONSTRAINT order_details_pkey PRIMARY KEY (order_detail_id)
);

CREATE INDEX IF NOT EXISTS ix_order_details_order_id ON public.order_details(order_id);


-- ==========================================
-- 3. BẢNG ORDER_ATTENDEES (Thông tin người đi cùng) [MỚI]
-- ==========================================
CREATE TABLE IF NOT EXISTS public.order_attendees
(
    attendee_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MAXVALUE 2147483647 CACHE 1 ),
    order_id integer NOT NULL,
    ticket_type_id integer NULL,
	seat_id integer NULL,
    full_name character varying(255) NOT NULL,
    citizen_id character varying(50),             -- CCCD/CMND/Hộ chiếu (Rất quan trọng cho concert)
    phone character varying(50),
    is_primary_buyer boolean DEFAULT false,       -- True: Chủ tài khoản, False: Người đi cùng
    
    CONSTRAINT order_attendees_pkey PRIMARY KEY (attendee_id)
);

CREATE INDEX IF NOT EXISTS ix_order_attendees_order_id ON public.order_attendees(order_id);

-- ==========================================
-- 4. BẢNG TICKETS (Vé chính thức - Sinh ra sau khi thanh toán)
-- ==========================================
CREATE TABLE IF NOT EXISTS public.tickets
(
    ticket_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MAXVALUE 2147483647 CACHE 1 ),
    ticket_code uuid NOT NULL DEFAULT gen_random_uuid(), -- Mã vé duy nhất
    order_id integer NOT NULL,
    event_id integer NOT NULL,                            
    ticket_type_id integer NOT NULL,                        
    
    seat_id integer,                                         -- NULL nếu là Standing Zone / General Admission
    owner_user_id integer,                                   -- ID người sở hữu vé này
    
    -- Snapshot
    ticket_type_name character varying(100), 
    owner_name character varying(100),       
    
    -- Trạng thái: Active, CheckedIn, Revoked, Refunded
    status character varying(20) DEFAULT 'Active',
    checked_in_at timestamp with time zone,
    checked_in_by integer,                                   -- ID nhân viên check-in
    
    -- Soft delete
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at timestamp with time zone,
    deleted_by integer,                                        
    CONSTRAINT tickets_pkey PRIMARY KEY (ticket_id),
    CONSTRAINT tickets_ticket_code_key UNIQUE (ticket_code)
);

CREATE INDEX IF NOT EXISTS ix_tickets_event_id ON public.tickets(event_id);
CREATE INDEX IF NOT EXISTS ix_tickets_order_id ON public.tickets(order_id);
CREATE INDEX IF NOT EXISTS ix_tickets_owner_user_id ON public.tickets(owner_user_id);
CREATE INDEX IF NOT EXISTS ix_tickets_ticket_type_id ON public.tickets(ticket_type_id);

-- [SỬA LỖI QUAN TRỌNG] Partial Unique Index: 
-- Chỉ chặn trùng khi seat_id có giá trị (không null) VÀ vé đang Active VÀ chưa bị xóa.
-- Điều này đảm bảo 100% không thể bán trùng 1 ghế cụ thể.
CREATE UNIQUE INDEX IF NOT EXISTS uq_tickets_seat_active 
ON public.tickets(seat_id) 
WHERE seat_id IS NOT NULL AND status = 'Active' AND is_deleted = false;


-- ==========================================
-- 5. BẢNG TICKET_QR_TOKENS (Dynamic QR Code)
-- ==========================================
CREATE TABLE IF NOT EXISTS public.ticket_qr_tokens
(
    ticket_qr_token_id integer NOT NULL GENERATED ALWAYS AS IDENTITY ( INCREMENT 1 START 1 MAXVALUE 2147483647 CACHE 1 ),
    ticket_id integer NOT NULL,
    qr_token character varying(200) NOT NULL,
    issued_at timestamp with time zone NOT NULL DEFAULT now(),
    expires_at timestamp with time zone NOT NULL,
    is_revoked boolean NOT NULL DEFAULT false,
    revoked_at timestamp with time zone,
    is_used boolean NOT NULL DEFAULT false,
    used_at timestamp with time zone,
    CONSTRAINT ticket_qr_tokens_pkey PRIMARY KEY (ticket_qr_token_id),
    CONSTRAINT ticket_qr_tokens_qr_token_key UNIQUE (qr_token)
);

-- Index giúp tìm token cực nhanh khi quét QR tại cổng
CREATE INDEX IF NOT EXISTS ix_ticket_qr_tokens_qr ON public.ticket_qr_tokens(qr_token);

-- Index giúp lấy token đang active của 1 vé (tối ưu performance)
CREATE INDEX IF NOT EXISTS ix_ticket_qr_tokens_ticket_active 
ON public.ticket_qr_tokens(ticket_id) 
WHERE is_revoked = false;


-- ==========================================
-- 6. FOREIGN KEY CONSTRAINTS (Ràng buộc toàn vẹn)
-- ==========================================

ALTER TABLE IF EXISTS public.order_details
    DROP CONSTRAINT IF EXISTS fk_order_details_order;
ALTER TABLE IF EXISTS public.order_details
    ADD CONSTRAINT fk_order_details_order FOREIGN KEY (order_id)
    REFERENCES public.orders (order_id) ON DELETE CASCADE;

ALTER TABLE IF EXISTS public.order_attendees
    DROP CONSTRAINT IF EXISTS fk_order_attendees_order;
ALTER TABLE IF EXISTS public.order_attendees
    ADD CONSTRAINT fk_order_attendees_order FOREIGN KEY (order_id)
    REFERENCES public.orders (order_id) ON DELETE CASCADE;

ALTER TABLE IF EXISTS public.tickets
    DROP CONSTRAINT IF EXISTS fk_tickets_order;
ALTER TABLE IF EXISTS public.tickets
    ADD CONSTRAINT fk_tickets_order FOREIGN KEY (order_id)
    REFERENCES public.orders (order_id) ON DELETE CASCADE;

ALTER TABLE IF EXISTS public.ticket_qr_tokens
    DROP CONSTRAINT IF EXISTS fk_ticket_qr_tokens_ticket;
ALTER TABLE IF EXISTS public.ticket_qr_tokens
    ADD CONSTRAINT fk_ticket_qr_tokens_ticket FOREIGN KEY (ticket_id)
    REFERENCES public.tickets (ticket_id) ON DELETE CASCADE;

-- UC_9: ticket return requests.
-- A ticket can have many requests over time, but only ONE open (Pending) request.
CREATE TABLE IF NOT EXISTS public.ticket_return_requests (
    ticket_return_request_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ticket_id        integer      NOT NULL,
    order_id         integer      NOT NULL,
    event_id         integer      NOT NULL,
    requester_user_id integer     NOT NULL,
    reason           varchar(500) NOT NULL,
    status           varchar(20)  NOT NULL DEFAULT 'Pending',
    refund_amount    bigint       NOT NULL DEFAULT 0,
    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    updated_at       timestamp with time zone NOT NULL DEFAULT now(),
    cancelled_at     timestamp with time zone NULL,
    reviewed_by      integer      NULL,
    reviewed_at      timestamp with time zone NULL,
    review_note      varchar(500) NULL,
    CONSTRAINT fk_ticket_return_requests_ticket FOREIGN KEY (ticket_id) REFERENCES public.tickets (ticket_id),
    CONSTRAINT ck_ticket_return_requests_status CHECK (status IN ('Pending', 'Approved', 'Rejected', 'Cancelled'))
);

-- Upgrade an existing older ticket_return_requests table to the shape expected by current TicketAPI.
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS event_id integer;
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS requester_user_id integer;
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS refund_amount bigint NOT NULL DEFAULT 0;
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS created_at timestamp with time zone NOT NULL DEFAULT now();
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone NOT NULL DEFAULT now();
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS cancelled_at timestamp with time zone;
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS reviewed_by integer;
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS reviewed_at timestamp with time zone;
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS review_note varchar(500);

CREATE UNIQUE INDEX IF NOT EXISTS uq_ticket_return_requests_open
    ON public.ticket_return_requests (ticket_id) WHERE status = 'Pending';
CREATE INDEX IF NOT EXISTS ix_ticket_return_requests_requester
    ON public.ticket_return_requests (requester_user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_ticket_return_requests_event
    ON public.ticket_return_requests (event_id, status);

-- =============================================================================
-- DEMO DATA FOR REVENUE / MY TICKETS / CHECK-IN
-- Event 4 belongs to organizer user 3 in the supplied event_db seed.
-- Customer 2 buys 3 paid orders. Ticket type IDs are soft references to event_db.
-- =============================================================================
INSERT INTO public.orders
(customer_id,event_id,full_name,email,phone,event_name,poster_url,order_date,total_amount,discount_amount,final_amount,payment_method,payment_gateway_ref,status,is_deleted)
SELECT 2,4,'Test Customer','customer@ticketbox.com','0987654321','Đen Vâu - Tour "Đi Đâu Cho Thiếp Theo Cùng"',NULL,now()-interval '3 days',1000000,0,1000000,'VNPay','DEMO-E4-001','Paid',false
WHERE NOT EXISTS (SELECT 1 FROM public.orders WHERE payment_gateway_ref='DEMO-E4-001');
INSERT INTO public.orders
(customer_id,event_id,full_name,email,phone,event_name,poster_url,order_date,total_amount,discount_amount,final_amount,payment_method,payment_gateway_ref,status,is_deleted)
SELECT 2,4,'Test Customer','customer@ticketbox.com','0987654321','Đen Vâu - Tour "Đi Đâu Cho Thiếp Theo Cùng"',NULL,now()-interval '2 days',1500000,100000,1400000,'VNPay','DEMO-E4-002','Paid',false
WHERE NOT EXISTS (SELECT 1 FROM public.orders WHERE payment_gateway_ref='DEMO-E4-002');
INSERT INTO public.orders
(customer_id,event_id,full_name,email,phone,event_name,poster_url,order_date,total_amount,discount_amount,final_amount,payment_method,payment_gateway_ref,status,is_deleted)
SELECT 2,4,'Test Customer','customer@ticketbox.com','0987654321','Đen Vâu - Tour "Đi Đâu Cho Thiếp Theo Cùng"',NULL,now()-interval '1 day',500000,0,500000,'VNPay','DEMO-E4-003','Paid',false
WHERE NOT EXISTS (SELECT 1 FROM public.orders WHERE payment_gateway_ref='DEMO-E4-003');

INSERT INTO public.order_details(order_id,ticket_type_id,ticket_type_name,quantity,unit_price)
SELECT o.order_id,12,'Standard',2,500000 FROM public.orders o
WHERE o.payment_gateway_ref='DEMO-E4-001'
AND NOT EXISTS (SELECT 1 FROM public.order_details d WHERE d.order_id=o.order_id);
INSERT INTO public.order_details(order_id,ticket_type_id,ticket_type_name,quantity,unit_price)
SELECT o.order_id,11,'VIP',2,750000 FROM public.orders o
WHERE o.payment_gateway_ref='DEMO-E4-002'
AND NOT EXISTS (SELECT 1 FROM public.order_details d WHERE d.order_id=o.order_id);
INSERT INTO public.order_details(order_id,ticket_type_id,ticket_type_name,quantity,unit_price)
SELECT o.order_id,12,'Standard',1,500000 FROM public.orders o
WHERE o.payment_gateway_ref='DEMO-E4-003'
AND NOT EXISTS (SELECT 1 FROM public.order_details d WHERE d.order_id=o.order_id);

-- Five tickets: two already checked in, three active.
INSERT INTO public.tickets(order_id,event_id,ticket_type_id,owner_user_id,ticket_type_name,owner_name,status,checked_in_at,checked_in_by,is_deleted)
SELECT o.order_id,4,12,2,'Standard','Test Customer','CheckedIn',now()-interval '2 hours',4,false FROM public.orders o
WHERE o.payment_gateway_ref='DEMO-E4-001'
AND NOT EXISTS (SELECT 1 FROM public.tickets t WHERE t.order_id=o.order_id);
INSERT INTO public.tickets(order_id,event_id,ticket_type_id,owner_user_id,ticket_type_name,owner_name,status,checked_in_at,checked_in_by,is_deleted)
SELECT o.order_id,4,12,2,'Standard','Guest A','Active',NULL,NULL,false FROM public.orders o
WHERE o.payment_gateway_ref='DEMO-E4-001' AND (SELECT count(*) FROM public.tickets t WHERE t.order_id=o.order_id)<2;
INSERT INTO public.tickets(order_id,event_id,ticket_type_id,owner_user_id,ticket_type_name,owner_name,status,checked_in_at,checked_in_by,is_deleted)
SELECT o.order_id,4,11,2,'VIP','Test Customer','CheckedIn',now()-interval '1 hour',5,false FROM public.orders o
WHERE o.payment_gateway_ref='DEMO-E4-002'
AND NOT EXISTS (SELECT 1 FROM public.tickets t WHERE t.order_id=o.order_id);
INSERT INTO public.tickets(order_id,event_id,ticket_type_id,owner_user_id,ticket_type_name,owner_name,status,is_deleted)
SELECT o.order_id,4,11,2,'VIP','Guest B','Active',false FROM public.orders o
WHERE o.payment_gateway_ref='DEMO-E4-002' AND (SELECT count(*) FROM public.tickets t WHERE t.order_id=o.order_id)<2;
INSERT INTO public.tickets(order_id,event_id,ticket_type_id,owner_user_id,ticket_type_name,owner_name,status,is_deleted)
SELECT o.order_id,4,12,2,'Standard','Test Customer','Active',false FROM public.orders o
WHERE o.payment_gateway_ref='DEMO-E4-003'
AND NOT EXISTS (SELECT 1 FROM public.tickets t WHERE t.order_id=o.order_id);

-- One approved historical return so Revenue shows a refund.
INSERT INTO public.ticket_return_requests
(ticket_id,order_id,event_id,requester_user_id,reason,status,refund_amount,created_at,updated_at,reviewed_by,reviewed_at,review_note)
SELECT t.ticket_id,t.order_id,4,2,'Demo approved refund','Approved',250000,now()-interval '20 hours',now()-interval '18 hours',1,now()-interval '18 hours','Demo approved request'
FROM public.tickets t JOIN public.orders o ON o.order_id=t.order_id
WHERE o.payment_gateway_ref='DEMO-E4-003'
AND NOT EXISTS (SELECT 1 FROM public.ticket_return_requests r WHERE r.ticket_id=t.ticket_id AND r.status='Approved')
LIMIT 1;

COMMIT;


-- DEMO PENDING RETURN FOR REVIEW DASHBOARD (rerun-safe)
-- Uses an active ticket from demo event 4; keeps one request Pending for Staff/Admin review.
INSERT INTO public.ticket_return_requests(ticket_id,order_id,event_id,requester_user_id,reason,status,refund_amount,created_at,updated_at)
SELECT t.ticket_id,t.order_id,t.event_id,t.owner_user_id,'Demo: cannot attend the concert','Pending',
       CASE WHEN t.ticket_type_name='VIP' THEN 750000 ELSE 500000 END,now()-interval '30 minutes',now()-interval '30 minutes'
FROM public.tickets t JOIN public.orders o ON o.order_id=t.order_id
WHERE t.event_id=4 AND t.status='Active' AND o.status='Paid'
  AND NOT EXISTS (SELECT 1 FROM public.ticket_return_requests r WHERE r.ticket_id=t.ticket_id AND r.status='Pending')
ORDER BY t.ticket_id LIMIT 1;
UPDATE public.tickets t SET status='ReturnPending'
WHERE EXISTS (SELECT 1 FROM public.ticket_return_requests r WHERE r.ticket_id=t.ticket_id AND r.status='Pending');


-- =================================================================
-- CONSOLIDATED TICKET MIGRATIONS
-- =================================================================
-- CHẠY TRONG ticket_db (05_ticket_db). Cần có sẵn orders/tickets từ 05_ticket_db.sql.
-- Chạy MỘT LẦN, an toàn khi chạy lại (CREATE/ALTER ... IF NOT EXISTS).


-- ---------- 001 ticket_return_requests (bảng nền, 05_ticket_db cũ chưa có) ----------
-- UC_9: ticket return requests. Apply once to 05_ticket_db.
-- A ticket can have many requests over time, but only ONE open (Pending) request.
CREATE TABLE IF NOT EXISTS public.ticket_return_requests (
    ticket_return_request_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ticket_id        integer      NOT NULL,
    order_id         integer      NOT NULL,
    event_id         integer      NOT NULL,
    requester_user_id integer     NOT NULL,
    reason           varchar(500) NOT NULL,
    status           varchar(20)  NOT NULL DEFAULT 'Pending',
    refund_amount    bigint       NOT NULL DEFAULT 0,
    created_at       timestamp with time zone NOT NULL DEFAULT now(),
    updated_at       timestamp with time zone NOT NULL DEFAULT now(),
    cancelled_at     timestamp with time zone NULL,
    reviewed_by      integer      NULL,
    reviewed_at      timestamp with time zone NULL,
    review_note      varchar(500) NULL,
    CONSTRAINT fk_ticket_return_requests_ticket FOREIGN KEY (ticket_id) REFERENCES public.tickets (ticket_id),
    CONSTRAINT ck_ticket_return_requests_status CHECK (status IN ('Pending', 'Approved', 'Rejected', 'Cancelled'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_ticket_return_requests_open
    ON public.ticket_return_requests (ticket_id) WHERE status = 'Pending';
CREATE INDEX IF NOT EXISTS ix_ticket_return_requests_requester
    ON public.ticket_return_requests (requester_user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_ticket_return_requests_event
    ON public.ticket_return_requests (event_id, status);

-- ---------- 009 event changes ----------
-- Chạy trong ticket_db sau schema orders/tickets/ticket_return_requests hiện có.
BEGIN;
CREATE TABLE IF NOT EXISTS applied_event_changes (
 change_id bigint PRIMARY KEY, event_id integer NOT NULL,
 type text NOT NULL CHECK(type IN ('Postpone','Reschedule')),
 starts_at timestamptz, ends_at timestamptz, schedule_version integer NOT NULL,
 reason text NOT NULL, title text NOT NULL, slug text NOT NULL, applied_at timestamptz NOT NULL,
 UNIQUE(event_id,schedule_version)
);
CREATE TABLE IF NOT EXISTS affected_tickets (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 change_id bigint NOT NULL REFERENCES applied_event_changes(change_id),
 ticket_id integer NOT NULL REFERENCES tickets(ticket_id), order_id integer NOT NULL REFERENCES orders(order_id),
 customer_id integer NOT NULL, paid_amount bigint NOT NULL CHECK(paid_amount >= 0), notified_at timestamptz,
 UNIQUE(change_id,ticket_id)
);
CREATE INDEX IF NOT EXISTS ix_affected_notification_pending ON affected_tickets(id) WHERE notified_at IS NULL;
COMMIT;

-- ---------- 011 return/refund (ticket) ----------
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS refunded_at timestamp with time zone NULL;
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS refund_reference varchar(100) NULL;
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS refund_error varchar(500) NULL;
ALTER TABLE public.ticket_return_requests DROP CONSTRAINT IF EXISTS ck_ticket_return_requests_status;
ALTER TABLE public.ticket_return_requests
    ADD CONSTRAINT ck_ticket_return_requests_status
    CHECK (status IN ('Pending', 'Approved', 'Rejected', 'Cancelled', 'Refunded', 'RefundFailed'));
