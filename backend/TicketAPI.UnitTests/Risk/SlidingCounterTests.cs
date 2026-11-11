using TicketAPI.Risk;
using Xunit;

namespace TicketAPI.Tests;

public class SlidingCounterTests
{
    [Fact]
    public void Hit_CountsEveryCallInsideTheWindow()
    {
        var c = new SlidingCounter();
        var w = TimeSpan.FromSeconds(10);

        Assert.Equal(1, c.Hit("k", w));
        Assert.Equal(2, c.Hit("k", w));
        Assert.Equal(3, c.Hit("k", w));
    }

    [Fact]
    public void Keys_AreIndependent()
    {
        var c = new SlidingCounter();
        var w = TimeSpan.FromSeconds(10);

        c.Hit("a", w); c.Hit("a", w);
        Assert.Equal(1, c.Hit("b", w));
    }

    [Fact]
    public async Task OldEntriesFallOutOfTheWindow()
    {
        var c = new SlidingCounter();
        var w = TimeSpan.FromMilliseconds(100);

        c.Hit("k", w); c.Hit("k", w);
        await Task.Delay(250);

        Assert.Equal(1, c.Hit("k", w));
    }

    [Fact]
    public void Peek_DoesNotAddAnEntry()
    {
        var c = new SlidingCounter();
        var w = TimeSpan.FromSeconds(10);

        c.Hit("k", w);
        Assert.Equal(1, c.Peek("k", w));
        Assert.Equal(1, c.Peek("k", w));
        Assert.Equal(0, c.Peek("unknown", w));
    }
}
