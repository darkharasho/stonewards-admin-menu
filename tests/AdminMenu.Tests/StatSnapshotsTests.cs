using AdminMenu;
using Xunit;

public class StatSnapshotsTests
{
    private const int Stats = 3;

    private static float[] Values(params float[] values) => values;

    [Fact]
    public void ReadsBackAStoredSnapshot()
    {
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);
        snapshots.Store(7, Values(1f, 2f, 3f), now: 10f);

        Assert.True(snapshots.TryGet(7, 1, now: 10f, out var value));
        Assert.Equal(2f, value);
    }

    [Fact]
    public void HasNothingForAPlayerItNeverHeardAbout()
    {
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);
        snapshots.Store(7, Values(1f, 2f, 3f), now: 10f);

        Assert.False(snapshots.TryGet(8, 1, now: 10f, out _));
    }

    [Fact]
    public void KeepsServingASnapshotUntilItGoesStale()
    {
        // The panel asks about once a second; a snapshot has to outlive the gap between replies or the
        // fields would flicker back to base values between them.
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);
        snapshots.Store(7, Values(1f, 2f, 3f), now: 10f);

        Assert.True(snapshots.TryGet(7, 0, now: 12.9f, out _));
    }

    [Fact]
    public void StopsServingAStaleSnapshot()
    {
        // Better to fall back to the local copy and say so than to show numbers from before the player
        // levelled up, or from after they left.
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);
        snapshots.Store(7, Values(1f, 2f, 3f), now: 10f);

        Assert.False(snapshots.TryGet(7, 0, now: 13.1f, out _));
    }

    [Fact]
    public void ReplacesAnEarlierSnapshotForTheSamePlayer()
    {
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);
        snapshots.Store(7, Values(1f, 2f, 3f), now: 10f);
        snapshots.Store(7, Values(4f, 5f, 6f), now: 11f);

        Assert.True(snapshots.TryGet(7, 0, now: 11f, out var value));
        Assert.Equal(4f, value);
    }

    [Fact]
    public void RejectsASnapshotFromADifferentBuildOfTheMod()
    {
        // The wire format is positional, so a peer with a different stat list would line every value up
        // against the wrong name. Showing nothing is honest; showing a shuffled stat table isn't.
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);

        Assert.False(snapshots.Store(7, Values(1f, 2f), now: 10f));
        Assert.False(snapshots.TryGet(7, 0, now: 10f, out _));
    }

    [Fact]
    public void RejectsAMissingSnapshot()
    {
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);

        Assert.False(snapshots.Store(7, null, now: 10f));
        Assert.False(snapshots.TryGet(7, 0, now: 10f, out _));
    }

    [Fact]
    public void KeepsAnEarlierSnapshotWhenARejectedOneArrives()
    {
        // A mismatched reply mustn't blank out values that are still good.
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);
        snapshots.Store(7, Values(1f, 2f, 3f), now: 10f);
        snapshots.Store(7, Values(9f), now: 11f);

        Assert.True(snapshots.TryGet(7, 0, now: 11f, out var value));
        Assert.Equal(1f, value);
    }

    [Fact]
    public void IgnoresAStatOutsideTheSnapshot()
    {
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);
        snapshots.Store(7, Values(1f, 2f, 3f), now: 10f);

        Assert.False(snapshots.TryGet(7, 3, now: 10f, out _));
        Assert.False(snapshots.TryGet(7, -1, now: 10f, out _));
    }

    [Fact]
    public void ForgetsEverythingWhenTheLevelEnds()
    {
        // Every player respawns between levels, and netIds are handed out afresh.
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);
        snapshots.Store(7, Values(1f, 2f, 3f), now: 10f);
        snapshots.Clear();

        Assert.False(snapshots.TryGet(7, 0, now: 10f, out _));
    }

    [Fact]
    public void ReportsWhetherAPlayerHasFreshValuesAtAll()
    {
        // The panel's note depends on this: it stops apologising for base stats once real ones arrive.
        var snapshots = new StatSnapshots(Stats, freshForSeconds: 3f);
        Assert.False(snapshots.IsFresh(7, now: 10f));

        snapshots.Store(7, Values(1f, 2f, 3f), now: 10f);
        Assert.True(snapshots.IsFresh(7, now: 12f));
        Assert.False(snapshots.IsFresh(7, now: 14f));
    }
}
