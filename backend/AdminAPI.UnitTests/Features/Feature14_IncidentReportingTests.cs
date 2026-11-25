using System;
using System.Threading.Tasks;
using IncidentAPI.Data;
using IncidentAPI.DTOs;
using IncidentAPI.Models;
using IncidentAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AdminAPI.UnitTests.Features;

/// <summary>
/// Feature 14: Incident Reporting & Operations Support
/// Tests for UC_3.15.1 (Report On-site Incident), UC_3.15.2 (Attach Evidence),
/// UC_3.15.3 (Update Incident Report), UC_3.15.4 (Escalate Incident to Admin),
/// UC_3.15.5 (View Resolution)
/// </summary>
public class Feature14_IncidentReportingTests
{
    private IncidentDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IncidentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new IncidentDbContext(options);
    }

    [Fact]
    public async Task UC_3_15_1_ReportOnSiteIncident_CreatesIncidentWithReportedStatus()
    {
        using var db = CreateInMemoryDbContext();
        var service = new IncidentService(db, NullLogger<IncidentService>.Instance);

        var request = new CreateIncidentRequest
        {
            EventId = 10,
            EventName = "Live Arena Tour 2026",
            Title = "Ticket Scanner Malfunction at Gate B",
            Category = "Technical",
            Severity = "High",
            Location = "Gate B - North Turnstile",
            Description = "All 3 handheld barcode scanners suddenly dropped Wi-Fi connectivity."
        };

        var result = await service.CreateIncidentAsync(request, 201, "GateStaffJohn");

        Assert.NotNull(result);
        Assert.Equal("Reported", result.Status);
        Assert.Equal("High", result.Severity);
        Assert.False(result.IsEscalated);
    }

    [Fact]
    public async Task UC_3_15_2_AttachEvidence_LinksEvidenceToIncident()
    {
        using var db = CreateInMemoryDbContext();
        var incident = new Incident
        {
            Id = 1,
            EventId = 10,
            ReporterId = 201,
            ReporterName = "Staff One",
            Title = "Counterfeit Wristband Detected",
            Category = "Security",
            Severity = "Critical",
            Location = "Main Gate",
            Description = "Patron presented forged wristband.",
            Status = "Reported"
        };
        db.Incidents.Add(incident);

        var evidence = new IncidentEvidence
        {
            Id = 1,
            IncidentId = 1,
            PublicId = "cloudinary_ev_01",
            FileName = "counterfeit_band_photo.jpg",
            FileUrl = "https://cdn.concertshield.vn/evidence/c_photo1.jpg",
            FileType = "image",
            MimeType = "image/jpeg",
            FileSize = 1024500,
            UploadedById = 201,
            UploadedByName = "Staff One",
            UploadedAt = DateTime.UtcNow
        };
        db.IncidentEvidences.Add(evidence);
        await db.SaveChangesAsync();

        var attachedEvidence = await db.IncidentEvidences.FirstOrDefaultAsync(e => e.IncidentId == 1);
        Assert.NotNull(attachedEvidence);
        Assert.Equal("counterfeit_band_photo.jpg", attachedEvidence.FileName);
    }

    [Fact]
    public async Task UC_3_15_3_UpdateIncidentReport_AppendsUpdateHistory()
    {
        using var db = CreateInMemoryDbContext();
        var service = new IncidentService(db, NullLogger<IncidentService>.Instance);

        var incident = new Incident
        {
            Id = 2,
            EventId = 10,
            ReporterId = 201,
            ReporterName = "Staff One",
            Title = "Minor Gate Scuffle",
            Category = "Security",
            Severity = "Medium",
            Location = "Gate A",
            Description = "Crowd jostling in line.",
            Status = "Reported"
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        var updateReq = new UpdateStatusRequest
        {
            Status = "Investigating",
            Note = "Security team arrived on scene to manage queue barricades."
        };

        var result = await service.UpdateStatusAsync(2, updateReq, 201, "Staff One", false);

        Assert.Equal("Investigating", result.Status);
        var updates = await db.IncidentUpdates.Where(u => u.IncidentId == 2).ToListAsync();
        Assert.NotEmpty(updates);
    }

    [Fact]
    public async Task UC_3_15_4_EscalateIncidentToAdmin_SetsIsEscalatedTrue()
    {
        using var db = CreateInMemoryDbContext();
        var service = new IncidentService(db, NullLogger<IncidentService>.Instance);

        var incident = new Incident
        {
            Id = 3,
            EventId = 10,
            ReporterId = 201,
            ReporterName = "Staff One",
            Title = "Power Failure in Zone C",
            Category = "Facility",
            Severity = "Critical",
            Location = "Zone C Audio Booth",
            Description = "Secondary generator failed to ignite.",
            Status = "Investigating"
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        var escalateReq = new EscalateIncidentRequest
        {
            Reason = "Facility engineers unable to restore power. Emergency Admin intervention needed.",
            AdminId = 1
        };

        var result = await service.EscalateIncidentAsync(3, escalateReq, 201, "Staff One");

        Assert.True(result.IsEscalated);
        Assert.Equal("Escalated", result.Status);
        Assert.NotNull(result.EscalatedAt);
    }

    [Fact]
    public async Task UC_3_15_5_ViewResolution_ResolvesIncidentAndRecordsSummary()
    {
        using var db = CreateInMemoryDbContext();
        var service = new IncidentService(db, NullLogger<IncidentService>.Instance);

        var incident = new Incident
        {
            Id = 4,
            EventId = 10,
            ReporterId = 201,
            ReporterName = "Staff One",
            Title = "Turnstile Motor Jammed",
            Category = "Technical",
            Severity = "High",
            Location = "Gate 3 Turnstile",
            Description = "Motor locked due to mechanical wear.",
            Status = "Escalated",
            IsEscalated = true
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        var resolveReq = new ResolveIncidentRequest
        {
            ResolutionSummary = "Backup turnstile #4 activated. Maintenance deployed spare gear motor.",
            ActionTaken = "Turnstile #4 fully cleared and operational for remaining attendees."
        };

        var result = await service.ResolveIncidentAsync(4, resolveReq, 1, "AdminSuper");

        Assert.Equal("Resolved", result.Status);
        Assert.Equal("AdminSuper", result.ResolvedByName);
        Assert.NotNull(result.ResolvedAt);
    }
}
