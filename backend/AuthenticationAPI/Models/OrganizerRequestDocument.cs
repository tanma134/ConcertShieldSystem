using System;

namespace AuthenticationAPI.Models;

public partial class OrganizerRequestDocument
{
    public int DocumentId { get; set; }

    public int RequestId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public string? PreviewUrl { get; set; }

    public string? PublicId { get; set; }

    public bool IsImage { get; set; } = true;

    public DateTime UploadedAt { get; set; }

    public virtual OrganizerRequest Request { get; set; } = null!;
}
