namespace TicketAPI.DTOs
{
    public class ConfirmEventSaleDto
    {
        public class ConfirmEventSaleRequestDto
        {
            public int OrderId { get; set; }
            public List<ConfirmEventSaleItemDto> Items { get; set; } = new();
        }

        public class ConfirmEventSaleItemDto
        {
            public int TicketTypeId { get; set; }
            public int Quantity { get; set; }
        }
    }
}
