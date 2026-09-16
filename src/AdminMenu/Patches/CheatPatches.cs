using HarmonyLib;

namespace AdminMenu.Patches
{
    /// <summary>Stamina is tracked on the owning client, so a local patch is enough to keep the bar full.</summary>
    [HarmonyPatch(typeof(PlayerStats))]
    internal static class StaminaPatches
    {
        // Resolved once at type load instead of per frame: Traverse re-walks the field every call.
        private static readonly AccessTools.FieldRef<PlayerStats, float> CurrentStamina =
            AccessTools.FieldRefAccess<PlayerStats, float>("currentStamina");

        [HarmonyPrefix]
        [HarmonyPatch("ConsumeStamina")]
        private static bool SkipConsumption(PlayerStats __instance)
        {
            return !(Cheats.InfiniteStaminaActive && IsLocal(__instance));
        }

        [HarmonyPostfix]
        [HarmonyPatch("HandleStamina")]
        private static void RefillAfterRegen(PlayerStats __instance)
        {
            if (!Cheats.InfiniteStaminaActive || !IsLocal(__instance))
                return;
            // Sprinting and climbing drain the field directly instead of calling ConsumeStamina.
            var max = __instance.MaxStamina;
            if (max == null)
                return;
            CurrentStamina(__instance) = max.Value;
            // HandleStamina raises OnStaminaChanged with the drained value *before* this postfix restores it,
            // so the HUD would otherwise sit permanently one frame of drain below full. Re-raise it with the
            // restored value; the event is a public field, so the HUD's own handlers are the ones invoked.
            __instance.OnStaminaChanged?.Invoke(max.Value, max.Value, true);
        }

        private static bool IsLocal(PlayerStats stats) =>
            stats != null && stats.Player != null && stats.Player.isLocalPlayer;
    }

    /// <summary>
    /// <see cref="PlayerStats.ServerUpdateHealth"/> is the funnel for every absolute health change:
    /// <c>UserCode_ServerApplyDamage</c> reaches it directly, and <see cref="FirstPersonController"/> fall
    /// damage reaches it via <c>CmdUpdateHealth</c>. Clamping a negative delta to zero therefore blocks all
    /// damage while leaving healing and every other positive path alone.
    ///
    /// <c>ServerUpdateHealthPercentage</c> writes the health SyncVar itself rather than delegating here, so it
    /// is clamped separately below. <c>ServerRestoreDeath</c> does too, but it is scripted death rather than
    /// damage, so god mode deliberately does not veto it.
    ///
    /// The method is <c>[Server]</c>, so this only bites when you are the host. As a client, god mode falls
    /// back to the heal-up in <see cref="Cheats.Update"/>.
    /// </summary>
    [HarmonyPatch(typeof(PlayerStats), nameof(PlayerStats.ServerUpdateHealth))]
    internal static class ServerUpdateHealthPatch
    {
        private static void Prefix(PlayerStats __instance, ref float _Amount)
        {
            if (_Amount < 0f && Cheats.GodModeActive
                && __instance != null && __instance.Player != null && __instance.Player.isLocalPlayer)
                _Amount = 0f;
        }
    }

    /// <summary>
    /// The percentage variant clamps the health SyncVar itself instead of calling
    /// <see cref="PlayerStats.ServerUpdateHealth"/>, so it needs the same clamp to be covered by god mode.
    /// </summary>
    [HarmonyPatch(typeof(PlayerStats), nameof(PlayerStats.ServerUpdateHealthPercentage))]
    internal static class ServerUpdateHealthPercentagePatch
    {
        private static void Prefix(PlayerStats __instance, ref float _Percentage)
        {
            if (_Percentage < 0f && Cheats.GodModeActive
                && __instance != null && __instance.Player != null && __instance.Player.isLocalPlayer)
                _Percentage = 0f;
        }
    }
}
