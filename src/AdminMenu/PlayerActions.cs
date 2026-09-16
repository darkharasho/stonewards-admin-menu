using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// Player admin actions. Every call goes through a game Command marked
    /// <c>requiresAuthority = false</c>, so they work from the host and from a plain client alike.
    /// </summary>
    internal static class PlayerActions
    {
        /// <summary>Host-only mode is a user choice, not a technical limit; the game accepts these from any client.</summary>
        public static bool ActionsAllowed => !Plugin.RequireHost.Value || NetworkServer.active;

        public static List<FirstPersonController> Players() =>
            FirstPersonController.LocalPlayers
                .Where(p => p != null && p.PlayerStats != null)
                .OrderBy(p => p.playerName)
                .ToList();

        public static void Kill(FirstPersonController player)
        {
            if (!ActionsAllowed || player == null || player.PlayerStats == null || player.PlayerStats.isDead)
                return;
            player.PlayerStats.CmdDebugForceIsDead(player, true);
            Plugin.Log.LogInfo($"Killed {player.playerName}");
        }

        public static void Revive(FirstPersonController player)
        {
            if (!ActionsAllowed || player == null || player.PlayerStats == null || !player.PlayerStats.isDead)
                return;
            // The revive tool restores health and plays the game's own revive flow; ForceIsDead alone would
            // clear the flag but leave the player at zero health.
            player.CmdUseReviveTool(player, player.transform.position, 1f);
            Plugin.Log.LogInfo($"Revived {player.playerName}");
        }

        public static void Heal(FirstPersonController player)
        {
            if (!ActionsAllowed || player == null || player.PlayerStats == null)
                return;
            var stats = player.PlayerStats;
            stats.CmdUpdateHealth(stats.MaxHealth.Value - stats.GetCurrentHealth());
        }
    }
}
