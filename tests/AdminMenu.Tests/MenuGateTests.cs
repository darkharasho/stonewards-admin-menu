using AdminMenu;
using Xunit;

public class MenuGateTests
{
    [Fact]
    public void OpensDuringGameplay()
    {
        Assert.True(MenuGate.CanToggle(pluginEnabled: true, inGame: true, MenuInputState.Gameplay, menuOpen: false));
    }

    [Fact]
    public void OpensWhileDeadSoYouCanReviveYourself()
    {
        Assert.True(MenuGate.CanToggle(pluginEnabled: true, inGame: true, MenuInputState.Dead, menuOpen: false));
    }

    [Fact]
    public void DoesNotOpenInTheMainMenuOrOtherGameMenus()
    {
        Assert.False(MenuGate.CanToggle(pluginEnabled: true, inGame: false, MenuInputState.None, menuOpen: false));
        Assert.False(MenuGate.CanToggle(pluginEnabled: true, inGame: true, MenuInputState.Other, menuOpen: false));
    }

    [Fact]
    public void DoesNotOpenWhenDisabled()
    {
        Assert.False(MenuGate.CanToggle(pluginEnabled: false, inGame: true, MenuInputState.Gameplay, menuOpen: false));
    }

    [Fact]
    public void AlwaysClosesWhenOpenEvenIfTheStateTurnedUnsupported()
    {
        Assert.True(MenuGate.CanToggle(pluginEnabled: false, inGame: false, MenuInputState.Other, menuOpen: true));
    }

    [Fact]
    public void RestoresGameplayAfterRevivingYourselfFromTheMenuWhileDead()
    {
        Assert.Equal(MenuInputState.Gameplay, MenuGate.StateToRestore(MenuInputState.Menu, localPlayerDead: false));
    }

    [Fact]
    public void RestoresTheDeadStateWhenStillDead()
    {
        Assert.Equal(MenuInputState.Dead, MenuGate.StateToRestore(MenuInputState.Menu, localPlayerDead: true));
    }

    [Fact]
    public void LeavesAStateTheMenuDidNotSetAlone()
    {
        Assert.Null(MenuGate.StateToRestore(MenuInputState.Gameplay, localPlayerDead: true));
        Assert.Null(MenuGate.StateToRestore(MenuInputState.Dead, localPlayerDead: false));
    }
}
