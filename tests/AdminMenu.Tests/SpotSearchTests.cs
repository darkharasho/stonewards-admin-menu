using System.Collections.Generic;
using System.Linq;
using AdminMenu;
using Xunit;

public class SpotSearchTests
{
    private static List<SpotOffset> Ring() => SpotSearch.Candidates(preferredDistance: 1.5f, closerRings: 1, perRing: 8);

    [Fact]
    public void TriesTheSpotStraightAheadFirst()
    {
        var first = Ring()[0];
        Assert.Equal(1.5f, first.Forward, 3);
        Assert.Equal(0f, first.Right, 3);
    }

    [Fact]
    public void SweepsOutwardFromTheFacingDirectionAlternatingSides()
    {
        // Each side of the anchor gets tried before turning any further from where it is facing, so the spot
        // picked is the closest clear one to the side rather than whatever the sweep happened to reach first.
        var sides = Ring().Take(5).Select(o => o.Right).ToList();
        Assert.True(sides[1] > 0f && sides[2] < 0f, "the second and third candidates should mirror each other");
        Assert.Equal(sides[1], -sides[2], 3);
        Assert.True(sides[3] > sides[1], "the fourth candidate should be turned further from forward");
    }

    [Fact]
    public void NeverLooksFartherAwayThanThePreferredDistance()
    {
        // A teleport that searched outward could drop you across the room, or through a wall into the spot
        // behind it; every candidate stays within arm's reach of the anchor.
        foreach (var offset in Ring())
            Assert.True(offset.Distance <= 1.5f + 0.001f, $"candidate at {offset.Distance} is beyond the preferred distance");
    }

    [Fact]
    public void EndsOnTheAnchorItselfAsTheLastResort()
    {
        // Somebody is standing at the anchor, so that spot is known to fit a player: it is the one candidate
        // that can never drop the teleported player through the map.
        var last = Ring().Last();
        Assert.Equal(0f, last.Forward, 3);
        Assert.Equal(0f, last.Right, 3);
    }

    [Fact]
    public void PicksTheFirstCandidateThatFits()
    {
        var candidates = Ring();
        var picked = SpotSearch.Pick(candidates, i => i >= 2);
        Assert.Equal(candidates[2], picked);
    }

    [Fact]
    public void FallsBackToTheAnchorWhenNothingFits()
    {
        var picked = SpotSearch.Pick(Ring(), _ => false);
        Assert.Equal(0f, picked.Forward, 3);
        Assert.Equal(0f, picked.Right, 3);
    }
}
