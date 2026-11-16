using Moq;
using ReviewAPI.Models;
using ReviewAPI.Services;
using Xunit;
using static ReviewAPI.UnitTests.ReviewServiceTestHelper;

namespace ReviewAPI.UnitTests;

public sealed class ViewAllReviewsAdminTests
{
    [Fact]
    public async Task UTCID01_ReturnsRepositoryPageAndMapsReviews()
    {
        var (service, repository, events, _, _) = CreateService();
        var reviews = BaselineReviews();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"));
        SetupSearch(repository, reviews, 12);

        var result = await service.GetForAdminAsync(null, null, null, 2, 5);

        Assert.Equal(new[] { 1, 2 }, result.Items.Select(x => x.ReviewId));
        Assert.Equal(new[] { "Rock Night", "Jazz Night" }, result.Items.Select(x => x.EventName));
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(12, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 2, 5), Times.Once);
    }

    [Fact]
    public async Task UTCID02_UsesDatabasePagingWhenEventNameIsNull()
    {
        var (service, repository, events, _, _) = CreateService();
        var reviews = BaselineReviews();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"));
        SetupSearch(repository, reviews, 12);

        var result = await service.GetForAdminAsync(null, null, null, 1, 20);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(12, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task UTCID03_ForwardsStatusAndRatingFiltersToRepository()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"));
        SetupSearch(repository, BaselineReviews(), 12);

        var result = await service.GetForAdminAsync(null, "hidden", 5, 1, 20);

        Assert.Equal(12, result.TotalCount);
        repository.Verify(r => r.SearchAdminAsync("hidden", 5, 1, 20), Times.Once);
    }

    [Fact]
    public async Task UTCID04_UsesMinimumPageSize()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"));
        SetupSearch(repository, BaselineReviews(), 12);

        var result = await service.GetForAdminAsync(null, null, null, 1, 1);

        Assert.Equal(1, result.PageSize);
        Assert.Equal(12, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 1), Times.Once);
    }

    [Fact]
    public async Task UTCID05_UsesMaximumPageSize()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"));
        SetupSearch(repository, BaselineReviews(), 12);

        var result = await service.GetForAdminAsync(null, null, null, 1, 100);

        Assert.Equal(100, result.PageSize);
        Assert.Equal(1, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 100), Times.Once);
    }

    [Fact]
    public async Task UTCID06_ClampsPageBelowOne()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"));
        SetupSearch(repository, BaselineReviews(), 12);

        var result = await service.GetForAdminAsync(null, null, null, 0, 20);

        Assert.Equal(1, result.Page);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task UTCID07_ClampsPageSizeBelowOne()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"));
        SetupSearch(repository, BaselineReviews(), 12);

        var result = await service.GetForAdminAsync(null, null, null, 1, 0);

        Assert.Equal(1, result.PageSize);
        Assert.Equal(12, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 1), Times.Once);
    }

    [Fact]
    public async Task UTCID08_ClampsPageSizeAboveOneHundred()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"));
        SetupSearch(repository, BaselineReviews(), 12);

        var result = await service.GetForAdminAsync(null, null, null, 1, 101);

        Assert.Equal(100, result.PageSize);
        Assert.Equal(1, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 100), Times.Once);
    }

    [Fact]
    public async Task UTCID09_ClampsNegativePageToOne()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"));
        SetupSearch(repository, BaselineReviews(), 12);

        var result = await service.GetForAdminAsync(null, null, null, -3, 20);

        Assert.Equal(1, result.Page);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task UTCID10_FiltersEventNameCaseInsensitively()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"), (103, "ROCK Fest"));
        SetupSearch(repository, ThreeNamedReviews(), 3);

        var result = await service.GetForAdminAsync("rock", null, null, 1, 20);

        Assert.Equal(new[] { "Rock Night", "ROCK Fest" }, result.Items.Select(x => x.EventName));
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 10000), Times.Once);
    }

    [Fact]
    public async Task UTCID11_PaginatesFilteredResultsOnFirstPage()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"), (103, "ROCK Fest"));
        SetupSearch(repository, ThreeNamedReviews(), 3);

        var result = await service.GetForAdminAsync("rock", null, null, 1, 1);

        Assert.Equal("Rock Night", Assert.Single(result.Items).EventName);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 10000), Times.Once);
    }

    [Fact]
    public async Task UTCID12_PaginatesFilteredResultsOnSecondPage()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"), (103, "ROCK Fest"));
        SetupSearch(repository, ThreeNamedReviews(), 3);

        var result = await service.GetForAdminAsync("rock", null, null, 2, 1);

        Assert.Equal("ROCK Fest", Assert.Single(result.Items).EventName);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 10000), Times.Once);
    }

    [Fact]
    public async Task UTCID13_TreatsWhitespaceEventNameAsNoFilter()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"));
        SetupSearch(repository, BaselineReviews(), 12);

        var result = await service.GetForAdminAsync("   ", null, null, 1, 20);

        Assert.Equal(12, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task UTCID14_ReturnsEmptyPageWhenEventNameMatchesNothing()
    {
        var (service, repository, events, _, _) = CreateService();
        SetupNames(events, (101, "Rock Night"), (102, "Jazz Night"), (103, "ROCK Fest"));
        SetupSearch(repository, ThreeNamedReviews(), 12);

        var result = await service.GetForAdminAsync("opera", null, null, 1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 10000), Times.Once);
    }

    [Fact]
    public async Task UTCID15_ReturnsEmptyResultFromRepository()
    {
        var (service, repository, _, _, _) = CreateService();
        SetupSearch(repository, [], 0);

        var result = await service.GetForAdminAsync(null, null, null, 1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task UTCID16_PropagatesRepositoryException()
    {
        var (service, repository, _, _, _) = CreateService();
        repository.Setup(r => r.SearchAdminAsync(null, null, 1, 20))
            .ThrowsAsync(new InvalidOperationException("Database unavailable."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetForAdminAsync(null, null, null, 1, 20));

        Assert.Equal("Database unavailable.", exception.Message);
        repository.Verify(r => r.SearchAdminAsync(null, null, 1, 20), Times.Once);
    }

    private static List<Review> BaselineReviews() =>
    [
        new() { ReviewId = 1, EventId = 101, UserId = 7, Rating = 5, Comment = "Great", CreatedAt = DateTime.UtcNow },
        new() { ReviewId = 2, EventId = 102, UserId = 8, Rating = 4, Comment = "Nice", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
    ];

    private static List<Review> ThreeNamedReviews() =>
    [
        new() { ReviewId = 1, EventId = 101, UserId = 7, Rating = 5, Comment = "Great", CreatedAt = DateTime.UtcNow },
        new() { ReviewId = 2, EventId = 102, UserId = 8, Rating = 4, Comment = "Nice", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
        new() { ReviewId = 3, EventId = 103, UserId = 9, Rating = 3, Comment = "Good", CreatedAt = DateTime.UtcNow.AddMinutes(-2) }
    ];

    private static void SetupNames(
        Mock<IEventClient> events,
        params (int EventId, string Name)[] names)
    {
        var byId = names.ToDictionary(x => x.EventId, x => x.Name);
        events.Setup(e => e.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int eventId, CancellationToken _) =>
            {
                byId.TryGetValue(eventId, out var name);
                return Task.FromResult((true, name, (int?)3));
            });
    }

    private static void SetupSearch(
        Mock<ReviewAPI.Repositories.IReviewRepository> repository,
        List<Review> reviews,
        int totalCount) =>
        repository.Setup(r => r.SearchAdminAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((reviews, totalCount));
}
