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
            // Without a local player there is nothing to measure a player capsule with, let alone move.
            if (self == null || target == null || target == self)
                return;
            // Behind the target, or the nearest clear spot to that: the old fixed offset dropped you inside
            // whatever the target had their back to.
            if (MoveSelf(SpotNear(target.transform.position, -target.transform.forward, self)))
                Plugin.Log.LogInfo($"Teleported to {target.playerName}");
        }

        /// <summary>The level's campfire, or null outside a level (e.g. in the hub).</summary>
        public static CampFire Campfire() => LevelManager.Instance != null ? LevelManager.Instance.CampFire : null;

        /// <summary>
        /// Moves the local player to the campfire. Prefers the level's player spawn, the same point the game's
        /// own "unstuck" button moves you to, when it's near the fire: it is known to be clear, where a spot
        /// beside the fire has to be searched for and the fire can be built into the cave rock.
        /// </summary>
        public static void TeleportToCampfire()
        {
            var campfire = Campfire();
            if (campfire == null)
                return;
            var spawn = GameManager.Instance != null ? GameManager.Instance.playerSpawn : null;
            Vector3 position;
            var self = LocalPlayer();
            if (spawn != null && Vector3.Distance(spawn.position, campfire.transform.position) <= CampfireSpawnRange)
                position = spawn.position;
            else if (self != null)
                // Beside the fire, wherever a player fits. Offsetting up from the floor by hand put the capsule's
                // centre half a metre up, which is a player's feet below the floor and a push out through the map.
                position = SpotNear(campfire.transform.position, campfire.transform.forward, self);
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
            // In front of the requester, or the nearest clear spot to that. SmoothSync assigns the position
            // straight onto the target's live CharacterController, so a spot inside the level ejects the capsule
            // in whatever direction the overlap resolves -- routinely downward, out of the world.
            var position = SpotNear(destination.transform.position, destination.transform.forward, target);
            target.SmoothSyncMirror.teleportAnyObjectFromServer(position, target.transform.rotation, target.transform.localScale);
        }

        /// <summary>Rings tried inside the preferred distance, for gaps too tight for a full step.</summary>
        private const int SpotCloserRings = 1;

        /// <summary>Directions tried per ring; eight is every 45 degrees around the anchor.</summary>
        private const int SpotsPerRing = 8;

        /// <summary>How far below the anchor a candidate will accept a floor, so a ledge is a step down and not a drop.</summary>
        private const float FloorSearchDepth = 1.5f;

        /// <summary>
        /// Shrinks the capsule used to test a spot. A gap exactly as wide as a player would otherwise be
        /// rejected on float noise, and the controller's skin width means it never quite fills its own radius.
        /// </summary>
        private const float FitTolerance = 0.95f;

        /// <summary>
        /// A spot near <paramref name="anchor"/> that <paramref name="mover"/> fits in, preferring
        /// <paramref name="facing"/> at arm's length and sweeping around the anchor when that is blocked.
        /// Falls back to the anchor's own position, which is known to fit a player because one is standing in it.
        /// </summary>
        private static Vector3 SpotNear(Vector3 anchor, Vector3 facing, FirstPersonController mover)
        {
            var candidates = SpotSearch.Candidates(SpotSearch.PreferredDistance, SpotCloserRings, SpotsPerRing);
            var forward = Flatten(facing);
            var right = Vector3.Cross(Vector3.up, forward);
            var spot = anchor;
            SpotSearch.Pick(candidates, i =>
            {
                var point = anchor + forward * candidates[i].Forward + right * candidates[i].Right;
                var standable = StandablePoint(point, mover);
                if (standable == null)
                    return false;
                spot = standable.Value;
                return true;
            });
            return spot;
        }

        /// <summary>Flattens a look direction onto the ground plane, so candidates never aim into the sky or the floor.</summary>
        private static Vector3 Flatten(Vector3 direction)
        {
            var flat = new Vector3(direction.x, 0f, direction.z);
            return flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
        }

        /// <summary>
        /// Where <paramref name="mover"/> would stand at <paramref name="candidate"/>, or null if they would not
        /// fit there. Candidates arrive at the anchor's own height, which can be over a ledge or a little inside a
        /// step, so the floor is found first and the capsule tested where the player would come to rest.
        /// </summary>
        private static Vector3? StandablePoint(Vector3 candidate, FirstPersonController mover)
        {
            var controller = mover.Controller;
            if (controller == null)
                return candidate;
            var radius = controller.radius;
            var height = Mathf.Max(controller.height, radius * 2f);
            var mask = SolidMask(mover.gameObject.layer);

            // From head height down, so a candidate sunk into a step still finds the surface above it. A candidate
            // buried deep in rock starts inside the collider and finds nothing, which rejects it.
            if (!Physics.Raycast(candidate + Vector3.up * height, Vector3.down, out var floor, height + FloorSearchDepth, mask, QueryTriggerInteraction.Ignore))
                return null;

            // transform.position is the capsule's centre: the game pins CharacterController.center to zero every frame.
            var centre = floor.point + Vector3.up * (height * 0.5f + controller.skinWidth);
            var cap = Mathf.Max(0f, height * 0.5f - radius);
            if (Physics.CheckCapsule(centre - Vector3.up * cap, centre + Vector3.up * cap, radius * FitTolerance, mask, QueryTriggerInteraction.Ignore))
                return null;
            return centre;
        }

        private static int _solidMask;
        private static int _solidMaskLayer = -1;

        /// <summary>
        /// The layers a player's capsule actually collides with, taken from the physics matrix rather than
        /// guessed: those are exactly the ones that can push it out of the level. The player's own layer is left
        /// out so another player or a body standing in a spot does not rule it out -- two capsules resolve by
        /// sliding apart on the floor, which is harmless.
        /// </summary>
        private static int SolidMask(int playerLayer)
        {
            if (_solidMaskLayer == playerLayer)
                return _solidMask;
            var mask = 0;
            for (var layer = 0; layer < 32; layer++)
                if (layer != playerLayer && !Physics.GetIgnoreLayerCollision(playerLayer, layer))
                    mask |= 1 << layer;
            _solidMaskLayer = playerLayer;
            _solidMask = mask;
            return mask;
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
