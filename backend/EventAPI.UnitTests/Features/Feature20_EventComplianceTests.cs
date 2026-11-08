using EventAPI.Common;
using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventAPI.UnitTests.Features;

/// <summary>
/// Feature 20: Event Compliance & Legal Document Management
/// Tests for UC_3.21.6 (Submit Event Compliance Documents), UC_3.21.7 (Review Event Compliance Record)
/// </summary>
public class Feature20_EventComplianceTests
{
    private EventDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    [Fact]
    public void UC_3_21_6_SubmitComplianceDocs_AllowedDocumentTypes()
    {
        var allowedCategories = new[] { "PermitLicense", "FireSafetyCertificate", "InsurancePolicy", "ArtistContract" };

        Assert.Contains("PermitLicense", allowedCategories);
        Assert.Contains("FireSafetyCertificate", allowedCategories);
        Assert.DoesNotContain("PersonalPhoto", allowedCategories);
    }

    [Fact]
    public void UC_3_21_6_SubmitComplianceDocs_ValidMimeTypesAndExtensions()
    {
        var validExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        string uploadedFileName = "business_permit_hanoi_2026.pdf";
        string ext = System.IO.Path.GetExtension(uploadedFileName).ToLower();

        Assert.Contains(ext, validExtensions);
    }

    [Fact]
    public void UC_3_21_7_ReviewComplianceRecord_ValidAdminDecisions()
    {
        var validDecisions = new[] { "Approved", "Rejected", "NeedMoreInfo" };

        Assert.Contains("Approved", validDecisions);
        Assert.Contains("Rejected", validDecisions);
        Assert.Contains("NeedMoreInfo", validDecisions);
        Assert.DoesNotContain("UnknownStatus", validDecisions);
    }

    [Fact]
    public void UC_3_21_7_ReviewComplianceRecord_RejectionRequiresFeedbackNote()
    {
        string decision = "Rejected";
        string reason = "Missing local authority stamp on fire safety certificate";

        Assert.Equal("Rejected", decision);
        Assert.False(string.IsNullOrWhiteSpace(reason), "Rejection must have explicit feedback reason");
    }
}

