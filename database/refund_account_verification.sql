-- Add accounting verification to existing ticket_db without changing past refunds.
BEGIN;
ALTER TABLE public.ticket_return_requests
    ADD COLUMN IF NOT EXISTS bank_verification_json jsonb NULL;
COMMIT;

