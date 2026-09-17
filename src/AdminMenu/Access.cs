using System;
using System.Collections.Generic;
using Mirror;
using Steamworks;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// Who may use the admin menu: the host (or a solo game), plus the Steam IDs in the host's
    /// <see cref="Plugin.TrustedAdmins"/> list. The host publishes its list in the Steam lobby data, so a client
    /// knows whether it's trusted there; when the host doesn't run this mod, the client's own list decides.
    ///
    /// Peers running this mod flag themselves in their lobby member data. Mirror disconnects a peer that
    /// receives a message it has no handler for, so the mod's own messages only go to peers with that flag.
    /// </summary>
    internal static class Access
    {
        private const string MemberKey = "darkharasho.adminmenu";
        private const string AdminsKey = "darkharasho.adminmenu.admins";

        private static bool _trustedClient;
        private static float _nextLobbyCheck;

        /// <summary>Whether this peer may use the menu's actions and cheats.</summary>
        public static bool Allowed => NetworkServer.active || !NetworkClient.active || _trustedClient;

        /// <summary>Whether host-side actions go through the host's copy of this mod.</summary>
        public static bool RoutesThroughHost => !NetworkServer.active && NetworkClient.active && _trustedClient && HostHasMod();

        public static void Update()
        {
            if (Time.unscaledTime < _nextLobbyCheck)
                return;
            _nextLobbyCheck = Time.unscaledTime + 2f;

            var lobby = LobbyId();
            if (lobby.m_SteamID == 0)
            {
                _trustedClient = false;
                return;
            }

            var self = SteamUser.GetSteamID();
            if (string.IsNullOrEmpty(SteamMatchmaking.GetLobbyMemberData(lobby, self, MemberKey)))
                SteamMatchmaking.SetLobbyMemberData(lobby, MemberKey, Plugin.PluginVersion);

            if (NetworkServer.active)
            {
                var admins = string.Join(",", TrustedIds());
                if (SteamMatchmaking.GetLobbyOwner(lobby) == self && SteamMatchmaking.GetLobbyData(lobby, AdminsKey) != admins)
                    SteamMatchmaking.SetLobbyData(lobby, AdminsKey, admins);
                return;
            }

            var list = HostHasMod() ? Parse(SteamMatchmaking.GetLobbyData(lobby, AdminsKey)) : TrustedIds();
            var trusted = list.Contains(self.m_SteamID);
            if (trusted != _trustedClient)
                Plugin.Log.LogInfo(trusted ? "Trusted admin in this lobby" : "Not a trusted admin in this lobby; the menu is host-only");
            _trustedClient = trusted;
        }

        /// <summary>
        /// Whether a message from <paramref name="sender"/> may act on the host. The Steam ID comes from the
        /// transport's connection address, the same source the game uses for player IDs, not from the message.
        /// </summary>
        public static bool IsTrusted(NetworkConnectionToClient sender)
        {
            if (sender == null)
                return false;
            if (sender == NetworkServer.localConnection)
                return true;
            return ulong.TryParse(sender.address, out var steamId) && TrustedIds().Contains(steamId);
        }

        public static bool HostHasMod()
        {
            var lobby = LobbyId();
            return lobby.m_SteamID != 0 && HasMod(SteamMatchmaking.GetLobbyOwner(lobby));
        }

        public static bool HasMod(FirstPersonController player)
        {
            return player != null && (player.isLocalPlayer || HasMod(new CSteamID(player.playerID)));
        }

        private static bool HasMod(CSteamID member)
        {
            var lobby = LobbyId();
            return lobby.m_SteamID != 0 && member.m_SteamID != 0
                && !string.IsNullOrEmpty(SteamMatchmaking.GetLobbyMemberData(lobby, member, MemberKey));
        }

        private static HashSet<ulong> TrustedIds() => Parse(Plugin.TrustedAdmins.Value);

        private static HashSet<ulong> Parse(string list)
        {
            var ids = new HashSet<ulong>();
            if (string.IsNullOrEmpty(list))
                return ids;
            foreach (var part in list.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (ulong.TryParse(part.Trim(), out var id))
                    ids.Add(id);
            return ids;
        }

        private static CSteamID LobbyId()
        {
            try
            {
                if (!SteamManager.Initialized || SteamLobby.Instance == null)
                    return CSteamID.Nil;
                return new CSteamID(SteamLobby.Instance.currentLobbyID);
            }
            catch (Exception)
            {
                return CSteamID.Nil;
            }
        }
    }
}
