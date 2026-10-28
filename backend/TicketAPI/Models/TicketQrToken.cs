using System;
using System.Collections.Generic;

namespace TicketAPI.Models;

public partial class TicketQrToken
{
    public int TicketQrTokenId { get; set; }

    public int TicketId { get; set; }

    public string QrToken { get; set; } = null!;

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool IsUsed { get; set; }

    public DateTime? UsedAt { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;
}
