using QueueAPI.DTOs;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class ConfigureQueuePolicyTests
{
    private static readonly DateTime Start = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime End = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public async Task UTCID01_AcceptsPositiveAdmissionRate()
    {
        using var context = new QueueServiceTestContext();

        var result = await context.Service.ConfigureAsync(12, new ConfigureQueueRequest(Start, End, 100), 7);

        Assert.Equal(100, result.AdmissionRate);
        Assert.True(result.QueueEnabled);
        Assert.Equal("RANDOMIZED", result.FairnessPolicy);
        Assert.Equal(100, Assert.Single(context.Db.QueueConfigurations).AdmissionRate);
    }

    [Fact]
    public async Task UTCID02_AcceptsMinimumPositiveAdmissionRate()
    {
        using var context = new QueueServiceTestContext();

        var result = await context.Service.ConfigureAsync(12, new ConfigureQueueRequest(Start, End, 1), 7);

        Assert.Equal(1, result.AdmissionRate);
        Assert.Equal(1, Assert.Single(context.Db.QueueConfigurations).AdmissionRate);
    }

    [Fact]
    public async Task UTCID03_RejectsZeroAdmissionRate()
    {
        using var context = new QueueServiceTestContext();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            context.Service.ConfigureAsync(12, new ConfigureQueueRequest(Start, End, 0), 7));

        Assert.Contains("Admission rate must be greater than zero", exception.Message);
        Assert.Empty(context.Db.QueueConfigurations);
    }

    [Fact]
    public async Task UTCID04_RejectsNegativeAdmissionRate()
    {
        using var context = new QueueServiceTestContext();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            context.Service.ConfigureAsync(12, new ConfigureQueueRequest(Start, End, -5), 7));

        Assert.Contains("Admission rate must be greater than zero", exception.Message);
        Assert.Empty(context.Db.QueueConfigurations);
    }

    [Fact]
    public async Task UTCID05_TrimsAndUppercasesFairnessPolicy()
    {
        using var context = new QueueServiceTestContext();

        var result = await context.Service.ConfigureAsync(
            12, new ConfigureQueueRequest(Start, End, 100, true, "  fifo "), 7);

        Assert.Equal("FIFO", result.FairnessPolicy);
        Assert.Equal("FIFO", Assert.Single(context.Db.QueueConfigurations).FairnessPolicy);
    }

    [Fact]
    public async Task UTCID06_UppercasesRandomizedFairnessPolicy()
    {
        using var context = new QueueServiceTestContext();

        var result = await context.Service.ConfigureAsync(
            12, new ConfigureQueueRequest(Start, End, 100, true, "randomized"), 7);

        Assert.Equal("RANDOMIZED", result.FairnessPolicy);
        Assert.Equal("RANDOMIZED", Assert.Single(context.Db.QueueConfigurations).FairnessPolicy);
    }

    [Fact]
    public async Task UTCID07_PersistsQueueDisabledValue()
    {
        using var context = new QueueServiceTestContext();

        var result = await context.Service.ConfigureAsync(
            12, new ConfigureQueueRequest(Start, End, 100, false, "RANDOMIZED"), 7);

        Assert.False(result.QueueEnabled);
        Assert.False(Assert.Single(context.Db.QueueConfigurations).QueueEnabled);
    }

    [Fact]
    public async Task UTCID08_UsesRequestDefaultsWhenOptionalValuesAreOmitted()
    {
        using var context = new QueueServiceTestContext();

        var result = await context.Service.ConfigureAsync(12, new ConfigureQueueRequest(Start, End, 100), 7);

        Assert.True(result.QueueEnabled);
        Assert.Equal("RANDOMIZED", result.FairnessPolicy);
        Assert.Equal(100, Assert.Single(context.Db.QueueConfigurations).AdmissionRate);
    }

    [Fact]
    public async Task UTCID09_UpdatesExistingConfiguration()
    {
        using var context = new QueueServiceTestContext();
        var existing = await context.SeedConfigurationAsync(admissionRate: 50, queueEnabled: true);

        var result = await context.Service.ConfigureAsync(12, new ConfigureQueueRequest(Start, End, 100), 7);

        var configuration = Assert.Single(context.Db.QueueConfigurations);
        Assert.Equal(existing.QueueConfigurationId, configuration.QueueConfigurationId);
        Assert.Equal(100, configuration.AdmissionRate);
        Assert.True(configuration.QueueEnabled);
        Assert.Equal("RANDOMIZED", configuration.FairnessPolicy);
        Assert.Equal(100, result.AdmissionRate);
    }

    [Fact]
    public async Task UTCID10_TrimsWhitespaceFairnessPolicyToEmptyString()
    {
        using var context = new QueueServiceTestContext();

        var result = await context.Service.ConfigureAsync(
            12, new ConfigureQueueRequest(Start, End, 100, true, "   "), 7);

        Assert.Equal(string.Empty, result.FairnessPolicy);
        Assert.Equal(string.Empty, Assert.Single(context.Db.QueueConfigurations).FairnessPolicy);
    }

    [Fact]
    public async Task UTCID11_PropagatesNullFairnessPolicyFailure()
    {
        using var context = new QueueServiceTestContext();

        await Assert.ThrowsAsync<NullReferenceException>(() =>
            context.Service.ConfigureAsync(
                12, new ConfigureQueueRequest(Start, End, 100, true, null!), 7));

        Assert.Empty(context.Db.QueueConfigurations);
    }
}
