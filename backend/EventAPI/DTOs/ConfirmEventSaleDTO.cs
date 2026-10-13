using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class ConfirmEventSaleDTO
    {
        public class ConfirmEventSaleRequestDTO
        {
            [Range(1, int.MaxValue)]
            public int OrderId { get; set; }

            [MinLength(1)]
            public List<ConfirmEventSaleItemDTO> Items { get; set; } = new();
        }

        public class ConfirmEventSaleItemDTO
        {
            [Range(1, int.MaxValue)]
            public int TicketTypeId { get; set; }

            [Range(1, int.MaxValue)]
            public int Quantity { get; set; }
        }
    }
}
