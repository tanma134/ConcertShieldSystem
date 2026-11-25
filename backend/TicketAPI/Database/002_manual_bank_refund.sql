ALTER TABLE public.ticket_return_requests ALTER COLUMN status TYPE varchar(40);
ALTER TABLE public.ticket_return_requests DROP CONSTRAINT IF EXISTS ck_ticket_return_requests_status;
ALTER TABLE public.ticket_return_requests ADD CONSTRAINT ck_ticket_return_requests_status CHECK (status IN ('Pending','ApprovedPendingManualRefund','Rejected','Cancelled','Refunded','RefundFailed','RefundProcessing','RefundInfoRequired'));
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS bank_account_name varchar(150);
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS bank_account_number varchar(50);
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS bank_name varchar(150);
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS manual_refund_reference varchar(100);
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS manual_refund_processed_by integer;
ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS manual_refund_processed_at timestamptz;
