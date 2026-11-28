using AdminAPI.DTOs;
using AdminAPI.Repositories;
using AdminAPI.Services;
using Moq;
using Xunit;

namespace AdminAPI.Tests.Services;

public class SystemPolicyServiceTests
{
    private static List<SystemPolicyRecord> CreateSeededPolicies(string category = "PRICE_TICKETING", int count = 3)
    {
        var list = new List<SystemPolicyRecord>();
        for (int i = 1; i <= count; i++)
        {
            list.Add(new SystemPolicyRecord(
                PolicyId: 100 + i,
                Category: category,
                VersionCode: $"{i}.0.0",
                VersionNumber: i,
                Title: $"Policy {category} v{i}",
                Scope: "All Platform Attendees & Event Organizers",
                ChangeSummary: $"Change summary v{i}",
                Content: "{\"feePercent\": 5, \"cutoffHours\": 24}",
                Status: i == count ? "ACTIVE" : "ARCHIVED",
                EffectiveDate: DateTime.UtcNow.AddDays(-10 + i),
                CreatedAt: DateTime.UtcNow.AddDays(-10 + i),
                CreatedByAdminId: 1,
                CreatedByAdminName: "Admin",
                PlatformFeePercent: 5.0m,
                PayoutDays: 7));
        }
        return list;
    }

    // =========================================================================
    // Function_160_ViewPolicyHistory (GetPolicyVersions / GetPolicyVersionsAsync)
    // =========================================================================

    [Fact]
    public async Task ViewPolicyVersionHistory_UTCID01_AllCategoriesAndStatuses_ReturnsPagedPolicyVersions()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetHistoryAsync("PRICE_TICKETING", default))
            .ReturnsAsync(CreateSeededPolicies("PRICE_TICKETING", 3));
        var service = new SystemPolicyService(repository.Object);

        var result = await service.GetPolicyVersionsAsync(
            category: null,
            status: null,
            page: 1,
            pageSize: 10,
            callerRole: "Admin");

        Assert.NotNull(result);
        Assert.True(result.Items.Count > 0);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task ViewPolicyVersionHistory_UTCID02_FilterByRefundAndActiveStatus_ReturnsMatchingVersions()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetHistoryAsync("REFUND_CANCELLATION", default))
            .ReturnsAsync(CreateSeededPolicies("REFUND_CANCELLATION", 3));
        var service = new SystemPolicyService(repository.Object);

        var result = await service.GetPolicyVersionsAsync(
            category: "REFUND",
            status: "ACTIVE",
            page: 1,
            pageSize: 10,
            callerRole: "Admin");

        Assert.NotNull(result);
        var item = Assert.Single(result.Items);
        Assert.Equal("REFUND_CANCELLATION", item.Category);
        Assert.Equal("ACTIVE", item.Status);
    }

    [Fact]
    public async Task ViewPolicyVersionHistory_UTCID03_FilterByRiskCategory_ReturnsRiskPolicyVersions()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetHistoryAsync("RISK_ANTI_FRAUD", default))
            .ReturnsAsync(CreateSeededPolicies("RISK_ANTI_FRAUD", 2));
        var service = new SystemPolicyService(repository.Object);

        var result = await service.GetPolicyVersionsAsync(
            category: "RISK",
            status: null,
            page: 1,
            pageSize: 10,
            callerRole: "Admin");

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, p => Assert.Equal("RISK_ANTI_FRAUD", p.Category));
    }

    [Fact]
    public async Task ViewPolicyVersionHistory_UTCID04_BoundaryPageSizeMin1_ReturnsSingleItem()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetHistoryAsync("PRICE_TICKETING", default))
            .ReturnsAsync(CreateSeededPolicies("PRICE_TICKETING", 3));
        var service = new SystemPolicyService(repository.Object);

        var result = await service.GetPolicyVersionsAsync(
            category: null,
            status: null,
            page: 1,
            pageSize: 1,
            callerRole: "Admin");

        Assert.Single(result.Items);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task ViewPolicyVersionHistory_UTCID05_BoundaryPageSizeMax50_ReturnsItems()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetHistoryAsync("PRICE_TICKETING", default))
            .ReturnsAsync(CreateSeededPolicies("PRICE_TICKETING", 5));
        var service = new SystemPolicyService(repository.Object);

        var result = await service.GetPolicyVersionsAsync(
            category: null,
            status: null,
            page: 1,
            pageSize: 50,
            callerRole: "Admin");

        Assert.Equal(5, result.Items.Count);
        Assert.Equal(50, result.PageSize);
    }

    [Fact]
    public async Task ViewPolicyVersionHistory_UTCID06_InvalidCategory_ThrowsArgumentException()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        var service = new SystemPolicyService(repository.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetPolicyVersionsAsync(
                category: "INVALID_CAT",
                status: null,
                page: 1,
                pageSize: 10,
                callerRole: "Admin"));

        Assert.Equal("Invalid policy category", ex.Message);
    }

    [Fact]
    public async Task ViewPolicyVersionHistory_UTCID07_BoundaryPageZero_ThrowsArgumentException()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        var service = new SystemPolicyService(repository.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetPolicyVersionsAsync(
                category: null,
                status: null,
                page: 0,
                pageSize: 10,
                callerRole: "Admin"));

        Assert.Equal("Page must be >= 1", ex.Message);
    }

    [Fact]
    public async Task ViewPolicyVersionHistory_UTCID08_AbnormalNegativePage_ThrowsArgumentException()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        var service = new SystemPolicyService(repository.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetPolicyVersionsAsync(
                category: null,
                status: null,
                page: -1,
                pageSize: 10,
                callerRole: "Admin"));

        Assert.Equal("Page must be >= 1", ex.Message);
    }

    [Fact]
    public async Task ViewPolicyVersionHistory_UTCID09_CustomerRole_ThrowsUnauthorizedAccessException()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        var service = new SystemPolicyService(repository.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetPolicyVersionsAsync(
                category: null,
                status: null,
                page: 1,
                pageSize: 10,
                callerRole: "Customer"));

        Assert.Equal("Admin access required", ex.Message);
    }

    // =========================================================================
    // Function_161_CreatePolicyVer (CreatePolicyVersion / CreatePolicyVersionAsync)
    // =========================================================================

    [Fact]
    public async Task CreatePolicyVersion_UTCID01_ValidRefundPolicyFutureEffectiveDate_CreatesActiveVersion()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetHistoryAsync("REFUND_CANCELLATION", default))
            .ReturnsAsync(CreateSeededPolicies("REFUND_CANCELLATION", 2));
        repository.Setup(r => r.GetNextVersionNumberAsync("REFUND_CANCELLATION", default)).ReturnsAsync(3);
        repository.Setup(r => r.PublishAsync(It.IsAny<NewSystemPolicyVersion>(), default))
            .ReturnsAsync((101, DateTime.UtcNow));
        var service = new SystemPolicyService(repository.Object);

        var result = await service.CreatePolicyVersionAsync(new CreatePolicyVersionRequestDTO
        {
            Category = "REFUND",
            VersionNumber = "2.1.0",
            RulesJson = "{\"feePercent\": 5, \"cutoffHours\": 24}",
            EffectiveDate = DateTime.UtcNow.AddDays(7)
        }, adminId: 1, adminName: "Admin", callerRole: "Admin");

        Assert.True(result.PolicyId > 0);
        Assert.Equal("REFUND_CANCELLATION", result.Category);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task CreatePolicyVersion_UTCID02_ValidRiskPolicy_CreatesActiveVersion()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetHistoryAsync("RISK_ANTI_FRAUD", default))
            .ReturnsAsync(new List<SystemPolicyRecord>());
        repository.Setup(r => r.GetNextVersionNumberAsync("RISK_ANTI_FRAUD", default)).ReturnsAsync(2);
        repository.Setup(r => r.PublishAsync(It.IsAny<NewSystemPolicyVersion>(), default))
            .ReturnsAsync((102, DateTime.UtcNow));
        var service = new SystemPolicyService(repository.Object);

        var result = await service.CreatePolicyVersionAsync(new CreatePolicyVersionRequestDTO
        {
            Category = "RISK",
            VersionNumber = "1.5.0",
            RulesJson = "{\"feePercent\": 5, \"cutoffHours\": 24}",
            EffectiveDate = DateTime.UtcNow.AddDays(7)
        }, adminId: 1, adminName: "Admin", callerRole: "Admin");

        Assert.True(result.PolicyId > 0);
        Assert.Equal("RISK_ANTI_FRAUD", result.Category);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task CreatePolicyVersion_UTCID03_BoundaryImmediateEffectiveDate_CreatesActiveVersion()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetHistoryAsync("REFUND_CANCELLATION", default))
            .ReturnsAsync(new List<SystemPolicyRecord>());
        repository.Setup(r => r.GetNextVersionNumberAsync("REFUND_CANCELLATION", default)).ReturnsAsync(3);
        repository.Setup(r => r.PublishAsync(It.IsAny<NewSystemPolicyVersion>(), default))
            .ReturnsAsync((103, DateTime.UtcNow));
        var service = new SystemPolicyService(repository.Object);

        var result = await service.CreatePolicyVersionAsync(new CreatePolicyVersionRequestDTO
        {
            Category = "REFUND",
            VersionNumber = "3.0.0",
            RulesJson = "{\"feePercent\": 5, \"cutoffHours\": 24}",
            EffectiveDate = DateTime.UtcNow
        }, adminId: 1, adminName: "Admin", callerRole: "Admin");

        Assert.True(result.PolicyId > 0);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task CreatePolicyVersion_UTCID04_BoundaryMinimalValidJson_CreatesActiveVersion()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetHistoryAsync("RISK_ANTI_FRAUD", default))
            .ReturnsAsync(new List<SystemPolicyRecord>());
        repository.Setup(r => r.GetNextVersionNumberAsync("RISK_ANTI_FRAUD", default)).ReturnsAsync(3);
        repository.Setup(r => r.PublishAsync(It.IsAny<NewSystemPolicyVersion>(), default))
            .ReturnsAsync((104, DateTime.UtcNow));
        var service = new SystemPolicyService(repository.Object);

        var result = await service.CreatePolicyVersionAsync(new CreatePolicyVersionRequestDTO
        {
            Category = "RISK",
            VersionNumber = "3.0.0",
            RulesJson = "{\"k\":1}",
            EffectiveDate = DateTime.UtcNow.AddDays(7)
        }, adminId: 1, adminName: "Admin", callerRole: "Admin");

        Assert.True(result.PolicyId > 0);
        Assert.Equal("{\"k\":1}", result.Content);
    }

    [Fact]
    public async Task CreatePolicyVersion_UTCID05_DuplicateExistingVersionNumber_ThrowsInvalidOperationException()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetHistoryAsync("REFUND_CANCELLATION", default))
            .ReturnsAsync(CreateSeededPolicies("REFUND_CANCELLATION", 2));
        var service = new SystemPolicyService(repository.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePolicyVersionAsync(new CreatePolicyVersionRequestDTO
            {
                Category = "REFUND",
                VersionNumber = "2.0.0",
                RulesJson = "{\"feePercent\": 5, \"cutoffHours\": 24}",
                EffectiveDate = DateTime.UtcNow.AddDays(7)
            }, adminId: 1, adminName: "Admin", callerRole: "Admin"));

        Assert.Equal("Policy version already exists", ex.Message);
    }

    [Fact]
    public async Task CreatePolicyVersion_UTCID06_EmptyVersionNumber_ThrowsArgumentException()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        var service = new SystemPolicyService(repository.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePolicyVersionAsync(new CreatePolicyVersionRequestDTO
            {
                Category = "REFUND",
                VersionNumber = "",
                RulesJson = "{\"feePercent\": 5, \"cutoffHours\": 24}",
                EffectiveDate = DateTime.UtcNow.AddDays(7)
            }, adminId: 1, adminName: "Admin", callerRole: "Admin"));

        Assert.Equal("Version number is required", ex.Message);
    }

    [Fact]
    public async Task CreatePolicyVersion_UTCID07_InvalidRulesJsonSyntax_ThrowsArgumentException()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        var service = new SystemPolicyService(repository.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePolicyVersionAsync(new CreatePolicyVersionRequestDTO
            {
                Category = "REFUND",
                VersionNumber = "2.2.0",
                RulesJson = "{feePercent: 5}",
                EffectiveDate = DateTime.UtcNow.AddDays(7)
            }, adminId: 1, adminName: "Admin", callerRole: "Admin"));

        Assert.Equal("RulesJson must be valid JSON", ex.Message);
    }

    [Fact]
    public async Task CreatePolicyVersion_UTCID08_PastEffectiveDate_ThrowsArgumentException()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        var service = new SystemPolicyService(repository.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePolicyVersionAsync(new CreatePolicyVersionRequestDTO
            {
                Category = "REFUND",
                VersionNumber = "2.2.0",
                RulesJson = "{\"feePercent\": 5, \"cutoffHours\": 24}",
                EffectiveDate = DateTime.UtcNow.AddDays(-5)
            }, adminId: 1, adminName: "Admin", callerRole: "Admin"));

        Assert.Equal("EffectiveDate cannot be in the past", ex.Message);
    }

    [Fact]
    public async Task CreatePolicyVersion_UTCID09_NonAdminOrganizerRole_ThrowsUnauthorizedAccessException()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        var service = new SystemPolicyService(repository.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreatePolicyVersionAsync(new CreatePolicyVersionRequestDTO
            {
                Category = "REFUND",
                VersionNumber = "2.2.0",
                RulesJson = "{\"feePercent\": 5, \"cutoffHours\": 24}",
                EffectiveDate = DateTime.UtcNow.AddDays(7)
            }, adminId: 10, adminName: "Organizer", callerRole: "Organizer"));

        Assert.Equal("Admin role required", ex.Message);
    }

    // =========================================================================
    // Existing Regression Tests
    // =========================================================================

    [Fact]
    public async Task Create_PreservesPricingTermsThroughRepositoryRefactor()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        NewSystemPolicyVersion? published = null;
        repository.Setup(r => r.GetNextVersionNumberAsync("PRICE_TICKETING", default)).ReturnsAsync(3);
        repository.Setup(r => r.PublishAsync(It.IsAny<NewSystemPolicyVersion>(), default))
            .Callback<NewSystemPolicyVersion, CancellationToken>((version, _) => published = version)
            .ReturnsAsync((42, DateTime.UtcNow));
        var service = new SystemPolicyService(repository.Object);

        var result = await service.CreateAsync(new CreatePolicyVersionRequestDTO
        {
            Title = "Updated terms", ChangeSummary = "Pricing update", Content = "Terms",
            PlatformFeePercent = 8.5m, PayoutDays = 14
        }, 7, "Admin");

        Assert.NotNull(published);
        Assert.Equal(8.5m, published.PlatformFeePercent);
        Assert.Equal(14, published.PayoutDays);
        Assert.Equal(8.5m, result.PlatformFeePercent);
        Assert.Equal(14, result.PayoutDays);
        Assert.Equal("POL-PRICE-V3.0", result.VersionCode);
        Assert.Equal(42, result.PolicyId);
    }

    [Fact]
    public async Task HistoryAndActive_MapPersistedPricingTerms()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        var row = new SystemPolicyRecord(42, "PRICE_TICKETING", "POL-PRICE-V2.0", 2,
            "Pricing", "All", "Update", "Terms", "Active", DateTime.UtcNow,
            DateTime.UtcNow, 7, "Admin", 9m, 21);
        repository.Setup(r => r.GetHistoryAsync("PRICE_TICKETING", default)).ReturnsAsync(new List<SystemPolicyRecord> { row });
        repository.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(new List<SystemPolicyRecord> { row });
        var service = new SystemPolicyService(repository.Object);

        var history = await service.GetHistoryAsync("price");
        var active = await service.GetActiveAsync();

        Assert.Equal(9m, history.Single().PlatformFeePercent);
        Assert.Equal(21, history.Single().PayoutDays);
        Assert.Equal(9m, active["PRICE_TICKETING"].PlatformFeePercent);
        Assert.Equal(21, active["PRICE_TICKETING"].PayoutDays);
    }

    [Fact]
    public async Task Create_NullPricingTerms_UsesFeatureDefaults()
    {
        var repository = new Mock<ISystemPolicyRepository>();
        repository.Setup(r => r.GetNextVersionNumberAsync(It.IsAny<string>(), default)).ReturnsAsync(1);
        repository.Setup(r => r.PublishAsync(It.IsAny<NewSystemPolicyVersion>(), default)).ReturnsAsync((1, DateTime.UtcNow));
        var result = await new SystemPolicyService(repository.Object).CreateAsync(new CreatePolicyVersionRequestDTO
        {
            Title = "Terms", ChangeSummary = "Initial", Content = "Terms",
            PlatformFeePercent = null, PayoutDays = null
        }, 7, "Admin");
        Assert.Equal(5m, result.PlatformFeePercent);
        Assert.Equal(7, result.PayoutDays);
    }
}
