namespace TicketAPI.DTOs
{
    public class AdminTicketDtos
    {
        public class AdminTicketQueryDto
        {
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 20;
            public int? TicketId { get; set; }
            public int? OrderId { get; set; }
            public int? EventId { get; set; }
            public string? Status { get; set; }
            public string? Search { get; set; }
        }

        public class AdminTicketPageDto
        {
            public List<MyTicketResponseDto> Items { get; set; } = new();
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalCount { get; set; }
        }

        public class ReissueTicketRequestDto
        {
            [System.ComponentModel.DataAnnotations.Required]
            [System.ComponentModel.DataAnnotations.StringLength(500, MinimumLength = 1)]
            public string Reason { get; set; } = string.Empty;
        }
    }
}
