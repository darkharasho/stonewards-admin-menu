using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Owns the single panel instance and attaches it to the game's HUD document. The HUD document is
    /// recreated on scene changes, so the attachment is rechecked every time the menu opens.
    /// </summary>
    internal static class AdminMenuController
    {
        private static AdminMenuPanel _panel;
        private static Hud _attachedHud;
        private static InputManager.InputState _stateBeforeOpen;

        /// <summary>
        /// The root having no panel means the HUD document we were attached to has been destroyed — a scene
        /// load with the menu open. Without that check the menu stays "logically open" while invisible, and
        /// the next hotkey press is swallowed closing it.
        /// </summary>
        public static bool IsOpen => _panel != null && _panel.IsOpen && _panel.Root.panel != null;

        public static void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        private static void Open()
        {
            if (!Attach())
                return;

            if (InputManager.Instance != null)
            {
                _stateBeforeOpen = InputManager.Instance.CurrentInputState;
                // GeneralMenu is the state the game's own full-screen menus use: player input off, cursor free.
                InputManager.Instance.SetState(InputManager.InputState.GeneralMenu);
            }
            _panel.Open();
            // Focusing an element whose display changed this frame can be dropped before layout resolves,
            // and the overlay must hold focus or its Escape handler never sees a key event.
            var root = _panel.Root;
            root.schedule.Execute(() => root.Focus()).ExecuteLater(0L);
        }

        public static void Close()
        {
            if (_panel == null || !_panel.IsOpen)
                return;
            _panel.Close();
            if (InputManager.Instance != null)
                RestoreInputState(InputManager.Instance);
        }

        /// <summary>
        /// Hands input back only if the menu's own state is still in effect: a scene load or a death that
        /// happened while the menu was open has already put the game where it wants to be, and overwriting
        /// that would strand the player with no input. The remembered state is sanity-checked for the same
        /// reason, since anything the menu could not have been opened from is not safe to return to.
        /// </summary>
        private static void RestoreInputState(InputManager input)
        {
            if (input.CurrentInputState != InputManager.InputState.GeneralMenu)
                return;

            var restore = _stateBeforeOpen;
            if (restore != InputManager.InputState.Gameplay
                && restore != InputManager.InputState.Inventory
                && restore != InputManager.InputState.Dead)
                restore = InputManager.InputState.Gameplay;
            input.SetState(restore);
        }

        /// <summary>
        /// Builds the panel once and keeps it attached to the live HUD root, re-adding it whenever the HUD
        /// has been rebuilt. Returns false while there is no HUD — during scene loads, for instance — so the
        /// menu declines to open rather than throwing.
        /// </summary>
        private static bool Attach()
        {
            var hud = Hud.Instance;
            var root = hud != null && hud.hudDocument != null ? hud.hudDocument.rootVisualElement : null;
            if (root == null)
            {
                Plugin.Log.LogWarning("No HUD document yet; the admin menu cannot open here.");
                return false;
            }

            if (_panel == null)
            {
                _panel = new AdminMenuPanel();
                _panel.CloseRequested += Close;

                var players = PlayersTab.Build(out var refreshPlayers);
                _panel.AddTab("Players", players, refreshPlayers);

                var items = ItemsTab.Build(out var refreshItems);
                _panel.AddTab("Items", items, refreshItems);

                var cheats = CheatsTab.Build(out var refreshCheats);
                _panel.AddTab("Cheats", cheats, refreshCheats);
            }

            // Same staleness check the HUD counter uses: a new Hud, or an element with no panel, means the
            // document we were attached to is gone.
            if (_panel.Root.panel == null || _attachedHud != hud)
            {
                _panel.Root.RemoveFromHierarchy();
                root.Add(_panel.Root);
                _panel.Root.style.position = Position.Absolute;
                _panel.Root.BringToFront();
                _attachedHud = hud;
            }
            return true;
        }
    }
}
