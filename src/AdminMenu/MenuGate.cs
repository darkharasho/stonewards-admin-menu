namespace AdminMenu
{
    /// <summary>The game's input states, reduced to the ones the menu cares about.</summary>
    internal enum MenuInputState
    {
        None,
        Gameplay,
        Inventory,
        Dead,
        Menu,
        Other,
    }

    /// <summary>
    /// When the toggle key may open or close the menu. Closing is always allowed: a scene change or a
    /// death can move the game into a state the menu would not have opened in, and it must not get stuck.
    /// </summary>
    internal static class MenuGate
    {
        public static bool CanToggle(bool pluginEnabled, bool inGame, MenuInputState state, bool menuOpen)
        {
            if (menuOpen)
                return true;
            if (!pluginEnabled || !inGame)
                return false;
            return state == MenuInputState.Gameplay || state == MenuInputState.Inventory || state == MenuInputState.Dead;
        }
    }
}
