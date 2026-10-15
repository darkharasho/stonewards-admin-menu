using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Steamworks;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// Who may use the admin menu: the host (or a solo game), plus the admins the host granted from the menu
    /// (<see cref="Roster"/>), the Steam IDs in the host's <see cref="Plugin.TrustedAdmins"/> list, and the
    /// mod author, who is always on it — see <see cref="TrustList"/>. The host publishes its list in the Steam
    /// lobby data, so a client knows whether it's trusted there; when the host doesn't run this mod, the
    /// client's own list decides.
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

        private static AdminRoster _roster;
        private static string _rosterSource;

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
                _hostHadMod = null;
                return;
            }

            var self = SteamUser.GetSteamID();
            if (string.IsNullOrEmpty(SteamMatchmaking.GetLobbyMemberData(lobby, self, MemberKey)))
                SteamMatchmaking.SetLobbyMemberData(lobby, MemberKey, Plugin.PluginVersion);

            if (NetworkServer.active)
            {
                RefreshNames();
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

        /// <summary>
        /// The admins granted from the menu, reread whenever the stored value differs from what was parsed --
        /// the config can also be edited by hand, or reloaded by BepInEx, between calls.
        /// </summary>
        public static AdminRoster Roster
        {
            get
            {
                var stored = Plugin.SavedAdmins != null ? Plugin.SavedAdmins.Value : string.Empty;
                if (_roster == null || stored != _rosterSource)
                {
                    _roster = AdminRoster.Parse(stored);
                    _rosterSource = stored;
                }
                return _roster;
            }
        }

        /// <summary>Whether <paramref name="player"/> has been granted admin from the menu.</summary>
        public static bool IsGranted(FirstPersonController player)
        {
            return player != null && Roster.Contains(player.playerID);
        }

        /// <summary>
        /// Grants or revokes admin for <paramref name="player"/>, saving it so it still holds next time you
        /// host. Takes effect on the host at once; the player's own menu unlocks when it next reads the lobby
        /// data, within a couple of seconds. Granting does nothing for a player without the mod -- there'd be
        /// no menu to unlock, and publishing their ID would only mislead whoever read the list. Revoking is
        /// allowed either way, so a grant can always be taken back.
        /// </summary>
        public static void SetGranted(FirstPersonController player, bool granted)
        {
            if (!CanGrant || player == null || player.playerID == 0 || (granted && !HasMod(player)))
                return;
            if (!(granted ? Grant(player.playerID, player.playerName) : Revoke(player.playerID)))
                return;
            Plugin.Log.LogInfo($"{(granted ? "Granted" : "Revoked")} admin for {player.playerName} ({player.playerID})");
        }

        /// <summary>
        /// Adds <paramref name="steamId"/> to the saved roster, or updates the name held for them. Returns
        /// whether anything changed.
        /// </summary>
        public static bool Grant(ulong steamId, string playerName)
        {
            return Save(Roster.Set(steamId, playerName));
        }

        /// <summary>
        /// Removes <paramref name="steamId"/> from the saved roster, reporting whether they were on it. Takes
        /// a plain ID rather than a player so the Admins tab can revoke somebody who isn't in the lobby, which
        /// is most of the point of keeping a saved list at all.
        /// </summary>
        public static bool Revoke(ulong steamId)
        {
            return Save(Roster.Remove(steamId));
        }

        /// <summary>
        /// Writes the roster back to the config when it changed, and asks for the lobby list to be republished
        /// now instead of waiting out the poll, so a grant or a revocation lands promptly.
        /// </summary>
        private static bool Save(bool changed)
        {
            if (!changed)
                return false;
            _rosterSource = _roster.Format();
            if (Plugin.SavedAdmins != null)
                Plugin.SavedAdmins.Value = _rosterSource;
            _nextLobbyCheck = 0f;
            return true;
        }

        /// <summary>
        /// Relabels saved admins who are in the game, so the Admins tab doesn't go on showing a name somebody
        /// changed months ago. Only names already on the roster are touched -- this never grants anyone.
        /// </summary>
        private static void RefreshNames()
        {
            if (Roster.Ids.Count() == 0)
                return;
            foreach (var player in PlayerActions.Players())
                if (player != null && player.playerID != 0 && !string.IsNullOrEmpty(player.playerName) && Roster.Contains(player.playerID))
                    Save(Roster.Set(player.playerID, player.playerName));
        }

        public static bool HostHasMod()
        {
            var lobby = LobbyId();
            return lobby.m_SteamID != 0 && HasMod(SteamMatchmaking.GetLobbyOwner(lobby));
        }

        /// <summary>Whether the host runs this mod at <paramref name="minimum"/> or later, read from the version it flags itself with.</summary>
        public static bool HostVersionAtLeast(string minimum)
        {
            var lobby = LobbyId();
            if (lobby.m_SteamID == 0)
                return false;
            var owner = SteamMatchmaking.GetLobbyOwner(lobby);
            return owner.m_SteamID != 0 && ModVersion.AtLeast(SteamMatchmaking.GetLobbyMemberData(lobby, owner, MemberKey), minimum);
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

        private static HashSet<ulong> TrustedIds() => TrustList.Resolve(Plugin.TrustedAdmins.Value, Plugin.OwnerSteamId, Roster.Ids);

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
