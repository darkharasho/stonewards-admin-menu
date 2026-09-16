using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AdminMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.darkharasho.stonewards.adminmenu";
        public const string PluginName = "AdminMenu";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> CloseOnEscape;
        internal static ConfigEntry<bool> RequireHost;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            // Display names and order are for the in-game ModSettings menu; they don't change the .cfg.
            Enabled = Config.Bind("General", "Enabled", true, new ConfigDescription(
                "Allow the admin menu to be opened.", null,
                new ConfigurationManagerAttributes { DispName = "Enable admin menu", Order = 30 }));
            ToggleKey = Config.Bind("Controls", "ToggleKey", new KeyboardShortcut(KeyCode.F1), new ConfigDescription(
                "Key that opens and closes the admin menu.", null,
                new ConfigurationManagerAttributes { DispName = "Open / close admin menu", Order = 20 }));
            CloseOnEscape = Config.Bind("Controls", "CloseOnEscape", true, new ConfigDescription(
                "Also close the admin menu with Escape, instead of opening the pause menu.", null,
                new ConfigurationManagerAttributes { DispName = "Close with Escape", Order = 10 }));
            RequireHost = Config.Bind("General", "RequireHost", false, new ConfigDescription(
                "Hide the player and item actions unless you are the host. The game accepts them from any client; turn this on if you only want to use them in your own games.", null,
                new ConfigurationManagerAttributes { DispName = "Host-only actions", Order = 20 }));

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void Update()
        {
            var state = ReadInputState();
            var inGame = GameManager.Instance != null;
            if (Hotkey.WasPressed(ToggleKey.Value) && MenuGate.CanToggle(Enabled.Value, inGame, state, AdminMenuController.IsOpen))
                AdminMenuController.Toggle();
        }

        /// <summary>Maps the game's input state onto the handful of cases the menu gate distinguishes.</summary>
        private static MenuInputState ReadInputState()
        {
            if (InputManager.Instance == null)
                return MenuInputState.None;
            switch (InputManager.Instance.CurrentInputState)
            {
                case InputManager.InputState.Gameplay: return MenuInputState.Gameplay;
                case InputManager.InputState.Inventory: return MenuInputState.Inventory;
                case InputManager.InputState.Dead: return MenuInputState.Dead;
                case InputManager.InputState.GeneralMenu: return MenuInputState.Menu;
                case InputManager.InputState.None: return MenuInputState.None;
                default: return MenuInputState.Other;
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
