using System.Linq;
using AdminMenu;
using Xunit;

public class AdminRosterTests
{
    [Fact]
    public void ParsesAnEmptyValueAsNobody()
    {
        Assert.Empty(AdminRoster.Parse(null).Ids);
        Assert.Empty(AdminRoster.Parse("").Ids);
    }

    [Fact]
    public void RoundTripsAnAdminThroughFormatAndParse()
    {
        var roster = new AdminRoster();
        roster.Set(111UL, "Ferris");

        var reloaded = AdminRoster.Parse(roster.Format());

        Assert.True(reloaded.Contains(111UL));
        Assert.Equal("Ferris", reloaded.NameOf(111UL));
    }

    [Fact]
    public void FormatsTheSameAdminsToTheSameStringWhateverOrderTheyWereAddedIn()
    {
        // Saved on every change, and compared against what is already stored; an unstable ordering would
        // rewrite the config file for no reason.
        var one = new AdminRoster();
        one.Set(333UL, "Cara");
        one.Set(111UL, "Ferris");

        var other = new AdminRoster();
        other.Set(111UL, "Ferris");
        other.Set(333UL, "Cara");

        Assert.Equal(one.Format(), other.Format());
    }

    [Fact]
    public void KeepsAnIdThatWasHandEditedInWithoutAName()
    {
        var roster = AdminRoster.Parse("111");

        Assert.True(roster.Contains(111UL));
        Assert.Equal("", roster.NameOf(111UL));
    }

    [Fact]
    public void SkipsEntriesWhoseIdIsNotASteamId()
    {
        var roster = AdminRoster.Parse("111=Ferris,rubbish=Nobody,222=Dana");

        Assert.Equal(new[] { 111UL, 222UL }, roster.Ids.OrderBy(id => id));
    }

    [Fact]
    public void ReportsWhetherAddingAnAdminChangedAnything()
    {
        var roster = new AdminRoster();

        Assert.True(roster.Set(111UL, "Ferris"));
        Assert.False(roster.Set(111UL, "Ferris"));
    }

    [Fact]
    public void RenamesAnAdminInPlaceRatherThanAddingThemTwice()
    {
        // Players rename themselves; the roster follows the Steam ID, and the name is only a label.
        var roster = new AdminRoster();
        roster.Set(111UL, "Ferris");

        Assert.True(roster.Set(111UL, "Ferris the Crab"));
        Assert.Single(roster.Ids);
        Assert.Equal("Ferris the Crab", roster.NameOf(111UL));
    }

    [Fact]
    public void ReportsWhetherRemovingAnAdminChangedAnything()
    {
        var roster = new AdminRoster();
        roster.Set(111UL, "Ferris");

        Assert.True(roster.Remove(111UL));
        Assert.False(roster.Remove(111UL));
        Assert.False(roster.Contains(111UL));
    }

    [Fact]
    public void RefusesAZeroId()
    {
        // A player whose Steam ID hasn't synced yet reads as 0; saving that would grant admin to everybody
        // else whose ID is likewise unset.
        var roster = new AdminRoster();

        Assert.False(roster.Set(0UL, "Nobody"));
        Assert.Empty(roster.Ids);
    }

    [Fact]
    public void StoresANameContainingSeparatorsWithoutSwallowingTheNextAdmin()
    {
        var roster = new AdminRoster();
        roster.Set(111UL, "Ferris=the,Crab");
        roster.Set(222UL, "Dana");

        var reloaded = AdminRoster.Parse(roster.Format());

        Assert.Equal(new[] { 111UL, 222UL }, reloaded.Ids.OrderBy(id => id));
        Assert.Equal("Dana", reloaded.NameOf(222UL));
    }

    [Fact]
    public void KeepsOneEntryWhenTheSameIdIsStoredTwice()
    {
        var roster = AdminRoster.Parse("111=Ferris,111=Ferris");

        Assert.Single(roster.Ids);
    }

    [Fact]
    public void ListsAdminsByNameSoTheMenuShowsThemInAPredictableOrder()
    {
        var roster = new AdminRoster();
        roster.Set(111UL, "Zara");
        roster.Set(222UL, "Dana");

        Assert.Equal(new[] { 222UL, 111UL }, roster.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public void NamesAnAdminNobodyHasHeardOfAsNothingRatherThanThrowing()
    {
        Assert.Equal("", new AdminRoster().NameOf(111UL));
    }
}
