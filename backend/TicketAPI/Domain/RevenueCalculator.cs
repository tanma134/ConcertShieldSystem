namespace TicketAPI.Domain;

// A paid order with its ticket lines (the data the calculator needs, nothing more).
public sealed record OrderFact(int OrderId, DateTime OrderDateUtc, long DiscountAmount, IReadOnlyList<OrderDetailFact> Details);

public sealed record OrderDetailFact(int TicketTypeId, string TicketTypeName, int Quantity, long UnitPrice);

// One ticket line of a paid order, with its share of the order discount.
public sealed record RevenueLine(
    int OrderId,
    DateTime OrderDateUtc,
    int TicketTypeId,
    string TicketTypeName,
    int Quantity,
    long GrossAmount,
    long DiscountShare);

// Money handed back for an approved ticket return.
public sealed record RefundLine(DateTime RefundedAtUtc, int TicketTypeId, long Amount);

public sealed record RevenueByDay(DateOnly Date, long Gross, long Discounts, long Refunded, long Net, int Orders, int Tickets);

public sealed record RevenueByTicketType(
    int TicketTypeId,
    string TicketTypeName,
    int Tickets,
    long Gross,
    long Discounts,
    long Refunded,
    long Net);

public sealed record RevenueSummary(
    long GrossRevenue,
    long Discounts,
    long Refunded,
    long NetRevenue,
    int OrdersCount,
    int TicketsSold,
    IReadOnlyList<RevenueByDay> ByDay,
    IReadOnlyList<RevenueByTicketType> ByTicketType);

public sealed record RangeValidation(bool IsValid, string? Error);

// Revenue maths for UC_13. Net revenue = gross - discounts - refunds.
// Pure functions: the service loads the data, this class only calculates.
public static class RevenueCalculator
{
    // The dashboard never reports on more than a year at once.
    public const int MaxRangeDays = 366;

    // Turns paid orders into ticket lines and splits each order's discount over
    // its lines in proportion to their price. The last line takes the rounding
    // remainder so the shares always add up to the order discount exactly.
    public static IReadOnlyList<RevenueLine> BuildLines(IEnumerable<OrderFact> orders)
    {
        var lines = new List<RevenueLine>();

        foreach (var order in orders)
        {
            var grossTotal = order.Details.Sum(d => d.UnitPrice * d.Quantity);
            var discount = Math.Clamp(order.DiscountAmount, 0, grossTotal);
            var allocated = 0L;

            for (var i = 0; i < order.Details.Count; i++)
            {
                var detail = order.Details[i];
                var gross = detail.UnitPrice * detail.Quantity;
                var isLast = i == order.Details.Count - 1;

                var share = isLast
                    ? discount - allocated
                    : grossTotal == 0 ? 0 : (long)((decimal)discount * gross / grossTotal);

                allocated += share;

                lines.Add(new RevenueLine(
                    order.OrderId, order.OrderDateUtc, detail.TicketTypeId, detail.TicketTypeName,
                    detail.Quantity, gross, share));
            }
        }

        return lines;
    }

    // Totals plus a per-day and per-ticket-type breakdown for the chosen filters.
    // Days are cut in local time (localOffset), and from/to are inclusive local days.
    public static RevenueSummary Summarize(
        IEnumerable<RevenueLine> lines,
        IEnumerable<RefundLine> refunds,
        DateOnly? from,
        DateOnly? to,
        int? ticketTypeId,
        TimeSpan localOffset)
    {
        var allLines = lines.ToList();
        var names = allLines
            .GroupBy(l => l.TicketTypeId)
            .ToDictionary(g => g.Key, g => g.First().TicketTypeName);

        var keptLines = allLines
            .Where(l => InFilter(l.OrderDateUtc, l.TicketTypeId, from, to, ticketTypeId, localOffset))
            .ToList();

        var keptRefunds = refunds
            .Where(r => InFilter(r.RefundedAtUtc, r.TicketTypeId, from, to, ticketTypeId, localOffset))
            .ToList();

        var gross = keptLines.Sum(l => l.GrossAmount);
        var discounts = keptLines.Sum(l => l.DiscountShare);
        var refunded = keptRefunds.Sum(r => r.Amount);

        return new RevenueSummary(
            gross,
            discounts,
            refunded,
            gross - discounts - refunded,
            keptLines.Select(l => l.OrderId).Distinct().Count(),
            keptLines.Sum(l => l.Quantity),
            BuildByDay(keptLines, keptRefunds, localOffset),
            BuildByTicketType(keptLines, keptRefunds, names));
    }

    // Checks the optional date filter sent by the dashboard.
    public static RangeValidation ValidateRange(DateOnly? from, DateOnly? to)
    {
        if (from.HasValue && to.HasValue)
        {
            if (from.Value > to.Value)
                return new RangeValidation(false, "The start date must not be after the end date.");

            if (to.Value.DayNumber - from.Value.DayNumber > MaxRangeDays)
                return new RangeValidation(false, $"The date range cannot be longer than {MaxRangeDays} days.");
        }

        return new RangeValidation(true, null);
    }

    // True when a moment and ticket type pass the dashboard filters.
    private static bool InFilter(
        DateTime utc, int ticketTypeId, DateOnly? from, DateOnly? to, int? wantedType, TimeSpan offset)
    {
        if (wantedType.HasValue && wantedType.Value != ticketTypeId)
            return false;

        var day = LocalDay(utc, offset);
        return (!from.HasValue || day >= from.Value) && (!to.HasValue || day <= to.Value);
    }

    // The calendar day a UTC moment falls on in local time.
    private static DateOnly LocalDay(DateTime utc, TimeSpan offset)
    {
        return DateOnly.FromDateTime(utc.Add(offset));
    }

    // One row per local day that has sales or refunds, oldest first.
    private static IReadOnlyList<RevenueByDay> BuildByDay(
        List<RevenueLine> lines, List<RefundLine> refunds, TimeSpan offset)
    {
        var days = lines.Select(l => LocalDay(l.OrderDateUtc, offset))
            .Concat(refunds.Select(r => LocalDay(r.RefundedAtUtc, offset)))
            .Distinct()
            .OrderBy(d => d);

        return days.Select(day =>
        {
            var dayLines = lines.Where(l => LocalDay(l.OrderDateUtc, offset) == day).ToList();
            var gross = dayLines.Sum(l => l.GrossAmount);
            var discounts = dayLines.Sum(l => l.DiscountShare);
            var refunded = refunds.Where(r => LocalDay(r.RefundedAtUtc, offset) == day).Sum(r => r.Amount);

            return new RevenueByDay(
                day, gross, discounts, refunded, gross - discounts - refunded,
                dayLines.Select(l => l.OrderId).Distinct().Count(),
                dayLines.Sum(l => l.Quantity));
        }).ToList();
    }

    // One row per ticket type, best seller first.
    private static IReadOnlyList<RevenueByTicketType> BuildByTicketType(
        List<RevenueLine> lines, List<RefundLine> refunds, Dictionary<int, string> names)
    {
        var typeIds = lines.Select(l => l.TicketTypeId).Concat(refunds.Select(r => r.TicketTypeId)).Distinct();

        return typeIds.Select(id =>
        {
            var typeLines = lines.Where(l => l.TicketTypeId == id).ToList();
            var gross = typeLines.Sum(l => l.GrossAmount);
            var discounts = typeLines.Sum(l => l.DiscountShare);
            var refunded = refunds.Where(r => r.TicketTypeId == id).Sum(r => r.Amount);

            return new RevenueByTicketType(
                id,
                names.TryGetValue(id, out var name) ? name : $"Ticket type {id}",
                typeLines.Sum(l => l.Quantity),
                gross, discounts, refunded, gross - discounts - refunded);
        })
        .OrderByDescending(t => t.Net)
        .ThenBy(t => t.TicketTypeName)
        .ToList();
    }
}
