using System;
using System.Collections.Generic;
using Mirror;
using Steamworks;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// Who may use the admin menu: the host (or a solo game), plus the Steam IDs in the host's
    /// <see cref="Plugin.TrustedAdmins"/> list and the mod author, who is always on it — see
    /// <see cref="TrustList"/>. The host publishes its list in the Steam lobby data, so a client knows whether
    /// it's trusted there; when the host doesn't run this mod, the client's own list decides.
    ///
    /// Peers running this mod flag themselves in their lobby member data. Mirror disconnects a peer that
    /// receives a message it has no handler for, so the mod's own messages only go to peers with that flag.
    /// </summary>
    internal static class Access
    {
        private const string MemberKey = "darkharasho.adminmenu";
        private const string AdminsKey = "darkharasho.adminmenu.admins";

        private static bool _trustedClient;
        private static bool? _hostHadMod;
        private static float _nextLobbyCheck;

        /// <summary>
        /// Players the host elevated from the menu, for this session only. Kept in memory rather than in the
        /// config, and dropped when the lobby changes, so a grant lasts exactly as long as the game it was
        /// made in. The host already republishes its admin list to the lobby every couple of seconds, so a
        /// grant reaches the elevated player's own copy of the mod without any message of our own.
        /// </summary>
        private static readonly HashSet<ulong> _sessionGrants = new HashSet<ulong>();
        private static ulong _grantsLobby;

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
            if (lobby.m_SteamID != _grantsLobby)
            {
                // A different lobby (or none) is a different session: nothing granted in the last one carries.
                if (_sessionGrants.Count > 0)
                    Plugin.Log.LogInfo($"Session admin grants cleared ({_sessionGrants.Count}) on leaving the lobby");
                _sessionGrants.Clear();
                _grantsLobby = lobby.m_SteamID;
            }

            if (lobby.m_SteamID == 0)
            {
                _trustedClient = false;
                _hostHadMod = null;
                return;
            }

            var self = SteamUser.GetSteamID();
            if (string.IsNullOrEmpty(SteamMatchmaking.GetLobbyMemberData(lobby, self, MemberKey)))
                SteamMatchmaking.SetLobbyMemberData(lobby, MemberKey, Plugin.PluginVersion);

            if (NetworkServer.active)
            {
                var admins = TrustList.Format(TrustedIds());
                if (SteamMatchmaking.GetLobbyOwner(lobby) == self && SteamMatchmaking.GetLobbyData(lobby, AdminsKey) != admins)
                    SteamMatchmaking.SetLobbyData(lobby, AdminsKey, admins);
                return;
            }

            var hostHasMod = HostHasMod();
            if (hostHasMod != _hostHadMod)
            {
                var owner = SteamMatchmaking.GetLobbyOwner(lobby);
                Plugin.Log.LogInfo($"Host {owner.m_SteamID} (connected to {NetworkManager.singleton?.networkAddress}) "
                    + (hostHasMod ? "runs the admin menu; host-side actions go through it" : "doesn't run the admin menu (no lobby flag); Bring and stats are unavailable"));
                _hostHadMod = hostHasMod;
            }

            var list = hostHasMod ? Parse(SteamMatchmaking.GetLobbyData(lobby, AdminsKey)) : TrustedIds();
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

        /// <summary>
        /// Whether this peer can hand out session admin. Only the host can: the trusted list that matters is
        /// the host's, and it's the host that publishes it to the lobby and authorises incoming commands.
        /// </summary>
        public static bool CanGrant => NetworkServer.active;

        /// <summary>Whether <paramref name="player"/> has been elevated for this session.</summary>
        public static bool IsGranted(FirstPersonController player)
        {
            return player != null && _sessionGrants.Contains(player.playerID);
        }

        /// <summary>
        /// Elevates or demotes <paramref name="player"/> for the rest of the session. Takes effect on the
        /// host at once; the player's own menu unlocks when it next reads the lobby data, within a couple of
        /// seconds. Does nothing for a player without the mod -- there'd be no menu to unlock, and publishing
        /// their ID would only mislead whoever read the list.
        /// </summary>
        public static void SetGranted(FirstPersonController player, bool granted)
        {
            if (!CanGrant || player == null || player.playerID == 0 || !HasMod(player))
                return;
            var changed = granted ? _sessionGrants.Add(player.playerID) : _sessionGrants.Remove(player.playerID);
            if (!changed)
                return;
            Plugin.Log.LogInfo($"{(granted ? "Granted" : "Revoked")} session admin for {player.playerName} ({player.playerID})");
            // Push the new list now instead of waiting out the poll, so the grant lands promptly.
            _nextLobbyCheck = 0f;
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

        private static HashSet<ulong> TrustedIds() => TrustList.Resolve(Plugin.TrustedAdmins.Value, Plugin.OwnerSteamId, _sessionGrants);

        private static HashSet<ulong> Parse(string list) => TrustList.Parse(list);

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
