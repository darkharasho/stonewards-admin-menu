using AdminMenu;
using Xunit;

public class ProtectedPlayersTests
{
    [Fact]
    public void BlocksDamageForAProtectedPlayer()
    {
        Assert.True(ProtectedPlayers.BlocksDamage(-50f, isProtected: true));
    }

    [Fact]
    public void LetsDamageThroughForAnUnprotectedPlayer()
    {
        Assert.False(ProtectedPlayers.BlocksDamage(-50f, isProtected: false));
    }

    [Fact]
    public void NeverBlocksHealing()
    {
        // The clamp sits in the funnel every absolute health change goes through, healing included, so it has
        // to look at the sign rather than just at who the player is.
        Assert.False(ProtectedPlayers.BlocksDamage(50f, isProtected: true));
        Assert.False(ProtectedPlayers.BlocksDamage(0f, isProtected: true));
    }

    [Fact]
    public void BlocksADamageBurstBigEnoughToKillOutright()
    {
        // The bug this exists for: a volley whose total exceeds max health used to kill a client outright,
        // because the client could only heal back after the host had already set isDead.
        var players = new ProtectedPlayers();
        players.Set(7, true);
        foreach (var hit in new[] { -40f, -60f, -80f })
            Assert.True(ProtectedPlayers.BlocksDamage(hit, players.Contains(7)));
    }

    [Fact]
    public void RemembersWhoIsProtected()
    {
        var players = new ProtectedPlayers();
        players.Set(1, true);
        Assert.True(players.Contains(1));
        Assert.False(players.Contains(2));
    }

    [Fact]
    public void ReportsWhetherSettingActuallyChangedAnything()
    {
        // The host only logs and re-publishes on a real change, so a client re-sending its state must not
        // read as a change.
        var players = new ProtectedPlayers();
        Assert.True(players.Set(1, true));
        Assert.False(players.Set(1, true));
        Assert.True(players.Set(1, false));
        Assert.False(players.Set(1, false));
    }

    [Fact]
    public void DropsAPlayerWhoTurnedGodModeOff()
    {
        var players = new ProtectedPlayers();
        players.Set(1, true);
        players.Set(1, false);
        Assert.False(players.Contains(1));
        Assert.Equal(0, players.Count);
    }

    [Fact]
    public void ForgetsConnectionsThatHaveGoneAway()
    {
        // Mirror reuses connection ids, so a stale entry would hand a later player somebody else's god mode.
        var players = new ProtectedPlayers();
        players.Set(1, true);
        players.Set(2, true);
        players.PruneTo(new[] { 2 });
        Assert.False(players.Contains(1));
        Assert.True(players.Contains(2));
    }

    [Fact]
    public void ForgetsEveryoneWhenTheServerStops()
    {
        var players = new ProtectedPlayers();
        players.Set(1, true);
        players.Clear();
        Assert.Equal(0, players.Count);
    }
}
