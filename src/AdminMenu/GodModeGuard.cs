using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>A client telling the host whether to hold it invulnerable.</summary>
    internal struct GodModeMessage : NetworkMessage
    {
        public bool Enabled;
    }

    /// <summary>
    /// Makes god mode work for a client by having the host enforce it, instead of the client healing itself
    /// back up after the fact.
    ///
    /// The game applies damage and decides death in the same server-side call, so the after-the-fact heal in
    /// <see cref="Cheats"/> could never survive a burst that covered the whole health bar: the host had
    /// already set <c>isDead</c>. A client now tells the host when its god mode is on, the host remembers the
    /// connection, and the clamp in <see cref="Patches.ServerUpdateHealthPatch"/> vetoes the damage before
    /// the death check ever sees it -- exactly what already happened for the host's own player.
    ///
    /// The heal in <see cref="Cheats"/> stays as the fallback for hosts that don't run this mod, where
    /// nothing can be enforced and reacting late is all there is.
    /// </summary>
    internal static class GodModeGuard
    {
        /// <summary>How often the host re-checks who it should still be protecting.</summary>
        private const float PruneIntervalSeconds = 2f;

        private static readonly ProtectedPlayers Protected = new ProtectedPlayers();

        private static bool _handler;
        private static float _nextPrune;

        // Null until sent, so reconnecting re-sends rather than assuming the new host already knows.
        private static bool? _sentState;

        static GodModeGuard()
        {
            Writer<GodModeMessage>.write = (writer, message) => writer.WriteBool(message.Enabled);
            Reader<GodModeMessage>.read = reader => new GodModeMessage { Enabled = reader.ReadBool() };
        }

        /// <summary>
        /// Whether damage to <paramref name="stats"/> should be vetoed on this machine: either it's our own
        /// player with the cheat on, or it's a client the host is protecting on request.
        /// </summary>
        public static bool ShouldProtect(PlayerStats stats)
        {
            if (stats == null || stats.Player == null)
                return false;
            if (Cheats.GodModeActive && stats.Player.isLocalPlayer)
                return true;
            return IsProtectedByHost(stats);
        }

        private static bool IsProtectedByHost(PlayerStats stats)
        {
            if (!NetworkServer.active || Protected.Count == 0)
                return false;
            var connection = stats.Player.connectionToClient;
            return connection != null && Protected.Contains(connection.connectionId);
        }

        /// <summary>Called every frame from <see cref="Plugin.Update"/>.</summary>
        public static void Update()
        {
            if (NetworkServer.active)
            {
                // Mirror drops every handler when the server shuts down, so register again after each start.
                if (!_handler)
                {
                    NetworkServer.ReplaceHandler<GodModeMessage>(OnGodModeRequest);
                    _handler = true;
                }
                if (Protected.Count > 0 && Time.unscaledTime >= _nextPrune)
                {
                    _nextPrune = Time.unscaledTime + PruneIntervalSeconds;
                    Protected.PruneTo(StillTrustedConnectionIds());
                }
                // The host's own god mode is handled by the isLocalPlayer branch, so nothing to send.
                _sentState = null;
                return;
            }

            if (_handler)
            {
                // Server gone: every protection it was holding went with it.
                Protected.Clear();
                _handler = false;
            }

            if (!NetworkClient.active || !NetworkClient.isConnected)
            {
                _sentState = null;
                return;
            }

            var wanted = Cheats.GodModeActive;
            if (_sentState == wanted)
                return;
            NetworkClient.Send(new GodModeMessage { Enabled = wanted });
            _sentState = wanted;
            Plugin.Log.LogInfo($"Asked the host to {(wanted ? "hold god mode for us" : "stop holding god mode")}");
        }

        /// <summary>
        /// The connections still both present and trusted. Re-checking trust here rather than on every hit
        /// keeps the damage path a set lookup, and means revoking a player's admin (see <c>Elevate</c>) also
        /// stops the host protecting them, within the same couple of seconds the rest of the mod's trust
        /// changes take.
        /// </summary>
        private static List<int> StillTrustedConnectionIds()
        {
            var ids = new List<int>();
            foreach (var entry in NetworkServer.connections)
                if (entry.Value != null && Access.IsTrusted(entry.Value))
                    ids.Add(entry.Key);
            return ids;
        }

        /// <summary>
        /// Trusted admins only, like every other request this mod accepts: an unchecked message would let any
        /// client on a host running this mod declare itself invulnerable.
        /// </summary>
        private static void OnGodModeRequest(NetworkConnectionToClient sender, GodModeMessage message)
        {
            if (sender == null)
                return;
            if (!Access.IsTrusted(sender))
            {
                Plugin.Log.LogWarning($"Ignored a god mode request from untrusted connection {sender.address}");
                return;
            }
            if (!Protected.Set(sender.connectionId, message.Enabled))
                return;
            Plugin.Log.LogInfo($"God mode {(message.Enabled ? "enforced" : "released")} for connection {sender.address}");
        }
    }
}
