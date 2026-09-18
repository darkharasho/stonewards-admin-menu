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

        /// <summary>
        /// The state to hand input back to when the menu closes, or null when the current state is not the
        /// menu's own and so not ours to overwrite (a scene load or a game menu has already moved the game).
        ///
        /// Keyed off whether the local player is dead *now*, not off the state the menu was opened from: the
        /// menu can revive or kill you while it is open, and the game only swaps Dead and Gameplay itself when
        /// its own state is in effect — ours isn't. Restoring the remembered state after reviving yourself
        /// left a live player in the Dead state, which enables no movement input.
        ///
        /// Inventory is never restored either: the inventory screen closes itself when the admin menu takes
        /// input, so that state would leave inventory input live with no inventory on screen.
        /// </summary>
        public static MenuInputState? StateToRestore(MenuInputState current, bool localPlayerDead)
        {
            if (current != MenuInputState.Menu)
                return null;
            return localPlayerDead ? MenuInputState.Dead : MenuInputState.Gameplay;
        }
    }
}
