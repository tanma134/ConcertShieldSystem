using System;
using System.Collections.Generic;

namespace AuthenticationAPI.Models;

public partial class KycSetting
{
    public int Id { get; set; }

    public int RetentionDays { get; set; }

    public bool KeepDocumentHashAfterDeletion { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }
}
