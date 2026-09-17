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
        public const string PluginVersion = "0.1.4";

        /// <summary>The mod author's Steam ID, the default for <see cref="TrustedAdmins"/>.</summary>
        private const string AuthorSteamId = "76561197987892075";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> CloseOnEscape;
        internal static ConfigEntry<string> TrustedAdmins;
        internal static ConfigEntry<bool> GodMode;
        internal static ConfigEntry<bool> InfiniteStamina;
        internal static ConfigEntry<bool> InfiniteAmmo;
        internal static ConfigEntry<bool> SuperPickaxe;
        internal static ConfigEntry<bool> SuperSpeedPickaxe;

        private Harmony _harmony;
        private bool _statEditorFailed;

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
            TrustedAdmins = Config.Bind("General", "TrustedAdmins", AuthorSteamId, new ConfigDescription(
                "Steam IDs (comma-separated) that may use the admin menu in games you host, besides you. Everyone else only gets it as host. "
                + "When you join a host running this mod, the host's list decides instead. Defaults to the mod author (darkharasho); clear it to remove them.", null,
                new ConfigurationManagerAttributes { DispName = "Trusted admins (Steam IDs)", Order = 20 }));

            GodMode = Config.Bind("Cheats", "GodMode", false, new ConfigDescription(
                "Keep your health topped up and block incoming damage. As a client, damage is applied by the host, so this heals you back instead of preventing the hit.", null,
                new ConfigurationManagerAttributes { DispName = "God mode", Order = 30 }));
            InfiniteStamina = Config.Bind("Cheats", "InfiniteStamina", false, new ConfigDescription(
                "Never run out of stamina when sprinting, climbing or blocking.", null,
                new ConfigurationManagerAttributes { DispName = "Infinite stamina", Order = 20 }));
            InfiniteAmmo = Config.Bind("Cheats", "InfiniteAmmo", false, new ConfigDescription(
                "Arrows, ballista bolts, bombs and other throwables are never used up. You need at least one to start with.", null,
                new ConfigurationManagerAttributes { DispName = "Infinite arrows & bombs", Order = 15 }));
            SuperPickaxe = Config.Bind("Cheats", "SuperPickaxe", false, new ConfigDescription(
                "Dig with 25x strength, including the heavy dig bonus.", null,
                new ConfigurationManagerAttributes { DispName = "Super pickaxe", Order = 10 }));
            SuperSpeedPickaxe = Config.Bind("Cheats", "SuperSpeedPickaxe", false, new ConfigDescription(
                "Dig 3x faster.", null,
                new ConfigurationManagerAttributes { DispName = "Super speed pickaxe", Order = 5 }));

            _harmony = new Harmony(PluginGuid);
            Patch(typeof(StaminaPatches));
            Patch(typeof(ServerUpdateHealthPatch));
            Patch(typeof(ServerUpdateHealthPercentagePatch));
            Patch(typeof(InfiniteAmmoPatches));
            Patch(typeof(RagdollReviveFix));
            Patch(typeof(DigSpeedPatch));

            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void Update()
        {
            var state = ReadInputState();
            var inGame = GameManager.Instance != null;
            if (Hotkey.WasPressed(ToggleKey.Value) && (AdminMenuController.IsOpen || Access.Allowed)
                && MenuGate.CanToggle(Enabled.Value, inGame, state, AdminMenuController.IsOpen))
                AdminMenuController.Toggle();

            // Deliberately ungated: each cheat obeys exactly its own config toggle. Gating this on Enabled
            // would strand an applied super-pickaxe modifier, because the removal branch would stop running.
            Cheats.Update();
            WeaponSpeed.Update();
            AdminMenuController.Update();
            try
            {
                Access.Update();
                PlayerActions.Update();
                StatEditor.Update();
            }
            catch (Exception e)
            {
                if (!_statEditorFailed)
                    Log.LogWarning($"Admin menu networking failed; trusted admins, Bring and stat changes may not work: {e}");
                _statEditorFailed = true;
            }
        }

        /// <summary>
        /// Patches one class at a time so a single renamed game method costs only that cheat: letting it
        /// throw out of Awake would take the hotkey and the whole menu down with it.
        /// </summary>
        private bool Patch(Type patchClass)
        {
            try
            {
                _harmony.PatchAll(patchClass);
                return true;
            }
            catch (Exception e)
            {
                Log.LogWarning($"Could not apply {patchClass.Name}; the feature it backs will not work: {e.Message}");
                return false;
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
