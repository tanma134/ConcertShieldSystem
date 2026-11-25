using Microsoft.EntityFrameworkCore;

namespace EventAPI.Data;

public static class DatabaseSchemaBootstrapper
{
    // Giữ DB local tương thích với chức năng Staff mới. Chỉ tạo schema còn thiếu, không xóa dữ liệu.
    public static async Task EnsureWeekTaskSchemaAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS public.event_staff (
                event_staff_id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                event_id integer NOT NULL REFERENCES public.events(event_id) ON DELETE CASCADE,
                staff_user_id integer NOT NULL,
                assigned_by integer NOT NULL,
                assigned_at timestamp with time zone NOT NULL DEFAULT now(),
                is_active boolean NOT NULL DEFAULT true,
                unassigned_by integer NULL,
                unassigned_at timestamp with time zone NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS uq_event_staff_active
                ON public.event_staff(event_id, staff_user_id) WHERE is_active = true;
            CREATE INDEX IF NOT EXISTS ix_event_staff_staff_user
                ON public.event_staff(staff_user_id) WHERE is_active = true;

            CREATE TABLE IF NOT EXISTS public.event_reminder_deliveries (
                event_id integer NOT NULL REFERENCES public.events(event_id) ON DELETE CASCADE,
                user_id integer NOT NULL,
                scheduled_start_at timestamp with time zone NOT NULL,
                created_at timestamp with time zone NOT NULL DEFAULT now(),
                sent_at timestamp with time zone NULL,
                attempt_count integer NOT NULL DEFAULT 0,
                next_attempt_at timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT pk_event_reminder_deliveries PRIMARY KEY (event_id, user_id, scheduled_start_at)
            );
            CREATE INDEX IF NOT EXISTS ix_event_reminder_deliveries_pending
                ON public.event_reminder_deliveries(next_attempt_at) WHERE sent_at IS NULL;

            ALTER TABLE public.events ADD COLUMN IF NOT EXISTS hold_timeout_minutes integer NOT NULL DEFAULT 10;
            ALTER TABLE public.events ADD COLUMN IF NOT EXISTS re_entry_policy character varying(30) NOT NULL DEFAULT 'NoReEntry';
            ALTER TABLE public.events ADD COLUMN IF NOT EXISTS re_entry_duration_minutes integer;
            """);
    }
}
