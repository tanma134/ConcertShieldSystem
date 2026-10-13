using TicketAPI.Domain;
using Xunit;
namespace TicketAPI.UnitTests;
public class EventChangeReturnPolicyTests
{
    [Fact] public void WithinChangeWindowCanReturn() { var now=DateTime.UtcNow; var r=EventChangeReturnPolicy.Evaluate("Active","Paid",null,false,now,now.AddHours(-1),168,100);Assert.True(r.IsEligible);Assert.Equal(100,r.RefundPercent); }
    [Fact] public void ExpiredChangeWindowCannotReturn() { var now=DateTime.UtcNow; Assert.False(EventChangeReturnPolicy.Evaluate("Active","Paid",null,false,now,now.AddHours(-169),168,100).IsEligible); }
    [Theory][InlineData("ReturnPending")][InlineData("Returned")][InlineData("Revoked")]
    public void InactiveTicketCannotReturn(string status) { var now=DateTime.UtcNow;Assert.False(EventChangeReturnPolicy.Evaluate(status,"Paid",null,false,now,now,168,100).IsEligible); }
    [Fact] public void AlreadyCheckedInCannotReturn() { var now=DateTime.UtcNow;Assert.False(EventChangeReturnPolicy.Evaluate("Active","Paid",now,false,now,now,168,100).IsEligible); }
    [Fact] public void DuplicateReturnCannotOpen() { var now=DateTime.UtcNow;Assert.False(EventChangeReturnPolicy.Evaluate("Active","Paid",null,true,now,now,168,100).IsEligible); }
    [Fact] public void RefundPercentIsBounded() { var now=DateTime.UtcNow;Assert.Equal(100,EventChangeReturnPolicy.Evaluate("Active","Paid",null,false,now,now,168,150).RefundPercent); }
}
