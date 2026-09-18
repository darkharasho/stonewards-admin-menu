using System.Linq;
using AdminMenu;
using Xunit;

public class TrustListTests
{
    private const string Owner = "76561197987892075";

    [Fact]
    public void TrustsTheOwnerWhenTheConfiguredListIsEmpty()
    {
        Assert.Contains(76561197987892075UL, TrustList.Resolve("", Owner));
    }

    [Fact]
    public void TrustsTheOwnerAlongsideTheConfiguredAdmins()
    {
        var ids = TrustList.Resolve("111,222", Owner);
        Assert.Contains(76561197987892075UL, ids);
        Assert.Contains(111UL, ids);
        Assert.Contains(222UL, ids);
    }

    [Fact]
    public void KeepsTheOwnerWhenAHostReplacesTheListWithTheirOwnFriends()
    {
        // The whole point of compiling the grant in: this is what used to drop it, and it is what a host does
        // the first time they add somebody, not a deliberate revocation.
        Assert.Contains(76561197987892075UL, TrustList.Resolve("111,222,333", Owner));
    }

    [Fact]
    public void DoesNotDuplicateTheOwnerWhenTheyAreAlsoListed()
    {
        var ids = TrustList.Resolve($"{Owner},111", Owner);
        Assert.Equal(2, ids.Count);
    }

    [Fact]
    public void IgnoresAnUnparseableOwnerRatherThanThrowing()
    {
        var ids = TrustList.Resolve("111", "not-a-steam-id");
        Assert.Equal(new[] { 111UL }, ids);
    }

    [Fact]
    public void TrustsAPlayerTheHostElevatedForTheSession()
    {
        var ids = TrustList.Resolve("", Owner, new ulong[] { 555 });
        Assert.Contains(555UL, ids);
    }

    [Fact]
    public void SessionGrantsJoinTheConfiguredAdminsRatherThanReplacingThem()
    {
        var ids = TrustList.Resolve("111", Owner, new ulong[] { 555 });
        Assert.Equal(new[] { 111UL, 555UL, 76561197987892075UL }, ids.OrderBy(id => id));
    }

    [Fact]
    public void IgnoresAZeroSessionGrant()
    {
        // A player whose Steam ID hasn't synced yet reads as 0; trusting that would trust everyone whose ID
        // is likewise unset.
        Assert.DoesNotContain(0UL, TrustList.Resolve("", Owner, new ulong[] { 0 }));
    }

    [Fact]
    public void ResolvesWithoutSessionGrantsWhenThereAreNone()
    {
        Assert.Equal(TrustList.Resolve("111", Owner), TrustList.Resolve("111", Owner, null));
    }

    [Fact]
    public void ParsesCommasSemicolonsAndSpacesAndSkipsJunk()
    {
        var ids = TrustList.Parse("111, 222;333 444 rubbish");
        Assert.Equal(new[] { 111UL, 222UL, 333UL, 444UL }, ids.OrderBy(id => id));
    }

    [Fact]
    public void ParsesAnEmptyListAsNobody()
    {
        Assert.Empty(TrustList.Parse(null));
        Assert.Empty(TrustList.Parse(""));
    }

    [Fact]
    public void FormatsTheSameSetToTheSameStringWhateverOrderItWasBuiltIn()
    {
        // The host compares this against the lobby data to decide whether to rewrite it, so an unstable
        // ordering would rewrite the same list on every check.
        Assert.Equal(TrustList.Format(TrustList.Parse("333,111,222")), TrustList.Format(TrustList.Parse("111,222,333")));
    }
}
