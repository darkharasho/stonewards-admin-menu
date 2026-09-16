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

        // Rebuilt only when the configured multiplier changes: StatModifier is immutable, and the same
        // instance is re-added every frame while the cheat is on (see ApplyPickaxe).
        private static StatModifier _pickaxeModifier;

        // Every cheat is gated on Enabled as well as its own toggle. Cheats.Update itself runs unconditionally
        // (see Plugin.Update) so that turning the mod off still reaches the removal branch below and cleans up
        // an applied pickaxe modifier — gating the call instead would strand it until a restart.
        public static bool GodModeActive => Plugin.Enabled.Value && Plugin.GodMode.Value;
        public static bool InfiniteStaminaActive => Plugin.Enabled.Value && Plugin.InfiniteStamina.Value;
        private static bool SuperPickaxeActive => Plugin.Enabled.Value && Plugin.SuperPickaxe.Value;

        /// <summary>Called every frame from <see cref="Plugin.Update"/>.</summary>
        public static void Update()
        {
            var player = PlayerActions.LocalPlayer();
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

            // DigStrength, DiggingSpeed and HeavyDigMultiplier are assigned only by
            // PlayerStats.InitCharacterStats, which runs from LocalInit/ServerInit — after OnStartClient has
            // already put the controller in FirstPersonController.LocalPlayers. PlayerStats itself is a
            // serialized field and non-null from spawn, so it cannot stand in for this check.
            if (stats.DigStrength == null || stats.DiggingSpeed == null || stats.HeavyDigMultiplier == null)
                return;

            var wanted = SuperPickaxeActive;
            var multiplier = Mathf.Max(1f, Plugin.SuperPickaxeMultiplier.Value);

            if (wanted)
            {
                // CharacterStat.CalculateFinalValue applies PERCENT_MULT as `num3 *= 1f + mod.Value`, so the
                // modifier value is a percentage to add, not a factor: to get a "x multiplier" we pass
                // (multiplier - 1f), not multiplier itself.
                if (_pickaxeModifier == null || !Mathf.Approximately(multiplier, _appliedMultiplier))
                    _pickaxeModifier = new StatModifier(multiplier - 1f, StatModType.PERCENT_MULT, ModifierSource);

                var mod = _pickaxeModifier;
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
