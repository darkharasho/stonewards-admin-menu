using AdminMenu;
using Xunit;

public class OfferBudgetTests
{
    [Fact]
    public void GrantsUpToCapacityAtOnce()
    {
        var budget = new OfferBudget(capacity: 15, window: 0.6f);
        Assert.Equal(15, budget.Take(0f, 30000));
        Assert.Equal(0, budget.Take(0.1f, 30000));
    }

    [Fact]
    public void FreesItemsOnceTheirWindowHasPassed()
    {
        var budget = new OfferBudget(capacity: 15, window: 0.6f);
        budget.Take(0f, 10);
        budget.Take(0.3f, 5);
        Assert.Equal(0, budget.Take(0.5f, 30000));
        Assert.Equal(10, budget.Take(0.6f, 30000));
        Assert.Equal(5, budget.Take(1.0f, 30000));
    }

    [Fact]
    public void GrantsOnlyWhatIsWanted()
    {
        var budget = new OfferBudget(capacity: 15, window: 0.6f);
        Assert.Equal(4, budget.Take(0f, 4));
        Assert.Equal(11, budget.Take(0f, 30000));
    }

    [Fact]
    public void NeverGrantsForAnEmptyRequest()
    {
        var budget = new OfferBudget(capacity: 15, window: 0.6f);
        Assert.Equal(0, budget.Take(0f, 0));
        Assert.Equal(15, budget.Take(0f, 15));
    }

    [Theory]
    [InlineData("0.1.11", "0.1.11", true)]
    [InlineData("0.1.12", "0.1.11", true)]
    [InlineData("0.2.0", "0.1.11", true)]
    [InlineData("0.1.10", "0.1.11", false)]
    [InlineData("0.1.9", "0.1.11", false)]
    [InlineData("", "0.1.11", false)]
    [InlineData(null, "0.1.11", false)]
    [InlineData("garbage", "0.1.11", false)]
    public void ComparesModVersionsNumerically(string version, string minimum, bool expected)
    {
        Assert.Equal(expected, ModVersion.AtLeast(version, minimum));
    }
}
