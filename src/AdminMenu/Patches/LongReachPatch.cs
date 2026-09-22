using HarmonyLib;
using UnityEngine;

namespace AdminMenu.Patches
{
    /// <summary>
    /// Reaches further with the pickaxe.
    ///
    /// <c>DiggingTool.Raycast</c> is a private helper with the reach hard-coded as <c>3f</c>, and
    /// <c>DiggingTool.OnHit</c> is its only caller -- once against the destructible layer and once against the
    /// diggable one. So this single patch lengthens both the swing at a prop and the dig at terrain; the game
    /// keeps no separate number for them. Everything downstream already works at range: DiggingManager's own
    /// <c>PlayerDigGround</c> raycast runs out to 100, and neither <c>DestructibleObject.CmdTap</c> nor
    /// <c>CmdDigGround</c> checks how far away the player was, so the host needs no mod -- the same trick as
    /// <see cref="DigSpeedPatch"/> and <see cref="BombPickaxePatch"/>.
    ///
    /// Replaces the method rather than transpiling it: the body is one <c>Physics.Raycast</c> call, so
    /// re-issuing it at our own distance is less to get wrong than matching a <c>ldc.r4 3</c> in IL.
    /// </summary>
    [HarmonyPatch(typeof(DiggingTool), "Raycast")]
    internal static class LongReachPatch
    {
        private static bool Prefix(DiggingTool __instance, Transform _cameraTransform, out RaycastHit hit,
            int layerMask, ref bool __result)
        {
            // ItemAnimationReceiver.OnAnimationEvent already gates the swing on isLocalPlayer, but its sibling
            // OnAnimationClientEvent doesn't, so check here rather than trust which one a prefab wires up.
            var player = __instance == null ? null : __instance.Player;
            if (player == null || !player.isLocalPlayer || !Cheats.PickaxeReachActive)
            {
                // Harmony requires the out parameter be assigned even on the path that runs the original,
                // which overwrites it.
                hit = default;
                return true;
            }

            __result = Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out hit,
                Cheats.PickaxeReach, layerMask);
            return false;
        }
    }
}
