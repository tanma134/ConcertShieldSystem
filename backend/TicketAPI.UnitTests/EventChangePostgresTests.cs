using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TicketAPI.Models;
using TicketAPI.Services;
using Xunit;
namespace TicketAPI.UnitTests;
public class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute() { if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONCERTSHIELD_TEST_POSTGRES"))) Skip="PostgreSQL integration connection is not configured."; }
}
public class TicketChangeFixture : IDisposable
{
    private readonly string adminConnection;
    private readonly string databaseName = "concertshield_ticket_test_"+Guid.NewGuid().ToString("N");
    public string Connection { get; }
    public TicketDbContext Db { get; }
    public EventChangeService Service { get; }
    public TicketChangeFixture()
    {
        adminConnection=Environment.GetEnvironmentVariable("CONCERTSHIELD_TEST_POSTGRES")!;
        using(var connection=new NpgsqlConnection(adminConnection)) {connection.Open();using var command=new NpgsqlCommand("CREATE DATABASE "+databaseName,connection);command.ExecuteNonQuery();}
        Connection=new NpgsqlConnectionStringBuilder(adminConnection){Database=databaseName}.ConnectionString;
        Db=NewContext();Db.Database.EnsureCreated();Service=new(Db);
    }
    public TicketDbContext NewContext()=>new(new DbContextOptionsBuilder<TicketDbContext>().UseNpgsql(Connection).Options);
    public async Task<Order> SeedAsync(int eventId=1,string status="Paid",int quantity=2)
    {
        var order=new Order {EventId=eventId,CustomerId=7,Status=status,FinalAmount=500000,TotalAmount=500000,OrderDate=DateTime.UtcNow.AddDays(-1),StartsAt=DateTime.UtcNow.AddDays(10),EndsAt=DateTime.UtcNow.AddDays(10).AddHours(2)};
        Db.Orders.Add(order);await Db.SaveChangesAsync();
        if(status=="Paid") {for(int i=0;i<quantity;i++) Db.Tickets.Add(new Ticket {OrderId=order.OrderId,EventId=eventId,TicketTypeId=1,OwnerUserId=7,TicketCode=Guid.NewGuid(),Status="Active"});await Db.SaveChangesAsync();}
        return order;
    }
    public ApplyChangeInput Change(long id=1,string type="Reschedule")=>new(id,1,type,type=="Reschedule"?DateTime.UtcNow.AddDays(20):null,type=="Reschedule"?DateTime.UtcNow.AddDays(20).AddHours(2):null,(int)id,"Venue unavailable","Demo concert","demo-concert");
    public void Dispose(){Db.Dispose();NpgsqlConnection.ClearAllPools();using var connection=new NpgsqlConnection(adminConnection);connection.Open();using var command=new NpgsqlCommand("DROP DATABASE "+databaseName+" WITH (FORCE)",connection);command.ExecuteNonQuery();}
}
public class EventChangePostgresTests
{
    [PostgresFact] public async Task ReschedulePreservesTicketsAndUpdatesOrderDates()
    {using var f=new TicketChangeFixture();var order=await f.SeedAsync();var input=f.Change();await f.Service.ApplyAsync(input,default);f.Db.ChangeTracker.Clear();Assert.Equal(input.StartsAt,(await f.Db.Orders.FindAsync(order.OrderId))!.StartsAt);Assert.All(await f.Db.Tickets.ToListAsync(),t=>Assert.Equal("Active",t.Status));Assert.Equal(2,await f.Db.Set<AffectedTicket>().CountAsync());}
    [PostgresFact] public async Task PostponeClearsEffectiveDates()
    {using var f=new TicketChangeFixture();var order=await f.SeedAsync();await f.Service.ApplyAsync(f.Change(type:"Postpone"),default);f.Db.ChangeTracker.Clear();Assert.Null((await f.Db.Orders.FindAsync(order.OrderId))!.StartsAt);}
    [PostgresFact] public async Task RetryDoesNotDuplicateSnapshot()
    {using var f=new TicketChangeFixture();await f.SeedAsync();var input=f.Change();await f.Service.ApplyAsync(input,default);await f.Service.ApplyAsync(input,default);Assert.Equal(2,await f.Db.Set<AffectedTicket>().CountAsync());Assert.Single(await f.Db.Set<AppliedEventChange>().ToListAsync());}
    [PostgresFact] public async Task ReusedIdWithDifferentPayloadFails()
    {using var f=new TicketChangeFixture();await f.SeedAsync();var input=f.Change();await f.Service.ApplyAsync(input,default);await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.ApplyAsync(input with {Type="Postpone",StartsAt=null,EndsAt=null},default));}
    [PostgresFact] public async Task EachChangeHasItsOwnSnapshot()
    {using var f=new TicketChangeFixture();await f.SeedAsync();await f.Service.ApplyAsync(f.Change(),default);await f.Service.ApplyAsync(f.Change(2),default);Assert.Equal(2,await f.Db.Set<AffectedTicket>().CountAsync(x=>x.ChangeId==1));Assert.Equal(2,await f.Db.Set<AffectedTicket>().CountAsync(x=>x.ChangeId==2));}
    [PostgresFact] public async Task OtherConcertOrdersAreExcluded()
    {using var f=new TicketChangeFixture();await f.SeedAsync();await f.SeedAsync(2);await f.Service.ApplyAsync(f.Change(),default);Assert.Equal(2,await f.Db.Set<AffectedTicket>().CountAsync());}
    [PostgresFact] public async Task UnpaidOrdersAreNotAffectedTickets()
    {using var f=new TicketChangeFixture();await f.SeedAsync(status:"Pending");await f.Service.ApplyAsync(f.Change(),default);Assert.Empty(await f.Db.Set<AffectedTicket>().ToListAsync());}
    [PostgresFact] public async Task SummaryDoesNotMultiplyOrderAmountByTickets()
    {using var f=new TicketChangeFixture();await f.SeedAsync();await f.Service.ApplyAsync(f.Change(),default);var response=await f.Service.ReadAsync(1,null,"orders",null,null,null,1,10,default);var data=JsonSerializer.SerializeToElement(response);Assert.Equal(500000,data.GetProperty("summary").GetProperty("totalPaid").GetInt64());Assert.Equal(2,data.GetProperty("summary").GetProperty("tickets").GetInt32());}
    [PostgresFact] public async Task ReadIsSideEffectFreeAndSummaryIgnoresFilter()
    {using var f=new TicketChangeFixture();await f.SeedAsync();await f.Service.ApplyAsync(f.Change(),default);var before=await f.Db.Set<AffectedTicket>().CountAsync();var response=await f.Service.ReadAsync(1,null,"orders",null,null,999,1,10,default);var data=JsonSerializer.SerializeToElement(response);Assert.Equal(1,data.GetProperty("summary").GetProperty("orders").GetInt32());Assert.Equal(0,data.GetProperty("items").GetArrayLength());Assert.Equal(before,await f.Db.Set<AffectedTicket>().CountAsync());}
    [PostgresFact] public async Task PreviewDoesNotPersistSnapshots()
    {using var f=new TicketChangeFixture();await f.SeedAsync();await f.Service.ReadAsync(null,1,"tickets",null,null,null,1,10,default);Assert.Empty(await f.Db.Set<AffectedTicket>().ToListAsync());}
    [PostgresFact] public async Task InvalidFiltersFail()
    {using var f=new TicketChangeFixture();await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ReadAsync(null,1,"orders","Bad",null,null,1,10,default));await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ReadAsync(null,1,"orders",null,"invalid",null,1,10,default));}
    [PostgresFact] public async Task ConcurrentApplyUsesOneSnapshot()
    {using var f=new TicketChangeFixture();await f.SeedAsync();var input=f.Change();async Task Apply(){await using var db=f.NewContext();await new EventChangeService(db).ApplyAsync(input,default);}await Task.WhenAll(Apply(),Apply());Assert.Equal(2,await f.Db.Set<AffectedTicket>().CountAsync());}
    [PostgresFact] public async Task LatePaymentIsAttachedToAppliedChange()
    {
        using var f=new TicketChangeFixture();var order=await f.SeedAsync(status:"Pending");await f.Service.ApplyAsync(f.Change(),default);
        await using var tx=await f.Db.Database.BeginTransactionAsync();await f.Service.LockEventAsync(1,default);
        order.Status="Paid";var ticket=new Ticket {Order=order,OrderId=order.OrderId,EventId=1,TicketTypeId=1,OwnerUserId=7,TicketCode=Guid.NewGuid(),Status="Active"};f.Db.Tickets.Add(ticket);await f.Db.SaveChangesAsync();
        await f.Service.AttachLatePaymentAsync(order,new(){ticket},default);await f.Db.SaveChangesAsync();await tx.CommitAsync();Assert.Single(await f.Db.Set<AffectedTicket>().ToListAsync());
    }
}
