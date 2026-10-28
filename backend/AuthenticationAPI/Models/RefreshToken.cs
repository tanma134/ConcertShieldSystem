using System;
using System.Collections.Generic;

namespace AuthenticationAPI.Models;

public partial class RefreshToken
{
    public int RefreshTokenId { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
