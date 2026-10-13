using EventAPI.Models;
using EventAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace EventAPI.UnitTests;
public class ComplianceUploadPostgresTests
{
    private class Assets(bool fail=false, Action? beforeUpload=null) : IComplianceAssetStore
    {
        public int Calls {get;private set;}
        public Task<StoredComplianceAsset> UploadAsync(byte[] bytes,string publicId,CancellationToken ct){Calls++;beforeUpload?.Invoke();if(fail)throw new AssetUploadException(publicId);return Task.FromResult(new StoredComplianceAsset(publicId,"https://example.invalid/authenticated"));}
        public Task<byte[]> DownloadAsync(string id,CancellationToken ct)=>Task.FromResult("%PDF-test"u8.ToArray());
        public Task DeleteAsync(string id,CancellationToken ct)=>Task.CompletedTask;
    }
    private static IFormFile Pdf(string name="permit.pdf",string mime="application/pdf",string content="%PDF-1.7\nfixture") => new FormFile(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)),0,System.Text.Encoding.UTF8.GetByteCount(content),"file",name){Headers=new HeaderDictionary(),ContentType=mime};
    private static GovernanceService Service(EventFlowFixture f,Assets assets)=>new(f.Context,assets,null!,new ConfigurationBuilder().Build());
    [PostgresFact] public async Task UploadCreatesVersionAndKeepsPriorDocuments()
    {using var f=new EventFlowFixture();var ev=await f.Events.CreateAsync(EventFlowFixture.ValidEvent(),10);var s=Service(f,new Assets());await s.UploadAsync(ev.EventId,10,"EventPermit",Pdf(),default);await s.UploadAsync(ev.EventId,10,"SafetyPlan",Pdf("safety.pdf"),default);f.Context.ChangeTracker.Clear();Assert.Equal(2,(await f.Context.Events.FindAsync(ev.EventId))!.ComplianceVersion);Assert.Equal(1,await f.Context.Set<ComplianceDocument>().CountAsync(x=>x.Version==1));Assert.Equal(2,await f.Context.Set<ComplianceDocument>().CountAsync(x=>x.Version==2));}
    [PostgresFact] public async Task WrongOwnerNeverCallsCloudinary()
    {using var f=new EventFlowFixture();var ev=await f.Events.CreateAsync(EventFlowFixture.ValidEvent(),10);var assets=new Assets();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f,assets).UploadAsync(ev.EventId,99,"EventPermit",Pdf(),default));Assert.Equal(0,assets.Calls);}
    [PostgresFact] public async Task SpoofedFileNeverCallsCloudinary()
    {using var f=new EventFlowFixture();var ev=await f.Events.CreateAsync(EventFlowFixture.ValidEvent(),10);var assets=new Assets();await Assert.ThrowsAsync<ArgumentException>(()=>Service(f,assets).UploadAsync(ev.EventId,10,"EventPermit",Pdf(content:"malicious"),default));Assert.Equal(0,assets.Calls);}
    [PostgresFact] public async Task PendingConcertCannotUpload()
    {using var f=new EventFlowFixture();var ev=await f.Events.CreateAsync(EventFlowFixture.ValidEvent(),10);await f.CompleteDraftAsync(ev.EventId);await f.Events.SubmitAsync(ev.EventId,10);var assets=new Assets();await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f,assets).UploadAsync(ev.EventId,10,"EventPermit",Pdf(),default));Assert.Equal(0,assets.Calls);}
    [PostgresFact] public async Task FailedUploadLeavesDurableCleanupAndNoDocument()
    {using var f=new EventFlowFixture();var ev=await f.Events.CreateAsync(EventFlowFixture.ValidEvent(),10);await Assert.ThrowsAsync<AssetUploadException>(()=>Service(f,new Assets(true)).UploadAsync(ev.EventId,10,"EventPermit",Pdf(),default));Assert.Empty(await f.Context.Set<ComplianceDocument>().ToListAsync());var job=Assert.Single(await f.Context.Set<GovernanceOutbox>().Where(x=>x.Kind=="DeleteAsset").ToListAsync());Assert.Null(job.CompletedAt);}
    [PostgresFact] public async Task ReplacingReviewedFileInvalidatesApprovalAndKeepsAudit()
    {using var f=new EventFlowFixture();var ev=await f.Events.CreateAsync(EventFlowFixture.ValidEvent(),10);await f.CompleteDraftAsync(ev.EventId);await f.Events.SubmitAsync(ev.EventId,10);await f.ApproveComplianceAsync(ev.EventId);await f.Events.RejectAsync(ev.EventId,"Correct concert details",1);await Service(f,new Assets()).UploadAsync(ev.EventId,10,"EventPermit",Pdf(),default);f.Context.ChangeTracker.Clear();var current=await f.Context.Events.FindAsync(ev.EventId);Assert.Equal(2,current!.ComplianceVersion);Assert.Null(current.ComplianceReviewedVersion);Assert.Single(await f.Context.Set<ComplianceReview>().ToListAsync());}
    [PostgresFact] public async Task DownloadRequiresOwnership()
    {using var f=new EventFlowFixture();var ev=await f.Events.CreateAsync(EventFlowFixture.ValidEvent(),10);await Service(f,new Assets()).UploadAsync(ev.EventId,10,"EventPermit",Pdf(),default);var doc=await f.Context.Set<ComplianceDocument>().SingleAsync();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f,new Assets()).DownloadAsync(ev.EventId,doc.Id,99,false,default));}
    [PostgresFact]
    public async Task NetworkUploadDoesNotHoldDatabaseTransaction()
    {
        using var fixture = new EventFlowFixture();
        var ev = await fixture.Events.CreateAsync(EventFlowFixture.ValidEvent(), 10);
        var assets = new Assets(beforeUpload: () => Assert.Null(fixture.Context.Database.CurrentTransaction));
        await Service(fixture, assets).UploadAsync(ev.EventId, 10, "EventPermit", Pdf(), default);
        Assert.Equal(1, assets.Calls);
        Assert.Single(await fixture.Context.Set<ComplianceDocument>().ToListAsync());
    }
}
