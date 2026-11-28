namespace EventAPI.Common;

public static class TicketSalesWindow
{
    public static DateTime EndsAt(DateTime? explicitEnd, DateTime concertStart) =>
        explicitEnd ?? concertStart.AddHours(-5);
}
