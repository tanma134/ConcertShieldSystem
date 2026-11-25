using Moq;
using ReviewAPI.DTOs;
using ReviewAPI.Models;
using Xunit;
using static ReviewAPI.UnitTests.ReviewServiceTestHelper;

namespace ReviewAPI.UnitTests;

public sealed class ReplyReviewTests
{
    [Fact]
    public async Task UTCID01_AdminCanReplyAndCommentIsTrimmed()
    {
        var (service, repository, events, users, _) = CreateService();
        users.Setup(u => u.GetUserNameAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Replier");
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);
        ReviewReply? added = null;
        repository.Setup(r => r.AddReplyAsync(It.IsAny<ReviewReply>()))
            .Callback<ReviewReply>(reply => added = reply)
            .Returns(Task.CompletedTask);

        var response = await service.ReplyAsync(20, new CreateReplyRequest { Comment = "  Thank you!  " }, 3, ["Admin"]);

        Assert.Equal(20, response.ReviewId);
        Assert.Equal(3, response.UserId);
        Assert.Equal("Admin", response.Role);
        Assert.Equal("Thank you!", response.Comment);
        Assert.Equal("Replier", response.UserName);
        Assert.NotNull(added);
        Assert.Equal(20, added.ReviewId);
        Assert.Equal(3, added.UserId);
        Assert.Equal("Admin", added.Role);
        Assert.Equal("Thank you!", added.Comment);
        events.Verify(e => e.GetEventAsync(12, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.AddReplyAsync(It.IsAny<ReviewReply>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_EventOrganizerCanReplyToOwnEvent()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);
        repository.Setup(r => r.AddReplyAsync(It.IsAny<ReviewReply>())).Returns(Task.CompletedTask);

        var response = await service.ReplyAsync(20, new CreateReplyRequest { Comment = "  Thank you!  " }, 3, ["Organizer"]);

        Assert.Equal("Organizer", response.Role);
        Assert.Equal("Thank you!", response.Comment);
        repository.Verify(r => r.AddReplyAsync(It.Is<ReviewReply>(reply => reply.Role == "Organizer")), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID03_AdminRoleTakesPriorityOverOrganizer()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);
        repository.Setup(r => r.AddReplyAsync(It.IsAny<ReviewReply>())).Returns(Task.CompletedTask);

        var response = await service.ReplyAsync(20, new CreateReplyRequest { Comment = " Thanks " }, 4, ["Admin", "Organizer"]);

        Assert.Equal("Admin", response.Role);
        repository.Verify(r => r.AddReplyAsync(It.Is<ReviewReply>(reply => reply.Role == "Admin")), Times.Once);
    }

    [Fact]
    public async Task UTCID04_AllowsReplyToHiddenReview()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        review.IsDeleted = true;
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);
        repository.Setup(r => r.AddReplyAsync(It.IsAny<ReviewReply>())).Returns(Task.CompletedTask);

        var response = await service.ReplyAsync(20, new CreateReplyRequest { Comment = " Thank you! " }, 3, ["Admin"]);

        Assert.Equal("Thank you!", response.Comment);
        repository.Verify(r => r.GetByIdAsync(20, true), Times.Once);
        repository.Verify(r => r.AddReplyAsync(It.IsAny<ReviewReply>()), Times.Once);
    }

    [Fact]
    public async Task UTCID05_AcceptsCommentAtMaximumLength()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);
        repository.Setup(r => r.AddReplyAsync(It.IsAny<ReviewReply>())).Returns(Task.CompletedTask);
        var comment = new string('x', 1000);

        var response = await service.ReplyAsync(20, new CreateReplyRequest { Comment = comment }, 3, ["Admin"]);

        Assert.Equal(comment, response.Comment);
        repository.Verify(r => r.AddReplyAsync(It.Is<ReviewReply>(reply => reply.Comment.Length == 1000)), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID06_AcceptsWhitespaceCommentAndTrimsItToEmpty()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);
        repository.Setup(r => r.AddReplyAsync(It.IsAny<ReviewReply>())).Returns(Task.CompletedTask);

        var response = await service.ReplyAsync(20, new CreateReplyRequest { Comment = "   " }, 3, ["Admin"]);

        Assert.Equal(string.Empty, response.Comment);
        repository.Verify(r => r.AddReplyAsync(It.Is<ReviewReply>(reply => reply.Comment == string.Empty)), Times.Once);
    }

    [Fact]
    public async Task UTCID07_RejectsCustomerRoleBeforeRepositoryLookup()
    {
        var (service, repository, events, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ReplyAsync(20, new CreateReplyRequest { Comment = "Thanks" }, 3, ["Customer"]));

        Assert.Equal("Only Admin or Organizer can reply to reviews.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        events.Verify(e => e.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID08_RejectsEmptyRolesBeforeRepositoryLookup()
    {
        var (service, repository, events, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ReplyAsync(20, new CreateReplyRequest { Comment = "Thanks" }, 3, []));

        Assert.Equal("Only Admin or Organizer can reply to reviews.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        events.Verify(e => e.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID09_ThrowsWhenReviewDoesNotExist()
    {
        var (service, repository, events, _, _) = CreateService();

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ReplyAsync(999, new CreateReplyRequest { Comment = "Thanks" }, 3, ["Admin"]));

        Assert.Equal("Review not found.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(999, true), Times.Once);
        events.Verify(e => e.GetEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.AddReplyAsync(It.IsAny<ReviewReply>()), Times.Never);
    }

    [Fact]
    public async Task UTCID10_ThrowsWhenEventDoesNotExist()
    {
        var (service, repository, events, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);
        events.Setup(e => e.GetEventAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, (string?)null, (int?)null));

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ReplyAsync(20, new CreateReplyRequest { Comment = "Thanks" }, 3, ["Admin"]));

        Assert.Equal("Event not found.", exception.Message);
        repository.Verify(r => r.AddReplyAsync(It.IsAny<ReviewReply>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID11_RejectsOrganizerReplyingToAnotherOrganizersEvent()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ReplyAsync(20, new CreateReplyRequest { Comment = "Thanks" }, 4, ["Organizer"]));

        Assert.Equal("Organizers can only reply to reviews for their own events.", exception.Message);
        repository.Verify(r => r.AddReplyAsync(It.IsAny<ReviewReply>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID12_NullCommentThrowsBeforeReplyIsAdded()
    {
        var (service, repository, _, _, _) = CreateService();
        var review = CreateExistingReview();
        repository.Setup(r => r.GetByIdAsync(20, true)).ReturnsAsync(review);

        await Assert.ThrowsAsync<NullReferenceException>(
            () => service.ReplyAsync(20, new CreateReplyRequest { Comment = null! }, 3, ["Admin"]));

        repository.Verify(r => r.AddReplyAsync(It.IsAny<ReviewReply>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }
}
