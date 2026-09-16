using HarmonyLib;

namespace AdminMenu.Patches
{
    /// <summary>Stamina is tracked on the owning client, so a local patch is enough to keep the bar full.</summary>
    [HarmonyPatch(typeof(PlayerStats))]
    internal static class StaminaPatches
    {
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
            Traverse.Create(__instance).Field<float>("currentStamina").Value = __instance.MaxStamina.Value;
        }

        private static bool IsLocal(PlayerStats stats) =>
            stats != null && stats.Player != null && stats.Player.isLocalPlayer;
    }

    /// <summary>
    /// Damage is decided on the server, so this only bites when you are the host. As a client, god mode
    /// falls back to the heal-up in <see cref="Cheats.Update"/>.
    /// </summary>
    [HarmonyPatch(typeof(PlayerStats), nameof(PlayerStats.CanTakeDamage))]
    internal static class CanTakeDamagePatch
    {
        private static void Postfix(PlayerStats __instance, ref bool __result)
        {
            if (Cheats.GodModeActive && __instance != null && __instance.Player != null && __instance.Player.isLocalPlayer)
                __result = false;
        }
    }
}
