using AdminAPI.DTOs;
using AdminAPI.Repositories;
using AdminAPI.Services;
using Moq;
using Xunit;

namespace AdminAPI.Tests.Services;

public class SystemPolicyServiceTests
{
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
