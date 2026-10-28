using System;
using System.Collections.Generic;

namespace AuthenticationAPI.Models;

public partial class KycConsentVersion
{
    public int Id { get; set; }

    public string Version { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string ContentJson { get; set; } = null!;

    public string CheckboxText { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? CreatedBy { get; set; }
}
