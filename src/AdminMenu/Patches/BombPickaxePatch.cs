using HarmonyLib;
using Unity.Mathematics;

namespace AdminMenu.Patches
{
    /// <summary>
    /// Digs a crater the size of a bomb's instead of a pickaxe's.
    ///
    /// The pickaxe and the bomb end at the same networked call: <c>DiggingManager.PlayerDigGround</c>
    /// sends <see cref="DiggingManager.CmdDigGround"/> with <c>diggingSettingsSO.diggSize</c> (0.35), and
    /// <c>ExplosionObject.ServerExplodeWithTerrain</c> sends the same command with the bomb's digging
    /// radius (1.5). Only the size differs, so raising the size the local player sends is the whole cheat,
    /// and the command is <c>requiresAuthority = false</c>, so the host needs no mod -- the same trick as
    /// <see cref="DigSpeedPatch"/> and <see cref="QuickOfferPatch"/>. Patched on the command rather than on
    /// <c>PlayerDigGround</c> because the host runs <c>UserCode</c> straight from this method instead of
    /// serialising, so one prefix covers host and client.
    ///
    /// Only the crater grows. Resources still come from the single snapped grid point
    /// <c>PlayerDigGround</c> passes to <c>CmdTrySpawnResource</c>; a bomb enumerates every point in its
    /// sphere separately, server-side, and that path isn't reachable from here.
    /// </summary>
    [HarmonyPatch(typeof(DiggingManager), nameof(DiggingManager.CmdDigGround))]
    internal static class BombPickaxePatch
    {
        private static void Prefix(ref float3 _Size, FirstPersonController _Player)
        {
            // _Player identifies a player's dig: level generation (DiggingManager.GenerateCaves) and the
            // bomb both pass null, so neither is touched, and other players' digs arrive as their own
            // commands on their own clients.
            if (_Player == null || !_Player.isLocalPlayer || !Cheats.BombPickaxeActive)
                return;

            // Never shrink: the slider's floor is the vanilla dig size, and a dig that is already larger
            // than what the slider asks for is not ours to make smaller.
            var size = Cheats.PickaxeDigSize;
            if (size > _Size.x)
                _Size = size;
        }
    }
}
