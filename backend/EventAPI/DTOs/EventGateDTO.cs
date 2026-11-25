using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class EventGateDTO
    {
        public int GateId { get; set; }
        public int EventId { get; set; }
        public string GateName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AllowedTicketTypes { get; set; }
        public List<string> AllowedTicketTypeList { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class CreateEventGateDTO
    {
        [Required(ErrorMessage = "Gate name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Gate name must be between 2 and 100 characters.")]
        public string GateName { get; set; } = null!;

        [StringLength(255)]
        public string? Description { get; set; }

        /// <summary>
        /// Comma-separated list of allowed ticket types or IDs, or "ALL"
        /// </summary>
        public string? AllowedTicketTypes { get; set; }

        public List<string>? AllowedTicketTypeList { get; set; }
    }

    public class UpdateEventGateDTO
    {
        [Required(ErrorMessage = "Gate name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Gate name must be between 2 and 100 characters.")]
        public string GateName { get; set; } = null!;

        [StringLength(255)]
        public string? Description { get; set; }

        public string? AllowedTicketTypes { get; set; }

        public List<string>? AllowedTicketTypeList { get; set; }
    }
}
