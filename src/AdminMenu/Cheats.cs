using System.Linq;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// The cheat toggles. God mode tops health back up through a game Command every frame it drops; the
    /// stamina and pickaxe cheats are local, because the game tracks both on the owning client.
    /// </summary>
    internal static class Cheats
    {
        public const string ModifierSource = "AdminMenu.SuperPickaxe";

        private static bool _pickaxeApplied;
        private static float _appliedMultiplier;

        public static bool GodModeActive => Plugin.GodMode.Value && PlayerActions.ActionsAllowed;
        public static bool InfiniteStaminaActive => Plugin.InfiniteStamina.Value;

        /// <summary>Called every frame from <see cref="Plugin.Update"/>.</summary>
        public static void Update()
        {
            var player = FirstPersonController.LocalPlayers.FirstOrDefault(p => p != null && p.isLocalPlayer);
            if (player == null || player.PlayerStats == null)
            {
                _pickaxeApplied = false;
                return;
            }

            var stats = player.PlayerStats;
            if (GodModeActive && !stats.isDead && stats.GetCurrentHealth() < stats.MaxHealth.Value)
                stats.CmdUpdateHealth(stats.MaxHealth.Value - stats.GetCurrentHealth());

            ApplyPickaxe(player);
        }

        /// <summary>
        /// Adds or removes the dig modifiers. The stats are rebuilt when the archetype changes, so the
        /// multiplier is reapplied whenever it is missing rather than only on toggle.
        /// </summary>
        public static void ApplyPickaxe(FirstPersonController player)
        {
            var stats = player.PlayerStats;
            var wanted = Plugin.SuperPickaxe.Value;
            var multiplier = Mathf.Max(1f, Plugin.SuperPickaxeMultiplier.Value);

            if (wanted && (!_pickaxeApplied || !Mathf.Approximately(multiplier, _appliedMultiplier)))
            {
                // CharacterStat.CalculateFinalValue applies PERCENT_MULT as `num3 *= 1f + mod.Value`, so the
                // modifier value is a percentage to add, not a factor: to get a "x multiplier" we pass
                // (multiplier - 1f), not multiplier itself.
                var mod = new StatModifier(multiplier - 1f, StatModType.PERCENT_MULT, ModifierSource);
                stats.DigStrength.AddOrReplaceModifier(mod);
                stats.DiggingSpeed.AddOrReplaceModifier(mod);
                stats.HeavyDigMultiplier.AddOrReplaceModifier(mod);
                _pickaxeApplied = true;
                _appliedMultiplier = multiplier;
                Plugin.Log.LogInfo($"Super pickaxe on at x{multiplier}");
            }
            else if (!wanted && _pickaxeApplied)
            {
                stats.DigStrength.RemoveAllModifiersFromSource(ModifierSource);
                stats.DiggingSpeed.RemoveAllModifiersFromSource(ModifierSource);
                stats.HeavyDigMultiplier.RemoveAllModifiersFromSource(ModifierSource);
                _pickaxeApplied = false;
                Plugin.Log.LogInfo("Super pickaxe off");
            }
        }
    }
}
