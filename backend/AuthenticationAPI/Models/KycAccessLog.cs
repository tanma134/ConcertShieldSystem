using System;
using System.Collections.Generic;

namespace AuthenticationAPI.Models;

public partial class KycAccessLog
{
    public long Id { get; set; }

    public int SubjectUserId { get; set; }

    public int? EkycId { get; set; }

    public int? ActorUserId { get; set; }

    public string ActorType { get; set; } = null!;

    public string Action { get; set; } = null!;

    public string? IpAddress { get; set; }

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; }
}
