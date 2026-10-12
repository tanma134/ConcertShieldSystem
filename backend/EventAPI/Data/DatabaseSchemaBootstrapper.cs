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
            """);
    }
}
