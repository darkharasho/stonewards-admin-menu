using System.Collections.Generic;
using System.Linq;
using AdminMenu;
using Xunit;

public class ItemSearchTests
{
    private static readonly List<ItemEntry> Items = new List<ItemEntry>
    {
        new ItemEntry { Id = "item_iron_ore", Name = "Iron Ore", Rarity = "Common" },
        new ItemEntry { Id = "item_gold_ore", Name = "Gold Ore", Rarity = "Rare" },
        new ItemEntry { Id = "weapon_iron_sword", Name = "Iron Sword", Rarity = "Common" },
    };

    [Fact]
    public void EmptySearchReturnsEverything()
    {
        Assert.Equal(3, ItemSearch.Filter(Items, "").Count);
        Assert.Equal(3, ItemSearch.Filter(Items, null).Count);
    }

    [Fact]
    public void MatchesNameCaseInsensitively()
    {
        Assert.Equal(new[] { "item_gold_ore" }, ItemSearch.Filter(Items, "gold").Select(i => i.Id));
    }

    [Fact]
    public void MatchesTheRawItemIdSoIdsFromLogsCanBePastedIn()
    {
        Assert.Equal(new[] { "weapon_iron_sword" }, ItemSearch.Filter(Items, "weapon_iron").Select(i => i.Id));
    }

    [Fact]
    public void ExactNameMatchesSortAbovePartialOnes()
    {
        var results = ItemSearch.Filter(Items, "iron");
        Assert.Equal(new[] { "item_iron_ore", "weapon_iron_sword" }, results.Select(i => i.Id));
    }

    [Fact]
    public void TrimsTheSearchSoTrailingSpacesStillMatch()
    {
        Assert.Single(ItemSearch.Filter(Items, "  gold  "));
    }
}
