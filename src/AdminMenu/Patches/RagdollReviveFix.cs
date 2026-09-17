using HarmonyLib;
using Mirror;

namespace AdminMenu.Patches
{
    /// <summary>
    /// Stands a revived player's body back up on the host when the host can't see them.
    ///
    /// The game turns a ragdoll off with an RPC that only reaches peers who have the player in interest range.
    /// The host keeps a hidden copy of out-of-range players, so a player killed in the host's range and revived
    /// out of it came back into view still ragdolled, sliding around. Plain clients respawn the player when they
    /// come into range, so they never had the problem.
    ///
    /// Deliberately narrow: an earlier version that also forced ragdolls on, and re-checked every player on a
    /// timer, fought the game's carry and below-the-map failsafe and froze the game. This only turns a ragdoll
    /// off, only at the moment of revive, only on the host, and only when the game's own RPC won't reach it.
    /// </summary>
    [HarmonyPatch(typeof(FirstPersonController), "OnDeathChanged")]
    internal static class RagdollReviveFix
    {
        private static readonly AccessTools.FieldRef<FirstPersonController, RagdollEnabler> Ragdoll =
            AccessTools.FieldRefAccess<FirstPersonController, RagdollEnabler>("ragdollEnabler");
        private static readonly AccessTools.FieldRef<RagdollEnabler, bool> RagdollActive =
            AccessTools.FieldRefAccess<RagdollEnabler, bool>("isRagdollActive");
        private static readonly AccessTools.FieldRef<RagdollEnabler, bool> FailsafeInProgress =
            AccessTools.FieldRefAccess<RagdollEnabler, bool>("failsafeInProgress");

        private static void Postfix(FirstPersonController __instance, bool _IsDead)
        {
            if (_IsDead || __instance == null || __instance.isLocalPlayer)
                return;
            var host = NetworkServer.localConnection;
            if (!NetworkServer.active || host == null)
                return;
            // In range, the game's RpcDisableRagdollClient reaches the host and handles it.
            var identity = __instance.netIdentity;
            if (identity == null || identity.observers.ContainsKey(host.connectionId))
                return;
            if (__instance.PlayerCarryHandler != null && __instance.PlayerCarryHandler.IsCarried)
                return;
            var ragdoll = Ragdoll(__instance);
            if (ragdoll == null || !RagdollActive(ragdoll) || FailsafeInProgress(ragdoll))
                return;
            ragdoll.DeactivateRagdoll();
            Plugin.Log.LogInfo($"Stood up {__instance.playerName}'s ragdoll after an out-of-view revive");
        }
    }
}
