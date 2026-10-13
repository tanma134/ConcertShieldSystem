using EventAPI.Common;
using EventAPI.DTOs;
using EventAPI.Services;
using Xunit;

namespace EventAPI.UnitTests;

// The whole concert flow, written as "false -> true": every step first proves the
// action is refused while something is missing, then proves it works once fixed.
public class EventLifecycleFlowTests
{
    private const int Org = EventFlowFixture.OrganizerId;
    private const int Admin = EventFlowFixture.AdminId;

    // ---- Step 1: create ----

    [PostgresFact]
    public async Task CreatingAConcertStartsItAsADraftOwnedByTheCreator()
    {
        using var f = new EventFlowFixture();

        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        Assert.Equal(EventStatus.Draft, created.Status);
        Assert.Equal(Org, created.OrganizerId);
        Assert.Equal("rock-night-can-tho", created.Slug);
    }

    [PostgresFact]
    public async Task CreatingWithATakenSlugIsRefusedThenWorksWithAnotherOne()
    {
        using var f = new EventFlowFixture();
        await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org));

        var second = EventFlowFixture.ValidEvent();
        second.Slug = "rock-night-2";
        var created = await f.Events.CreateAsync(second, Org);
        Assert.Equal("rock-night-2", created.Slug);
    }

    [PostgresFact]
    public async Task CreatingWithATooShortSlugIsRefused()
    {
        using var f = new EventFlowFixture();
        var dto = EventFlowFixture.ValidEvent();
        dto.Slug = "a";

        await Assert.ThrowsAsync<ArgumentException>(() => f.Events.CreateAsync(dto, Org));
    }

    // ---- Step 2: edit ----

    [PostgresFact]
    public async Task OnlyTheOwnerCanEditADraft()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Events.UpdateAsync(created.EventId, new UpdateEventDTO { Title = "Hacked" }, EventFlowFixture.OtherUserId, false));

        var updated = await f.Events.UpdateAsync(created.EventId, new UpdateEventDTO { Title = "Rock Night 2" }, Org, false);
        Assert.Equal("Rock Night 2", updated.Title);
    }

    [PostgresFact]
    public async Task EndBeforeStartBlocksSubmissionUntilCorrected()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);
        await f.CompleteDraftAsync(created.EventId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Events.UpdateAsync(created.EventId,
            new UpdateEventDTO { EndsAt = created.StartsAt.AddHours(-1) }, Org, false));
        f.Context.ChangeTracker.Clear();

        await f.Events.UpdateAsync(created.EventId,
            new UpdateEventDTO { EndsAt = created.StartsAt.AddHours(5) }, Org, false);
        var submitted = await f.Events.SubmitAsync(created.EventId, Org);
        Assert.Equal(EventStatus.Pending, submitted.Status);
    }

    // ---- Step 3: submit (incomplete -> complete) ----

    [PostgresFact]
    public async Task SubmittingAnIncompleteDraftReportsEveryMissingPiece()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        var ex = await Assert.ThrowsAsync<SubmissionValidationException>(() => f.Events.SubmitAsync(created.EventId, Org));

        Assert.False(ex.Result.IsValid);
        Assert.Contains(ex.Result.Errors, e => e.Contains("poster", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ex.Result.Errors, e => e.Contains("ticket type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ex.Result.Errors, e => e.Contains("refund", StringComparison.OrdinalIgnoreCase));
    }

    [PostgresFact]
    public async Task SubmittingAnIncompleteDraftLeavesItAsDraft()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        await Assert.ThrowsAsync<SubmissionValidationException>(() => f.Events.SubmitAsync(created.EventId, Org));

        var mine = await f.Events.GetMineByIdAsync(created.EventId, Org, false);
        Assert.Equal(EventStatus.Draft, mine.Status);
    }

    [PostgresFact]
    public async Task ValidationErrorsDisappearOneByOneAsTheDraftIsCompleted()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        var before = await f.Events.ValidateForSubmissionAsync(created.EventId, Org, false);
        Assert.False(before.IsValid);

        await f.Events.SetPosterAsync(created.EventId, "https://img.test/p.jpg", "p1", Org, false);
        var afterPoster = await f.Events.ValidateForSubmissionAsync(created.EventId, Org, false);
        Assert.DoesNotContain(afterPoster.Errors, e => e.Contains("poster", StringComparison.OrdinalIgnoreCase));

        await f.TicketTypes.CreateAsync(created.EventId, EventFlowFixture.ValidTicketType(), Org, false);
        await f.RefundPolicies.CreateAsync(created.EventId, EventFlowFixture.ValidRefundPolicy(), Org, false);

        await f.SeedComplianceAsync(created.EventId);
        var after = await f.Events.ValidateForSubmissionAsync(created.EventId, Org, false);
        Assert.True(after.IsValid);
    }

    [PostgresFact]
    public async Task ACompleteDraftIsSubmittedAndBecomesPending()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);
        await f.CompleteDraftAsync(created.EventId);

        var submitted = await f.Events.SubmitAsync(created.EventId, Org);

        Assert.Equal(EventStatus.Pending, submitted.Status);
    }

    [PostgresFact]
    public async Task SomeoneElseCannotSubmitMyDraft()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);
        await f.CompleteDraftAsync(created.EventId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Events.SubmitAsync(created.EventId, EventFlowFixture.OtherUserId));
    }

    // ---- Step 4: while Pending ----

    [PostgresFact]
    public async Task APendingConcertCannotBeEditedOrSubmittedAgain()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);
        await f.CompleteDraftAsync(created.EventId);
        await f.Events.SubmitAsync(created.EventId, Org);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Events.UpdateAsync(created.EventId, new UpdateEventDTO { Title = "Sneaky" }, Org, false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Events.SubmitAsync(created.EventId, Org));
    }

    [PostgresFact]
    public async Task RefundTermsAreLockedOnceSubmittedAndNotBeforeThen()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        // Draft: allowed.
        await f.RefundPolicies.CreateAsync(created.EventId, EventFlowFixture.ValidRefundPolicy("Early", 168, 100m), Org, false);
        await f.CompleteDraftWithoutRefundPolicyAsync(created.EventId);
        await f.Events.SubmitAsync(created.EventId, Org);

        // Pending: refused for the organizer.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.RefundPolicies.CreateAsync(created.EventId, EventFlowFixture.ValidRefundPolicy("Late", 48, 50m), Org, false));
    }

    [PostgresFact]
    public async Task ADraftIsInvisibleToThePublicButAPublishedConcertIsVisible()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Events.GetByIdAsync(created.EventId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            f.Access.EnsureVisibleAsync(created.EventId, EventFlowFixture.OtherUserId, false));

        await f.CompleteDraftAsync(created.EventId);
        await f.Events.SubmitAsync(created.EventId, Org);
        await f.ApproveComplianceAsync(created.EventId);
        await f.Events.ApproveAsync(created.EventId, Admin, null);

        var visible = await f.Events.GetByIdAsync(created.EventId);
        Assert.Equal(EventStatus.Published, visible.Status);
    }

    // ---- Step 5: admin decision ----

    [PostgresFact]
    public async Task OnlyAPendingConcertCanBeApprovedOrRejected()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Events.ApproveAsync(created.EventId, Admin, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Events.RejectAsync(created.EventId, "no", Admin));
    }

    [PostgresFact]
    public async Task ApprovalPublishesTheConcertAndRecordsWhoApprovedIt()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);
        await f.CompleteDraftAsync(created.EventId);
        await f.Events.SubmitAsync(created.EventId, Org);

        await f.ApproveComplianceAsync(created.EventId);
        var (approved, grant) = await f.Events.ApproveAsync(created.EventId, Admin, null);

        Assert.Equal(EventStatus.Published, approved.Status);
        Assert.NotNull(grant);
        Assert.Equal(Admin, approved.ReviewedBy);
        Assert.NotNull(approved.PublishedAt);
    }

    [PostgresFact]
    public async Task RejectionNeedsAReasonThenSendsTheConcertBackToTheOrganizer()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);
        await f.CompleteDraftAsync(created.EventId);
        await f.Events.SubmitAsync(created.EventId, Org);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Events.RejectAsync(created.EventId, "  ", Admin));

        var rejected = await f.Events.RejectAsync(created.EventId, "Poster is blurry", Admin);
        Assert.Equal(EventStatus.Rejected, rejected.Status);
        Assert.Equal("Poster is blurry", rejected.RejectedReason);
    }

    [PostgresFact]
    public async Task ARejectedConcertCanBeEditedAndResubmittedUntilPublished()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);
        await f.CompleteDraftAsync(created.EventId);
        await f.Events.SubmitAsync(created.EventId, Org);
        await f.Events.RejectAsync(created.EventId, "Fix the title", Admin);

        var edited = await f.Events.UpdateAsync(created.EventId, new UpdateEventDTO { Title = "Rock Night Fixed" }, Org, false);
        Assert.Equal("Rock Night Fixed", edited.Title);

        var resubmitted = await f.Events.SubmitAsync(created.EventId, Org);
        Assert.Equal(EventStatus.Pending, resubmitted.Status);
        Assert.Null(resubmitted.RejectedReason);

        await f.ApproveComplianceAsync(created.EventId);
        var (published, _) = await f.Events.ApproveAsync(created.EventId, Admin, null);
        Assert.Equal(EventStatus.Published, published.Status);
    }

    // ---- Step 6: after publishing ----

    [PostgresFact]
    public async Task APublishedConcertCannotBeEditedByItsOrganizer()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);
        await f.CompleteDraftAsync(created.EventId);
        await f.Events.SubmitAsync(created.EventId, Org);
        await f.ApproveComplianceAsync(created.EventId);
        await f.Events.ApproveAsync(created.EventId, Admin, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Events.UpdateAsync(created.EventId, new UpdateEventDTO { Title = "Changed" }, Org, false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Events.SoftDeleteAsync(created.EventId, Org, false));
    }

    [PostgresFact]
    public async Task ADraftCannotBeCancelledButAPublishedConcertCan()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Events.CancelAsync(created.EventId, Org, false));

        await f.CompleteDraftAsync(created.EventId);
        await f.Events.SubmitAsync(created.EventId, Org);
        await f.ApproveComplianceAsync(created.EventId);
        await f.Events.ApproveAsync(created.EventId, Admin, null);

        var cancelled = await f.Events.CancelAsync(created.EventId, Org, false);
        Assert.Equal(EventStatus.Cancelled, cancelled.Status);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Events.GetByIdAsync(created.EventId));
    }

    [PostgresFact]
    public async Task ADraftCanBeDeletedByItsOwnerOnly()
    {
        using var f = new EventFlowFixture();
        var created = await f.Events.CreateAsync(EventFlowFixture.ValidEvent(), Org);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Events.SoftDeleteAsync(created.EventId, EventFlowFixture.OtherUserId, false));

        await f.Events.SoftDeleteAsync(created.EventId, Org, false);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Events.GetMineByIdAsync(created.EventId, Org, false));
    }
}
