namespace EventAPI.Common;

public static class PricingScheduleRules
{
    // Kiểm tra toàn bộ timestamp; khoảng giảm giá phải nằm trong lịch bán và trước concert.
    // Khi chỉ sửa tên/tắt rule cũ, không bắt ngày bắt đầu cũ phải trở lại tương lai.
    public static void Validate(string type, DateTime? from, DateTime? to,
        DateTime? salesFrom, DateTime? salesTo, DateTime concertStart, DateTime now, bool requireFutureStart)
    {
        if (type == "QuantityBased") return;
        if (!from.HasValue || !to.HasValue)
            throw new InvalidOperationException("Time-based discounts require both start and end date/time.");
        if (requireFutureStart && from.Value <= now)
            throw new InvalidOperationException("Discount start date and time must be later than now.");
        if (to.Value <= from.Value)
            throw new InvalidOperationException("Discount end date and time must be after its start.");
        if (salesFrom.HasValue && from.Value < salesFrom.Value)
            throw new InvalidOperationException("Discount cannot start before ticket sales open.");
        if (salesTo.HasValue && to.Value > salesTo.Value)
            throw new InvalidOperationException("Discount cannot end after ticket sales close.");
        if (to.Value > concertStart)
            throw new InvalidOperationException("Discount must end by the concert start time.");
    }
}
