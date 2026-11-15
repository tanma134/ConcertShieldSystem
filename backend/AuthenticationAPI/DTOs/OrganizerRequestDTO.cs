using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AuthenticationAPI.DTOs
{
    public class CreateOrganizerRequestDTO
    {
        [Required(ErrorMessage = "Reason is required.")]
        [MaxLength(1000)]
        public string Reason { get; set; } = null!;

        [Required(ErrorMessage = "Company name is required.")]
        [MaxLength(200)]
        public string CompanyName { get; set; } = null!;

        [Required(ErrorMessage = "Tax code is required.")]
        [MaxLength(50)]
        public string TaxCode { get; set; } = null!;

        [Required(ErrorMessage = "Legal representative name is required.")]
        [MaxLength(200)]
        public string RepresentativeName { get; set; } = null!;

        [MaxLength(255)]
        public string? Website { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(2000)]
        public string? Experience { get; set; }

        [Required(ErrorMessage = "Business registration license is required.")]
        public IFormFile BusinessLicenseFile { get; set; } = null!;

        [Required(ErrorMessage = "Representative ID / passport is required.")]
        public IFormFile RepresentativeIdFile { get; set; } = null!;

        public IFormFile? PortfolioFile { get; set; }
    }

    public class ReviewOrganizerRequestDTO
    {
        [Required]
        public string Status { get; set; } = null!;

        [MaxLength(1000)]
        public string? ReviewNote { get; set; }

        [MaxLength(1000)]
        public string? RejectionReason { get; set; }
    }

    public class OrganizerRequestDocumentDTO
    {
        public int DocumentId { get; set; }
        public int RequestId { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string? PreviewUrl { get; set; }
        public bool IsImage { get; set; } = true;
        public DateTime UploadedAt { get; set; }
    }

    public class OrganizerRequestResponseDTO
    {
        public int RequestId { get; set; }
        public int UserId { get; set; }
        public string UserEmail { get; set; } = null!;
        public string UserFullName { get; set; } = null!;

        public string Reason { get; set; } = null!;
        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; }
        public string? RepresentativeName { get; set; }
        public string? Website { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Experience { get; set; }

        public string Status { get; set; } = null!;
        public int? ReviewedBy { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public int DocumentsCount { get; set; }
        public List<OrganizerRequestDocumentDTO> Documents { get; set; } = new();
    }
}
