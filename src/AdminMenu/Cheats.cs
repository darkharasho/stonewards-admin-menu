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

        // CmdUpdateHealth's argument is a delta, not an absolute (PlayerStats.ServerUpdateHealth), so sending
        // it every frame while under max health re-sends the full remaining deficit before the previous send
        // has round-tripped and been synced back — a burst of redundant full-heal deltas per hit on a plain
        // client. Throttling to this interval keeps god mode responsive without spamming the Command.
        private const float HealIntervalSeconds = 0.25f;

        private static bool _pickaxeApplied;
        private static float _appliedMultiplier;
        private static float _nextHealTime;

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
            var maxHealth = PlayerActions.MaxHealthOf(stats);
            if (GodModeActive && !stats.isDead && stats.GetCurrentHealth() < maxHealth
                && Time.unscaledTime >= _nextHealTime)
            {
                stats.CmdUpdateHealth(maxHealth - stats.GetCurrentHealth());
                _nextHealTime = Time.unscaledTime + HealIntervalSeconds;
            }

            ApplyPickaxe(player);
        }

        /// <summary>
        /// Adds or removes the dig modifiers. <see cref="CharacterStat.AddOrReplaceModifier"/> and
        /// <see cref="CharacterStat.RemoveAllModifiersFromSource"/> are idempotent, so both run unconditionally
        /// every frame based only on <c>wanted</c> rather than a one-shot transition: the stats are rebuilt
        /// when the archetype changes, which would silently drop a modifier applied only once on toggle.
        /// <c>_pickaxeApplied</c> is kept purely to gate the "on"/"off" log line to real transitions.
        /// </summary>
        public static void ApplyPickaxe(FirstPersonController player)
        {
            var stats = player.PlayerStats;
            var wanted = Plugin.SuperPickaxe.Value;
            var multiplier = Mathf.Max(1f, Plugin.SuperPickaxeMultiplier.Value);

            if (wanted)
            {
                // CharacterStat.CalculateFinalValue applies PERCENT_MULT as `num3 *= 1f + mod.Value`, so the
                // modifier value is a percentage to add, not a factor: to get a "x multiplier" we pass
                // (multiplier - 1f), not multiplier itself.
                var mod = new StatModifier(multiplier - 1f, StatModType.PERCENT_MULT, ModifierSource);
                stats.DigStrength.AddOrReplaceModifier(mod);
                stats.DiggingSpeed.AddOrReplaceModifier(mod);
                stats.HeavyDigMultiplier.AddOrReplaceModifier(mod);

                if (!_pickaxeApplied || !Mathf.Approximately(multiplier, _appliedMultiplier))
                    Plugin.Log.LogInfo($"Super pickaxe on at x{multiplier}");
                _pickaxeApplied = true;
                _appliedMultiplier = multiplier;
            }
            else
            {
                stats.DigStrength.RemoveAllModifiersFromSource(ModifierSource);
                stats.DiggingSpeed.RemoveAllModifiersFromSource(ModifierSource);
                stats.HeavyDigMultiplier.RemoveAllModifiersFromSource(ModifierSource);

                if (_pickaxeApplied)
                    Plugin.Log.LogInfo("Super pickaxe off");
                _pickaxeApplied = false;
            }
        }
    }
}
