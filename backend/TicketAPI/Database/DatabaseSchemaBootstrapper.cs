using Microsoft.EntityFrameworkCore;
using TicketAPI.Models;

namespace TicketAPI.Database;

public static class DatabaseSchemaBootstrapper
{
    // Tạo bảng return trong DB mà TicketAPI đang sở hữu để Report/TicketReturn dùng cùng một source of truth.
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
                status varchar(20) NOT NULL DEFAULT 'Pending',
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
                CONSTRAINT fk_ticket_return_requests_ticket FOREIGN KEY(ticket_id) REFERENCES public.tickets(ticket_id),
                CONSTRAINT ck_ticket_return_requests_status CHECK(status IN ('Pending','Approved','Rejected','Cancelled','Refunded','RefundFailed'))
            );
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

            CREATE TABLE IF NOT EXISTS public.status_histories (
                status_history_id integer NOT NULL GENERATED ALWAYS AS IDENTITY,
                target_type character varying(30) NOT NULL,
                target_id integer NOT NULL,
                event_id integer NOT NULL,
                order_id integer NULL,
                ticket_id integer NULL,
                ticket_type_id integer NULL,
                seat_id integer NULL,
                hold_id character varying(255) NULL,
                customer_id integer NULL,
                action character varying(50) NOT NULL,
                old_status character varying(30) NULL,
                new_status character varying(30) NULL,
                quantity integer NULL,
                actor_user_id integer NULL,
                actor_role character varying(30) NULL,
                reason text NULL,
                created_at timestamp with time zone NOT NULL DEFAULT now(),
                metadata jsonb NOT NULL DEFAULT '{{}}'::jsonb,
                CONSTRAINT status_histories_pkey PRIMARY KEY (status_history_id),
                CONSTRAINT fk_status_histories_orders FOREIGN KEY (order_id) REFERENCES public.orders (order_id) ON DELETE RESTRICT,
                CONSTRAINT fk_status_histories_tickets FOREIGN KEY (ticket_id) REFERENCES public.tickets (ticket_id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_status_histories_customer_created ON public.status_histories (customer_id, created_at);
            CREATE INDEX IF NOT EXISTS ix_status_histories_event_created ON public.status_histories (event_id, created_at);
            CREATE INDEX IF NOT EXISTS ix_status_histories_target_created ON public.status_histories (target_type, target_id, created_at);
            CREATE INDEX IF NOT EXISTS ix_status_histories_order_created ON public.status_histories (order_id, created_at);
            CREATE INDEX IF NOT EXISTS ix_status_histories_ticket_created ON public.status_histories (ticket_id, created_at);
            """);
    }
}
