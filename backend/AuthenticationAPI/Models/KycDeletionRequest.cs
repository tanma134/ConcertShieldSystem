using System;
using System.Collections.Generic;

namespace AuthenticationAPI.Models;

public partial class KycDeletionRequest
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime RequestedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public int? ProcessedBy { get; set; }

    public string? Note { get; set; }

    public string? ReasonCode { get; set; }
}
