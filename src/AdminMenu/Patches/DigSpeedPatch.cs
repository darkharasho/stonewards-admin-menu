using HarmonyLib;

namespace AdminMenu.Patches
{
    /// <summary>
    /// Scales the dig animation speed the pickaxe sends for the local player. The pickaxe sets its animator's
    /// speed from the host-synced digging speed through a command any client may send, so this works without
    /// the mod on the host.
    /// </summary>
    [HarmonyPatch(typeof(DiggingTool), "OnDiggingSpeedChanged")]
    internal static class DigSpeedPatch
    {
        private static void Prefix(DiggingTool __instance, ref float _NewValue)
        {
            if (Cheats.SuperSpeedPickaxeActive && __instance.Player != null && __instance.Player.isLocalPlayer)
                _NewValue *= Cheats.SpeedMultiplier;
        }
    }
}
