using System.ComponentModel.DataAnnotations;

namespace TicketAPI.DTOs
{
    public class CreateOrderDetailsDTO
    {
        public int TicketTypeId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public int Quantity { get; set; }
        public List<int> SeatIds { get; set; } = new();
    }
}
