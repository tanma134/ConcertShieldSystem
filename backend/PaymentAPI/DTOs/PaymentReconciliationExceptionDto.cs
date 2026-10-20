namespace PaymentAPI.DTOs
{
    public class PaymentReconciliationExceptionDto
    {
        public int PaymentTransactionId { get; set; }
        public int OrderId { get; set; }
        public int? EventId { get; set; }

        public string Gateway { get; set; } = null!;
        public string? GatewayTransactionRef { get; set; }

        public long PaymentAmount { get; set; }
        public long? OrderAmount { get; set; }

        public string PaymentStatus { get; set; } = null!;
        public string? OrderStatus { get; set; }

        public List<string> Reasons { get; set; } = new();

        public DateTime RequestedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
        public int ExpectedTicketCount { get; set; }
        public int IssuedTicketCount { get; set; }
        public int TicketsWithoutQrCount { get; set; }
    }

    public class PaymentReconciliationExceptionPageDto
    {
        public List<PaymentReconciliationExceptionDto> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }
}
