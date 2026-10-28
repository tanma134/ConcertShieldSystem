using EventAPI.Common;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace EventAPI.UnitTests;
public class GovernancePostgresTests
{
    private static async Task<int> Pending(EventFlowFixture f)
    { var ev=await f.Events.CreateAsync(EventFlowFixture.ValidEvent(),10); await f.CompleteDraftAsync(ev.EventId); await f.Events.SubmitAsync(ev.EventId,10); return ev.EventId; }
    [PostgresFact] public async Task ComplianceApprovedDoesNotPublish()
    { using var f=new EventFlowFixture(); var id=await Pending(f);await f.ApproveComplianceAsync(id);f.Context.ChangeTracker.Clear();var ev=await f.Context.Events.FindAsync(id);Assert.Equal("Pending",ev!.Status);Assert.Equal("Approved",ev.ComplianceStatus);Assert.Single(await f.Context.Set<ComplianceReview>().ToListAsync()); }
    [PostgresFact] public async Task ConcertApprovalBlockedBeforeCompliance()
    { using var f=new EventFlowFixture();var id=await Pending(f);await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Events.ApproveAsync(id,1,null));f.Context.ChangeTracker.Clear();Assert.Equal("Pending",(await f.Context.Events.FindAsync(id))!.Status); }
    [PostgresFact] public async Task MissingDocumentsBlockSubmit()
    { using var f=new EventFlowFixture();var ev=await f.Events.CreateAsync(EventFlowFixture.ValidEvent(),10);var result=await f.Events.ValidateForSubmissionAsync(ev.EventId,10,false);Assert.Contains(result.Errors,e=>e.Contains("EventPermit")); }
    [PostgresFact] public async Task OldVersionReviewFailsWithoutAudit()
    { using var f=new EventFlowFixture();var id=await Pending(f);await Assert.ThrowsAsync<GovernanceConflictException>(()=>f.Governance.ReviewComplianceAsync(id,1,new(99,"Approved","Reviewed"),default));Assert.Empty(await f.Context.Set<ComplianceReview>().ToListAsync()); }
    [PostgresFact] public async Task MoreInfoReturnsConcertForEditing()
    { using var f=new EventFlowFixture();var id=await Pending(f);await f.Governance.ReviewComplianceAsync(id,1,new(1,"RequestMoreInfo","Supply safety plan"),default);f.Context.ChangeTracker.Clear();var ev=await f.Context.Events.FindAsync(id);Assert.Equal("Rejected",ev!.Status);Assert.Equal("RequestMoreInfo",ev.ComplianceStatus); }
    [PostgresFact] public async Task DuplicateReviewIsRejected()
    { using var f=new EventFlowFixture();var id=await Pending(f);await f.ApproveComplianceAsync(id);await Assert.ThrowsAsync<GovernanceConflictException>(()=>f.ApproveComplianceAsync(id));Assert.Single(await f.Context.Set<ComplianceReview>().ToListAsync()); }
    [PostgresFact] public async Task WrongOwnerCannotReadDocuments()
    { using var f=new EventFlowFixture();var id=await Pending(f);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Governance.ComplianceAsync(id,99,false,default)); }
    [PostgresFact] public async Task MissingEventReturnsNotFound()
    { using var f=new EventFlowFixture();await Assert.ThrowsAsync<KeyNotFoundException>(()=>f.Governance.ComplianceAsync(123,10,false,default)); }
    [PostgresFact] public async Task SubmitChangeKeepsPublishedSchedule()
    { using var f=new EventFlowFixture();var id=await Pending(f);await f.ApproveComplianceAsync(id);await f.Events.ApproveAsync(id,1,null);var old=(await f.Context.Events.FindAsync(id))!.StartsAt;await f.Governance.SubmitChangeAsync(id,10,new("Postpone","Venue unavailable",null,null),default);f.Context.ChangeTracker.Clear();var ev=await f.Context.Events.FindAsync(id);Assert.Equal("Published",ev!.Status);Assert.Equal(old,ev.StartsAt); }
    [PostgresFact] public async Task DuplicatePendingChangeFails()
    { using var f=new EventFlowFixture();var id=await Pending(f);await f.ApproveComplianceAsync(id);await f.Events.ApproveAsync(id,1,null);await f.Governance.SubmitChangeAsync(id,10,new("Postpone","Venue unavailable",null,null),default);await Assert.ThrowsAsync<GovernanceConflictException>(()=>f.Governance.SubmitChangeAsync(id,10,new("Postpone","Venue unavailable",null,null),default)); }
    [PostgresFact] public async Task ApprovedPostponeClosesSalesAndQueuesSync()
    { using var f=new EventFlowFixture();var id=await Pending(f);await f.ApproveComplianceAsync(id);await f.Events.ApproveAsync(id,1,null);var request=await f.Governance.SubmitChangeAsync(id,10,new("Postpone","Venue unavailable",null,null),default);await f.Governance.ReviewChangeAsync(id,request,1,new("Approved","Change accepted"),default);f.Context.ChangeTracker.Clear();var ev=await f.Context.Events.FindAsync(id);Assert.Equal("Postponed",ev!.Status);Assert.True(ev.SalesFrozen);Assert.False((await f.Governance.EligibilityAsync(id,default)).CanSell);Assert.Single(await f.Context.Set<GovernanceOutbox>().Where(x=>x.Kind=="ApplyChange").ToListAsync()); }
    [PostgresFact] public async Task RejectChangeKeepsEvent()
    { using var f=new EventFlowFixture();var id=await Pending(f);await f.ApproveComplianceAsync(id);await f.Events.ApproveAsync(id,1,null);var request=await f.Governance.SubmitChangeAsync(id,10,new("Postpone","Venue unavailable",null,null),default);await f.Governance.ReviewChangeAsync(id,request,1,new("Rejected","Keep original schedule"),default);f.Context.ChangeTracker.Clear();Assert.Equal("Published",(await f.Context.Events.FindAsync(id))!.Status);Assert.Empty(await f.Context.Set<GovernanceOutbox>().Where(x=>x.Kind=="ApplyChange").ToListAsync()); }
    [PostgresFact] public async Task ConcurrentReviewsProduceOneDecision()
    {
        using var f=new EventFlowFixture();var id=await Pending(f);
        async Task<bool> Review() { await using var db=f.NewContext();var service=new GovernanceService(db,null!,null!,new ConfigurationBuilder().Build());try{await service.ReviewComplianceAsync(id,1,new(1,"Approved","Reviewed concurrently"),default);return true;}catch(GovernanceConflictException){return false;} }
        var result=await Task.WhenAll(Review(),Review());Assert.Single(result.Where(x=>x));Assert.Single(await f.Context.Set<ComplianceReview>().ToListAsync());
    }
}
