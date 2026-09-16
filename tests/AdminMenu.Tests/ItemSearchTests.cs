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

    [Fact]
    public void ExactNameMatchBeatsAContainsMatchEvenWhenAlphabeticallyLater()
    {
        // "AA Which Has Iron In It" sorts alphabetically before "Iron", so this only passes if the exact
        // match is genuinely ranked above the contains match rather than relying on the alphabetical tiebreak.
        var items = new List<ItemEntry>
        {
            new ItemEntry { Id = "item_exact", Name = "Iron", Rarity = "Common" },
            new ItemEntry { Id = "item_contains", Name = "AA Which Has Iron In It", Rarity = "Common" },
        };

        Assert.Equal(new[] { "item_exact", "item_contains" }, ItemSearch.Filter(items, "iron").Select(i => i.Id));
    }

    [Fact]
    public void NameContainsMatchBeatsAnIdOnlyMatchEvenWhenAlphabeticallyLater()
    {
        // "Widget" sorts alphabetically before "Zebra Ore", so this only passes if the name match is
        // genuinely ranked above the ID-only match rather than relying on the alphabetical tiebreak.
        var items = new List<ItemEntry>
        {
            new ItemEntry { Id = "item_misc", Name = "Zebra Ore", Rarity = "Common" },
            new ItemEntry { Id = "item_ore_sample", Name = "Widget", Rarity = "Common" },
        };

        Assert.Equal(new[] { "item_misc", "item_ore_sample" }, ItemSearch.Filter(items, "ore").Select(i => i.Id));
    }
}
