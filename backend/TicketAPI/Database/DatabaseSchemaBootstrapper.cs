using Microsoft.EntityFrameworkCore;
using TicketAPI.Models;

namespace TicketAPI.Database;

public static class DatabaseSchemaBootstrapper
{
    // Create return tables in TicketAPI's DB so Report and TicketReturn share a single source of truth.
    public static async Task EnsureWeekTaskSchemaAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS public.ticket_return_requests (
                ticket_return_request_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                ticket_id integer NOT NULL,
                order_id integer NOT NULL,
                event_id integer NOT NULL,
                requester_user_id integer NOT NULL,
                reason varchar(500) NOT NULL,
                status varchar(40) NOT NULL DEFAULT 'Pending',
                refund_amount bigint NOT NULL DEFAULT 0,
                created_at timestamp with time zone NOT NULL DEFAULT now(),
                updated_at timestamp with time zone NOT NULL DEFAULT now(),
                cancelled_at timestamp with time zone NULL,
                reviewed_by integer NULL,
                reviewed_at timestamp with time zone NULL,
                review_note varchar(500) NULL,
                refunded_at timestamp with time zone NULL,
                refund_reference varchar(100) NULL,
                refund_error varchar(500) NULL,
                bank_account_name varchar(150) NULL,
                bank_account_number varchar(50) NULL,
                bank_name varchar(150) NULL,
                manual_refund_reference varchar(100) NULL,
                manual_refund_processed_by integer NULL,
                manual_refund_processed_at timestamp with time zone NULL,
                CONSTRAINT fk_ticket_return_requests_ticket FOREIGN KEY(ticket_id) REFERENCES public.tickets(ticket_id),
                CONSTRAINT ck_ticket_return_requests_status CHECK(status IN ('Pending','ApprovedPendingManualRefund','Rejected','Cancelled','Refunded','RefundFailed','RefundProcessing','RefundInfoRequired'))
            );
            ALTER TABLE public.ticket_return_requests ALTER COLUMN status TYPE varchar(40);
            ALTER TABLE public.ticket_return_requests DROP CONSTRAINT IF EXISTS ck_ticket_return_requests_status;
            ALTER TABLE public.ticket_return_requests ADD CONSTRAINT ck_ticket_return_requests_status CHECK(status IN ('Pending','ApprovedPendingManualRefund','Rejected','Cancelled','Refunded','RefundFailed','RefundProcessing','RefundInfoRequired'));
            ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS bank_account_name varchar(150) NULL;
            ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS bank_account_number varchar(50) NULL;
            ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS bank_name varchar(150) NULL;
            ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS manual_refund_reference varchar(100) NULL;
            ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS manual_refund_processed_by integer NULL;
            ALTER TABLE public.ticket_return_requests ADD COLUMN IF NOT EXISTS manual_refund_processed_at timestamp with time zone NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS uq_ticket_return_requests_open
                ON public.ticket_return_requests(ticket_id) WHERE status='Pending';
            CREATE INDEX IF NOT EXISTS ix_ticket_return_requests_requester
                ON public.ticket_return_requests(requester_user_id, created_at DESC);
            CREATE INDEX IF NOT EXISTS ix_ticket_return_requests_event
                ON public.ticket_return_requests(event_id, status);

            ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS last_exit_at timestamp with time zone NULL;
            ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS checked_in_gate varchar(100) NULL;
            ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS is_inside boolean NOT NULL DEFAULT false;
            ALTER TABLE public.tickets ADD COLUMN IF NOT EXISTS re_entry_count integer NOT NULL DEFAULT 0;
            """);
    }
}
