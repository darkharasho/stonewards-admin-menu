using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>A trusted client asking the host to bring a player to the sender.</summary>
    internal struct BringRequestMessage : NetworkMessage
    {
        public uint NetId;
    }

    /// <summary>
    /// Player admin actions. Most go through a game Command marked <c>requiresAuthority = false</c>, so they work
    /// from the host and from a client alike; Bring needs the host.
    /// </summary>
    internal static class PlayerActions
    {
        /// <summary>
        /// Host-only, except for trusted admins (see <see cref="Access"/>). This is the mod's own rule, not a technical
        /// limit: the game accepts these Commands from any client.
        /// </summary>
        public static bool ActionsAllowed => Plugin.Enabled.Value && Access.Allowed;

        private static bool _bringHandler;

        static PlayerActions()
        {
            Writer<BringRequestMessage>.write = (writer, message) => writer.WriteUInt(message.NetId);
            Reader<BringRequestMessage>.read = reader => new BringRequestMessage { NetId = reader.ReadUInt() };
        }

        public static void Update()
        {
            // Mirror drops every handler when the server shuts down, so register again after each start.
            if (NetworkServer.active && !_bringHandler)
            {
                NetworkServer.ReplaceHandler<BringRequestMessage>(OnBringRequest);
                _bringHandler = true;
            }
            else if (!NetworkServer.active)
            {
                _bringHandler = false;
            }
        }

        public static List<FirstPersonController> Players() =>
            FirstPersonController.LocalPlayers
                .Where(p => p != null && p.PlayerStats != null)
                .OrderBy(p => p.playerName)
                .ToList();

        /// <summary>
        /// The local player, or null if none has spawned yet. Written as a plain loop rather than
        /// <c>FirstOrDefault</c> because the per-frame callers would otherwise box a
        /// <see cref="List{T}"/> enumerator every frame.
        /// </summary>
        public static FirstPersonController LocalPlayer()
        {
            var players = FirstPersonController.LocalPlayers;
            if (players == null)
                return null;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player != null && player.isLocalPlayer)
                    return player;
            }
            return null;
        }

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
            // A carried body has its model and collider switched off until it's dropped, and the carrier keeps
            // holding it, so drop it first the way the revive shrine does. The Commands run in order on the host.
            var position = player.transform.position;
            var carrier = CarrierOf(player);
            if (carrier != null)
            {
                position = carrier.transform.position + carrier.transform.forward;
                carrier.CmdReleaseDeadPlayer(carrier, player, position);
                carrier.PlayerEquipment.CmdSetCarriedDeadPlayer(null, null);
            }
            // The revive tool restores health and plays the game's own revive flow; ForceIsDead alone would
            // clear the flag but leave the player at zero health.
            player.CmdUseReviveTool(player, position, 1f);
            Plugin.Log.LogInfo($"Revived {player.playerName}");
        }

        /// <summary>The player carrying <paramref name="deadPlayer"/>'s body, or null.</summary>
        private static FirstPersonController CarrierOf(FirstPersonController deadPlayer)
        {
            if (deadPlayer.PlayerCarryHandler == null || !deadPlayer.PlayerCarryHandler.IsCarried)
                return null;
            foreach (var other in Players())
                if (other != null && other.PlayerEquipment != null && other.PlayerEquipment.DeadPlayerCarried == deadPlayer)
                    return other;
            return null;
        }

        public static void Heal(FirstPersonController player)
        {
            if (!ActionsAllowed || player == null || player.PlayerStats == null)
                return;
            var stats = player.PlayerStats;
            stats.CmdUpdateHealth(MaxHealthOf(stats) - stats.GetCurrentHealth());
            Plugin.Log.LogInfo($"Healed {player.playerName}");
        }

        /// <summary>Moves the local player to <paramref name="target"/>. Players own their own position, so this needs no host.</summary>
        public static void TeleportTo(FirstPersonController target)
        {
            var self = LocalPlayer();
            if (target == null || target == self)
                return;
            if (MoveSelf(target.transform.position - target.transform.forward * 1.5f))
                Plugin.Log.LogInfo($"Teleported to {target.playerName}");
        }

        /// <summary>The level's campfire, or null outside a level (e.g. in the hub).</summary>
        public static CampFire Campfire() => LevelManager.Instance != null ? LevelManager.Instance.CampFire : null;

        /// <summary>
        /// Moves the local player to the campfire. Picking a spot beside the fire by raycasting could start the
        /// ray inside the cave rock and drop you under the map, so this uses the level's player spawn, the
        /// same point the game's own "unstuck" button moves you to, when it's near the fire.
        /// </summary>
        public static void TeleportToCampfire()
        {
            var campfire = Campfire();
            if (campfire == null)
                return;
            var spawn = GameManager.Instance != null ? GameManager.Instance.playerSpawn : null;
            Vector3 position;
            if (spawn != null && Vector3.Distance(spawn.position, campfire.transform.position) <= CampfireSpawnRange)
                position = spawn.position;
            else if (Physics.Raycast(campfire.transform.position + Vector3.up * 1.5f, Vector3.down, out var hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                position = hit.point + Vector3.up * 0.5f;
            else
                position = campfire.transform.position + Vector3.up * 1f;
            if (MoveSelf(position))
                Plugin.Log.LogInfo($"Teleported to the campfire ({(spawn != null && position == spawn.position ? "player spawn" : "fire")})");
        }

        private const float CampfireSpawnRange = 40f;

        private static bool MoveSelf(Vector3 position)
        {
            var self = LocalPlayer();
            if (!Plugin.Enabled.Value || self == null)
                return false;
            // Same dance the game does for ladders: a live CharacterController overwrites a moved transform.
            var controller = self.Controller;
            if (controller != null) controller.enabled = false;
            self.transform.position = position;
            if (controller != null) controller.enabled = true;
            if (self.SmoothSyncMirror != null)
            {
                self.SmoothSyncMirror.clearBuffer();
                self.SmoothSyncMirror.teleportOwnedObjectFromOwner();
            }
            return true;
        }

        /// <summary>
        /// Pulls <paramref name="target"/> to the local player. Positions are owner-authoritative through
        /// SmoothSync, and only the server can tell another client to move (the RPC behind
        /// <c>teleportAnyObjectFromServer</c>), so a client can only do this by asking a host that runs this mod.
        /// </summary>
        public static bool CanBring => NetworkServer.active || Access.RoutesThroughHost;

        public static void Bring(FirstPersonController target)
        {
            var self = LocalPlayer();
            if (!CanBring || !ActionsAllowed || self == null || target == null || target == self)
                return;
            if (NetworkServer.active)
                BringTo(target, self);
            else
                NetworkClient.Send(new BringRequestMessage { NetId = target.netId });
            Plugin.Log.LogInfo($"Brought {target.playerName}");
        }

        private static void OnBringRequest(NetworkConnectionToClient sender, BringRequestMessage message)
        {
            if (!Access.IsTrusted(sender))
            {
                Plugin.Log.LogWarning($"Ignored a bring request from untrusted connection {sender.address}");
                return;
            }
            var requester = Players().FirstOrDefault(p => p.connectionToClient == sender);
            var target = Players().FirstOrDefault(p => p.netId == message.NetId);
            if (requester == null || target == null || target == requester)
                return;
            BringTo(target, requester);
            Plugin.Log.LogInfo($"{requester.playerName} brought {target.playerName}");
        }

        private static void BringTo(FirstPersonController target, FirstPersonController destination)
        {
            if (target.SmoothSyncMirror == null)
                return;
            var position = destination.transform.position + destination.transform.forward * 1.5f;
            target.SmoothSyncMirror.teleportAnyObjectFromServer(position, target.transform.rotation, target.transform.localScale);
        }

        /// <summary>
        /// The health cap the server actually clamps to. <see cref="PlayerStats.ServerUpdateHealth"/> clamps to
        /// the synced <see cref="PlayerStats.NetworksyncMaxHealth"/>, and on a plain client the local
        /// <see cref="PlayerStats.MaxHealth"/> stat never sees upgrades applied on the server — reading it would
        /// heal only up to base health.
        /// </summary>
        public static float MaxHealthOf(PlayerStats stats) => stats.NetworksyncMaxHealth;
    }
}
