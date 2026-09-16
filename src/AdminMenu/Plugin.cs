using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using AdminMenu.Patches;

namespace AdminMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.darkharasho.stonewards.adminmenu";
        public const string PluginName = "AdminMenu";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> CloseOnEscape;
        internal static ConfigEntry<bool> RequireHost;
        internal static ConfigEntry<bool> GodMode;
        internal static ConfigEntry<bool> InfiniteStamina;
        internal static ConfigEntry<bool> SuperPickaxe;
        internal static ConfigEntry<float> SuperPickaxeMultiplier;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

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

            GodMode = Config.Bind("Cheats", "GodMode", false, new ConfigDescription(
                "Keep your health topped up and block incoming damage. As a client, damage is applied by the host, so this heals you back instead of preventing the hit.", null,
                new ConfigurationManagerAttributes { DispName = "God mode", Order = 30 }));
            InfiniteStamina = Config.Bind("Cheats", "InfiniteStamina", false, new ConfigDescription(
                "Never run out of stamina when sprinting, climbing or blocking.", null,
                new ConfigurationManagerAttributes { DispName = "Infinite stamina", Order = 20 }));
            SuperPickaxe = Config.Bind("Cheats", "SuperPickaxe", false, new ConfigDescription(
                "Multiply dig strength, digging speed and the heavy dig bonus.", null,
                new ConfigurationManagerAttributes { DispName = "Super pickaxe", Order = 10 }));
            SuperPickaxeMultiplier = Config.Bind("Cheats", "SuperPickaxeMultiplier", 5f, new ConfigDescription(
                "How much stronger the super pickaxe is. 1 is the unmodified game.",
                new AcceptableValueRange<float>(1f, 25f),
                new ConfigurationManagerAttributes { DispName = "Super pickaxe strength", Order = 5 }));

            _harmony = new Harmony(PluginGuid);
            Patch(typeof(StaminaPatches));
            Patch(typeof(ServerUpdateHealthPatch));

            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void Update()
        {
            var state = ReadInputState();
            var inGame = GameManager.Instance != null;
            if (Hotkey.WasPressed(ToggleKey.Value) && MenuGate.CanToggle(Enabled.Value, inGame, state, AdminMenuController.IsOpen))
                AdminMenuController.Toggle();

            // Deliberately ungated: each cheat obeys exactly its own config toggle. Gating this on Enabled
            // would strand an applied super-pickaxe modifier, because the removal branch would stop running.
            Cheats.Update();
        }

        /// <summary>
        /// Patches one class at a time so a single renamed game method costs only that cheat: letting it
        /// throw out of Awake would take the hotkey and the whole menu down with it.
        /// </summary>
        private void Patch(Type patchClass)
        {
            try
            {
                _harmony.PatchAll(patchClass);
            }
            catch (Exception e)
            {
                Log.LogWarning($"Could not apply {patchClass.Name}; the cheat it backs will not work: {e.Message}");
            }
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
