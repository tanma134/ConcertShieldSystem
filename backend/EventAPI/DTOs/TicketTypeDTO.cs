using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class CreateTicketTypeDTO
    {
        [Required]
        [MaxLength(100)]
        public string TypeName { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public long Price { get; set; }

        public long? OriginalPrice { get; set; }

        [Required]
        public int Quantity { get; set; }

        public int MinPerOrder { get; set; } = 1;
        public int MaxPerOrder { get; set; } = 10;

        [MaxLength(20)]
        public string? ColorCode { get; set; }

        public int SortOrder { get; set; } = 0;

        public DateTime? SalesStartsAt { get; set; }
        public DateTime? SalesEndsAt { get; set; }
    }

    public class UpdateTicketTypeDTO
    {
        [Required]
        public int TicketTypeId { get; set; }

        [MaxLength(100)]
        public string? TypeName { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public long? Price { get; set; }
        public long? OriginalPrice { get; set; }
        public int? Quantity { get; set; }
        public int? MinPerOrder { get; set; }
        public int? MaxPerOrder { get; set; }

        [MaxLength(20)]
        public string? ColorCode { get; set; }

        public int? SortOrder { get; set; }
        public string? Status { get; set; }

        public DateTime? SalesStartsAt { get; set; }
        public DateTime? SalesEndsAt { get; set; }
    }

    public class DeleteTicketTypeDTO
    {
        [Required]
        public int TicketTypeId { get; set; }
    }

    // Inventory reserve/release seam for a future Booking/Payment service: hold created
    // -> reserve; hold timeout, payment failure, refund, or event cancellation -> release.
    // EventAPI does not know about orders/holds/payments — it only guarantees the count
    // never exceeds Quantity and never goes negative.

    public class ReserveInventoryDTO
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
        public int Quantity { get; set; }
    }

    public class InventoryOperationResultDTO
    {
        public bool Success { get; set; }
        public int TicketTypeId { get; set; }
        public int AvailableQuantity { get; set; }
        public string? Reason { get; set; }
    }

    public class TicketTypeResponseDTO
    {
        public int TicketTypeId { get; set; }
        public int EventId { get; set; }
        public string TypeName { get; set; } = null!;
        public string? Description { get; set; }
        public long Price { get; set; }
        public long? OriginalPrice { get; set; }
        public int Quantity { get; set; }
        public int SoldQuantity { get; set; }
        public int AvailableQuantity => Quantity - SoldQuantity;
        public int MinPerOrder { get; set; }
        public int MaxPerOrder { get; set; }
        public string? ColorCode { get; set; }
        public int SortOrder { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? SalesStartsAt { get; set; }
        public DateTime? SalesEndsAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
