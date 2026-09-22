using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// The cheat toggles. God mode tops health back up through a game Command every frame it drops; the
    /// stamina and pickaxe cheats are local, because the game tracks both on the owning client.
    /// </summary>
    internal static class Cheats
    {
        public const string StrengthSource = "AdminMenu.SuperPickaxe";
        public const string SpeedSource = "AdminMenu.SuperSpeedPickaxe";

        // CharacterStat.CalculateFinalValue applies PERCENT_MULT as `num3 *= 1f + mod.Value`, so the modifier
        // value is the percentage to add, not the factor: x25 is 24f. StatModifier is immutable, so the same
        // instance is re-added every frame while the cheat is on (see ApplyPickaxe).
        private const float StrengthMultiplier = 25f;
        public const float SpeedMultiplier = 3f;
        private static readonly StatModifier StrengthModifier =
            new StatModifier(StrengthMultiplier - 1f, StatModType.PERCENT_MULT, StrengthSource);

        // The vanilla pickaxe's dig size (DiggingSettingsSO.diggSize) and a bomb's (PickableBomb.diggingRadius).
        // Both are the game's own defaults, hard-coded because diggingRadius is a private serialized field on
        // the bomb prefab with no accessor -- if the game retunes the bomb, this number won't follow.
        public const float VanillaDigSize = 0.35f;
        public const float BombDigSize = 1.5f;

        // The pickaxe's reach, hard-coded as the raycast distance in DiggingTool.Raycast. The ceiling is ours,
        // not the game's, but DiggingManager.PlayerDigGround raycasts only 100 units itself, so terrain past
        // that wouldn't dig however far this went.
        public const float VanillaReach = 3f;
        public const float MaxReach = 20f;

        // CmdUpdateHealth's argument is a delta, not an absolute (PlayerStats.ServerUpdateHealth), so sending
        // it every frame while under max health re-sends the full remaining deficit before the previous send
        // has round-tripped and been synced back — a burst of redundant full-heal deltas per hit on a plain
        // client. Throttling to this interval keeps god mode responsive without spamming the Command.
        private const float HealIntervalSeconds = 0.25f;

        private static bool _strengthApplied;
        private static bool _speedApplied;
        private static float _nextHealTime;

        // Every cheat is gated on Enabled as well as its own toggle. Cheats.Update itself runs unconditionally
        // (see Plugin.Update) so that turning the mod off still reaches the removal branch below and cleans up
        // applied pickaxe modifiers — gating the call instead would strand them until a restart.
        public static bool GodModeActive => Plugin.Enabled.Value && Access.Allowed && Plugin.GodMode.Value;
        public static bool InfiniteStaminaActive => Plugin.Enabled.Value && Access.Allowed && Plugin.InfiniteStamina.Value;
        public static bool InfiniteAmmoActive => Plugin.Enabled.Value && Access.Allowed && Plugin.InfiniteAmmo.Value;
        private static bool SuperPickaxeActive => Plugin.Enabled.Value && Access.Allowed && Plugin.SuperPickaxe.Value;
        public static bool SuperSpeedPickaxeActive => Plugin.Enabled.Value && Access.Allowed && Plugin.SuperSpeedPickaxe.Value;
        public static bool QuickOfferActive => Plugin.Enabled.Value && Access.Allowed && Plugin.QuickOffer.Value;

        // Slider-driven rather than a toggle: at the vanilla size there is nothing to do, so the floor of the
        // slider's range is the cheat's own off switch (see Patches.BombPickaxePatch).
        public static bool BombPickaxeActive => Plugin.Enabled.Value && Access.Allowed && PickaxeDigSize > VanillaDigSize;
        public static float PickaxeDigSize => Plugin.PickaxeDigSize.Value;

        // Same shape as the dig size slider: the vanilla reach is the bottom of the range and the off switch
        // (see Patches.LongReachPatch).
        public static bool PickaxeReachActive => Plugin.Enabled.Value && Access.Allowed && PickaxeReach > VanillaReach;
        public static float PickaxeReach => Plugin.PickaxeReach.Value;

        /// <summary>Called every frame from <see cref="Plugin.Update"/>.</summary>
        public static void Update()
        {
            var player = PlayerActions.LocalPlayer();
            if (player == null || player.PlayerStats == null)
            {
                _strengthApplied = false;
                _speedApplied = false;
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
        /// Adds or removes the super pickaxe's modifiers on dig strength and the heavy dig bonus, and pushes
        /// the super speed pickaxe's speed to the tool when it's toggled. <see cref="CharacterStat.AddOrReplaceModifier"/>
        /// and <see cref="CharacterStat.RemoveAllModifiersFromSource"/> are idempotent, so both run
        /// unconditionally every frame based only on whether the cheat is wanted rather than a one-shot
        /// transition: the stats are rebuilt when the archetype changes, which would silently drop a modifier
        /// applied only once on toggle. The <c>_applied</c> flags only gate the "on"/"off" log lines.
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

            Apply(SuperPickaxeActive, StrengthModifier, StrengthSource, ref _strengthApplied, $"Super pickaxe (x{StrengthMultiplier} strength)",
                stats.DigStrength, stats.HeavyDigMultiplier);
            // Digging speed isn't read from the local stat: the host computes it into the syncDiggingSpeed SyncVar,
            // and the pickaxe copies that into its animator's speed. A local modifier did nothing on a client,
            // so the speed is scaled where the pickaxe applies it (Patches.DigSpeedPatch). Re-raising the synced
            // value on toggle makes the equipped pickaxe re-apply it right away, and WeaponSpeed keeps it there.
            stats.DiggingSpeed.RemoveAllModifiersFromSource(SpeedSource);
            var speedWanted = SuperSpeedPickaxeActive;
            if (speedWanted != _speedApplied)
            {
                Plugin.Log.LogInfo($"Super speed pickaxe (x{SpeedMultiplier} speed) {(speedWanted ? "on" : "off")}");
                _speedApplied = speedWanted;
                stats.OnDiggingSpeedChanged?.Invoke(stats.syncDiggingSpeed);
            }
        }

        private static void Apply(bool wanted, StatModifier mod, string source, ref bool applied, string logName, params CharacterStat[] targets)
        {
            foreach (var stat in targets)
            {
                if (wanted)
                    stat.AddOrReplaceModifier(mod);
                else
                    stat.RemoveAllModifiersFromSource(source);
            }

            if (wanted != applied)
                Plugin.Log.LogInfo($"{logName} {(wanted ? "on" : "off")}");
            applied = wanted;
        }
    }
}
