using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>A stat value sent between peers that both run the admin menu. NaN resets the stat.</summary>
    internal struct StatBonusMessage : NetworkMessage
    {
        public uint NetId;
        public string Stat;
        public float Value;
    }

    /// <summary>Asks the peer that holds a player's real stats to report them. See <see cref="StatSnapshots"/>.</summary>
    internal struct StatQueryMessage : NetworkMessage
    {
        public uint NetId;
    }

    /// <summary>A player's stat values in display units, one per <see cref="StatEditor.RingStats"/> entry, in order.</summary>
    internal struct StatSnapshotMessage : NetworkMessage
    {
        public uint NetId;
        public float[] Values;
    }

    /// <summary>
    /// Sets any of the stats that level-up rings raise to a chosen value, per player, by adding one flat
    /// modifier sized so the stat's final value lands on the target.
    ///
    /// Each peer keeps its own copy of every player's stats: the host's copy decides damage and max
    /// health, the owner's copy decides movement, digging and attack speed. The copies can differ (a client
    /// only sees other players' base stats), so peers send the target value and each sizes its own modifier.
    /// A value therefore has to be
    /// applied on the host and on the player's own client. The game has no message for that, so this
    /// sends its own, and only to peers running this mod (see <see cref="Access"/>).
    ///
    /// Reading works the same way round. Since a peer holds only base values for somebody else's player,
    /// an admin looking at another player asks that player's own client for its values and shows those
    /// instead; see <see cref="Request"/> and <see cref="StatSnapshots"/>.
    /// </summary>
    internal static class StatEditor
    {
        private const string Source = "AdminMenu.StatBonus";

        /// <summary>The <see cref="PlayerStats"/> properties that level-up upgrades modify.</summary>
        public static readonly string[] RingStats =
        {
            "MaxHealth", "HealthRegen", "Defense", "DodgeChance", "LifeStealPercentage", "DamageReflect",
            "HealOnBlockChance", "ChanceBonusMaxHealthPerKill", "MaxStamina", "MaxMana", "AttackPower",
            "MeleeDamageMultiplier", "RangedDamageMultiplier", "MagicPower", "HealingPower", "CritChance",
            "CritDamage", "CritChanceToHeal", "BerserkDamageMultiplier", "BonusDamageEliteBoss",
            "DamageToNearbyEnemiesMultiplier", "DamageToEnemiesAboveHpMultiplier", "ChanceOfKnockback",
            "MeleeAttackSpeed", "MagicSpeed", "MagicRange", "ProjectileSpeed", "ProjectileBonusChance",
            "BounceArrowChance", "ExplosionDamageMultiplier", "ExplosionSpeed", "ExplosionRangeMultiplier",
            "CompanionDamageMultiplier", "CompanionMaxHealth", "SpeedMultiplier", "SprintSpeedMultiplier",
            "JumpHeightMultiplier", "DigStrength", "DiggingSpeed", "BonusResourceCount", "DropItemChance",
            "ItemMaxStackMultiplier", "CompassRange",
        };

        // Stats this peer has overridden, keyed by the stat object so a respawned player starts clean.
        private static readonly HashSet<CharacterStat> Applied = new HashSet<CharacterStat>();

        // The values set this level, by player netId then stat name. Changing class rebuilds every stat object
        // and drops the modifiers, so these are put back when a stat turns up without one.
        private static readonly Dictionary<uint, Dictionary<string, float>> Targets = new Dictionary<uint, Dictionary<string, float>>();
        private const float ReapplyIntervalSeconds = 0.5f;
        private static float _nextReapply;

        // What other players' own clients last reported, and how often we ask them for it.
        private const float SnapshotFreshForSeconds = 3f;
        private const float QueryIntervalSeconds = 1f;
        private static readonly StatSnapshots Snapshots = new StatSnapshots(RingStats.Length, SnapshotFreshForSeconds);
        private static readonly Dictionary<uint, float> NextQuery = new Dictionary<uint, float>();

        // Host only: the admins waiting on a snapshot of a player, by that player's netId, with the time
        // they asked so a viewer who closed the panel or left is forgotten rather than sent a reply.
        private static readonly Dictionary<uint, Dictionary<NetworkConnectionToClient, float>> Waiting =
            new Dictionary<uint, Dictionary<NetworkConnectionToClient, float>>();
        private const float WaitingExpirySeconds = 5f;

        private static bool _serverHandler;
        private static bool _clientHandler;

        static StatEditor()
        {
            Writer<StatBonusMessage>.write = (writer, message) =>
            {
                writer.WriteUInt(message.NetId);
                writer.WriteString(message.Stat);
                writer.WriteFloat(message.Value);
            };
            Reader<StatBonusMessage>.read = reader => new StatBonusMessage
            {
                NetId = reader.ReadUInt(),
                Stat = reader.ReadString(),
                Value = reader.ReadFloat(),
            };

            Writer<StatQueryMessage>.write = (writer, message) => writer.WriteUInt(message.NetId);
            Reader<StatQueryMessage>.read = reader => new StatQueryMessage { NetId = reader.ReadUInt() };

            Writer<StatSnapshotMessage>.write = (writer, message) =>
            {
                var values = message.Values ?? Array.Empty<float>();
                writer.WriteUInt(message.NetId);
                writer.WriteInt(values.Length);
                foreach (var value in values)
                    writer.WriteFloat(value);
            };
            Reader<StatSnapshotMessage>.read = reader =>
            {
                var netId = reader.ReadUInt();
                var count = reader.ReadInt();
                // The host reads these from clients, so a length is something to be talked into, not trusted.
                // Anything but this build's stat list is dropped by StatSnapshots anyway.
                if (count < 0 || count > MaxSnapshotValues)
                    return new StatSnapshotMessage { NetId = netId, Values = Array.Empty<float>() };
                var values = new float[count];
                for (var i = 0; i < count; i++)
                    values[i] = reader.ReadFloat();
                return new StatSnapshotMessage { NetId = netId, Values = values };
            };
        }

        private const int MaxSnapshotValues = 256;

        public static CharacterStat Get(PlayerStats stats, string name)
        {
            return stats == null ? null : typeof(PlayerStats).GetProperty(name)?.GetValue(stats) as CharacterStat;
        }

        /// <summary>Splits "DamageToNearbyEnemiesMultiplier" into "Damage to nearby enemies multiplier".</summary>
        public static string Label(string name)
        {
            var text = new System.Text.StringBuilder();
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                    text.Append(' ').Append(char.ToLowerInvariant(c));
                else
                    text.Append(c);
            }
            return text.ToString().Replace("Hp", "HP").Replace(" hp", " HP");
        }

        /// <summary>Percentages are entered as whole percents but stored as fractions.</summary>
        public static float Scale(CharacterStat stat)
        {
            return stat._formatType == FormatType.Percentage || stat._formatType == FormatType.PercentageBySecond ? 100f : 1f;
        }

        /// <summary>The stat's current value in display units, e.g. 25 for a 25% stat.</summary>
        public static float DisplayValue(CharacterStat stat)
        {
            return stat == null ? 0f : stat.Value * Scale(stat);
        }

        /// <summary>
        /// The value to show for stat number <paramref name="index"/>: what the player's own client reported,
        /// when that's in hand, otherwise this peer's copy. Only another player's values can be reported --
        /// your own are already the real ones.
        /// </summary>
        public static float ShownValue(FirstPersonController player, int index, CharacterStat stat)
        {
            if (player != null && !player.isLocalPlayer && Snapshots.TryGet(player.netId, index, Time.unscaledTime, out var value))
                return value;
            return DisplayValue(stat);
        }

        /// <summary>Whether the values shown for <paramref name="player"/> are their real ones.</summary>
        public static bool HasRealValues(FirstPersonController player)
        {
            return player != null && (player.isLocalPlayer || Snapshots.IsFresh(player.netId, Time.unscaledTime));
        }

        /// <summary>
        /// Asks for <paramref name="player"/>'s real stats, at most once a second per player. Called while
        /// their Stats panel is open: the values are only wanted while somebody is looking at them, and a
        /// player's upgrades change as they level, so one reply wouldn't stay true.
        /// </summary>
        public static void Request(FirstPersonController player)
        {
            if (player == null || player.isLocalPlayer || !Access.Allowed)
                return;
            var now = Time.unscaledTime;
            if (NextQuery.TryGetValue(player.netId, out var next) && now < next)
                return;

            var message = new StatQueryMessage { NetId = player.netId };
            if (NetworkServer.active)
            {
                // The host's own copy is no better than a client's for somebody else's player, so it asks
                // the owner directly. A player without the mod has no handler to ask.
                var owner = player.connectionToClient;
                if (owner == null || owner == NetworkServer.localConnection || !Access.HasMod(player))
                    return;
                owner.Send(message);
            }
            else if (NetworkClient.active && Access.HostHasMod())
            {
                NetworkClient.Send(message);
            }
            else
            {
                return;
            }
            NextQuery[player.netId] = now + QueryIntervalSeconds;
        }

        public static bool IsOverridden(CharacterStat stat)
        {
            return stat != null && Applied.Contains(stat);
        }

        /// <summary>Why a bonus can't be sent for this player right now, or null when it can.</summary>
        public static string BlockedReason(FirstPersonController player)
        {
            if (player == null || player.PlayerStats == null)
                return "This player hasn't loaded yet.";
            if (player.isLocalPlayer || NetworkServer.active)
                return null;
            if (!Access.HostHasMod())
                return "The host needs the admin menu installed to change stats.";
            return null;
        }

        /// <summary>A note shown when a bonus only partly applies, or null when it fully applies.</summary>
        public static string PartialReason(FirstPersonController player)
        {
            if (player == null)
                return null;
            if (!player.isLocalPlayer && !Access.HasMod(player))
                return $"{player.playerName} doesn't have the admin menu, so these are the stats this peer holds for them rather than their own, and only host-side stats (health, defense, damage) change.";
            if (!player.isLocalPlayer && !HasRealValues(player))
                return $"Waiting for {player.playerName}'s own values; until they arrive these are base stats without their upgrades. The value you set lands exactly either way.";
            if (player.isLocalPlayer && !NetworkServer.active && !Access.HostHasMod())
                return "The host doesn't have the admin menu, so host-side stats (health, defense, damage) won't change.";
            return null;
        }

        /// <summary>Sets the stat to <paramref name="value"/> in display units, or resets it when NaN.</summary>
        public static void Set(FirstPersonController player, string statName, float value)
        {
            if (BlockedReason(player) != null)
                return;

            var message = new StatBonusMessage { NetId = player.netId, Stat = statName, Value = value };
            ApplyLocal(player, statName, value);

            if (NetworkServer.active)
                ForwardToOwner(player, message, null);
            else if (NetworkClient.active && Access.HostHasMod())
                NetworkClient.Send(message);

            Plugin.Log.LogInfo(float.IsNaN(value)
                ? $"Reset {player.playerName}'s {Label(statName)}"
                : $"Set {player.playerName}'s {Label(statName)} to {value}");
        }

        public static void Update()
        {
            RegisterHandlers();

            // Values last until the level ends; the next level spawns every player afresh.
            if (GameManager.Instance == null)
            {
                Applied.Clear();
                Targets.Clear();
                Snapshots.Clear();
                NextQuery.Clear();
                Waiting.Clear();
                return;
            }
            DropExpiredWaiters();
            if (Targets.Count > 0 && Time.unscaledTime >= _nextReapply)
            {
                _nextReapply = Time.unscaledTime + ReapplyIntervalSeconds;
                Reapply();
            }
        }

        private static void Reapply()
        {
            List<KeyValuePair<FirstPersonController, KeyValuePair<string, float>>> lost = null;
            foreach (var player in PlayerActions.Players())
            {
                if (!Targets.TryGetValue(player.netId, out var values))
                    continue;
                foreach (var entry in values)
                {
                    var stat = Get(player.PlayerStats, entry.Key);
                    if (stat == null || Applied.Contains(stat))
                        continue;
                    if (lost == null)
                        lost = new List<KeyValuePair<FirstPersonController, KeyValuePair<string, float>>>();
                    lost.Add(new KeyValuePair<FirstPersonController, KeyValuePair<string, float>>(player, entry));
                }
            }
            if (lost == null)
                return;
            foreach (var item in lost)
            {
                ApplyLocal(item.Key, item.Value.Key, item.Value.Value);
                Plugin.Log.LogInfo($"Re-applied {item.Key.playerName}'s {Label(item.Value.Key)} ({item.Value.Value}) after their stats were rebuilt");
            }
        }

        private static void RegisterHandlers()
        {
            // Mirror drops every handler when the server or client shuts down, so register again after each start.
            if (NetworkServer.active && !_serverHandler)
            {
                NetworkServer.ReplaceHandler<StatBonusMessage>(OnServerMessage);
                NetworkServer.ReplaceHandler<StatQueryMessage>(OnServerQuery);
                NetworkServer.ReplaceHandler<StatSnapshotMessage>(OnServerSnapshot);
                _serverHandler = true;
            }
            else if (!NetworkServer.active)
            {
                _serverHandler = false;
            }

            if (NetworkClient.active && !_clientHandler)
            {
                NetworkClient.ReplaceHandler<StatBonusMessage>(OnClientMessage);
                NetworkClient.ReplaceHandler<StatQueryMessage>(OnClientQuery);
                NetworkClient.ReplaceHandler<StatSnapshotMessage>(OnClientSnapshot);
                _clientHandler = true;
            }
            else if (!NetworkClient.active)
            {
                _clientHandler = false;
            }
        }

        private static void OnServerMessage(NetworkConnectionToClient sender, StatBonusMessage message)
        {
            if (!Access.IsTrusted(sender))
            {
                Plugin.Log.LogWarning($"Ignored a stat change from untrusted connection {sender.address}");
                return;
            }
            var player = FindPlayer(message.NetId);
            if (player == null)
                return;
            ApplyLocal(player, message.Stat, message.Value);
            ForwardToOwner(player, message, sender);
            Plugin.Log.LogInfo($"A client set {player.playerName}'s {Label(message.Stat)} to {message.Value}");
        }

        private static void OnClientMessage(StatBonusMessage message)
        {
            // On a host the server handler already applied it to the shared copy.
            if (NetworkServer.active)
                return;
            var player = FindPlayer(message.NetId);
            if (player != null)
                ApplyLocal(player, message.Stat, message.Value);
        }

        /// <summary>
        /// An admin asking for a player's real stats. The player's own client is the peer that has them, so
        /// the question is passed on when that client runs the mod; failing that the host answers from its
        /// own copy, which at least carries the host-side stats.
        /// </summary>
        private static void OnServerQuery(NetworkConnectionToClient sender, StatQueryMessage message)
        {
            if (!Access.IsTrusted(sender))
            {
                Plugin.Log.LogWarning($"Ignored a stat query from untrusted connection {sender.address}");
                return;
            }
            var player = FindPlayer(message.NetId);
            if (player == null)
                return;

            var owner = player.connectionToClient;
            if (owner != null && owner != NetworkServer.localConnection && owner != sender && Access.HasMod(player))
            {
                Remember(message.NetId, sender);
                owner.Send(message);
                return;
            }
            sender.Send(Capture(player));
        }

        /// <summary>A player's own client answering for itself. Nobody else's stats are reported from here.</summary>
        private static void OnClientQuery(StatQueryMessage message)
        {
            // On a host the server handler already dealt with it.
            if (NetworkServer.active)
                return;
            var player = FindPlayer(message.NetId);
            if (player != null && player.isLocalPlayer)
                NetworkClient.Send(Capture(player));
        }

        /// <summary>
        /// A client reporting its own player's stats, which the host keeps for its own panel and relays to
        /// whichever admins asked. Only the connection that owns the player may report for it, so a client
        /// can't feed everyone else made-up numbers for somebody they're not.
        /// </summary>
        private static void OnServerSnapshot(NetworkConnectionToClient sender, StatSnapshotMessage message)
        {
            var player = FindPlayer(message.NetId);
            if (player == null)
                return;
            if (player.connectionToClient != sender)
            {
                Plugin.Log.LogWarning($"Ignored a stat report for {player.playerName} from connection {sender.address}, which doesn't own them");
                return;
            }

            Snapshots.Store(message.NetId, message.Values, Time.unscaledTime);
            if (!Waiting.TryGetValue(message.NetId, out var waiters))
                return;
            Waiting.Remove(message.NetId);
            foreach (var waiter in waiters)
                if (waiter.Key.isReady && Access.IsTrusted(waiter.Key))
                    waiter.Key.Send(message);
        }

        private static void OnClientSnapshot(StatSnapshotMessage message)
        {
            // On a host the server handler already stored it.
            if (NetworkServer.active)
                return;
            Snapshots.Store(message.NetId, message.Values, Time.unscaledTime);
        }

        /// <summary>This peer's values for <paramref name="player"/>, in <see cref="RingStats"/> order.</summary>
        private static StatSnapshotMessage Capture(FirstPersonController player)
        {
            var values = new float[RingStats.Length];
            for (var i = 0; i < RingStats.Length; i++)
                values[i] = DisplayValue(Get(player.PlayerStats, RingStats[i]));
            return new StatSnapshotMessage { NetId = player.netId, Values = values };
        }

        private static void Remember(uint netId, NetworkConnectionToClient waiter)
        {
            if (!Waiting.TryGetValue(netId, out var waiters))
                Waiting[netId] = waiters = new Dictionary<NetworkConnectionToClient, float>();
            waiters[waiter] = Time.unscaledTime;
        }

        /// <summary>
        /// Forgets admins whose answer never came, so a player whose client stopped replying -- or who left
        /// mid-question -- doesn't leave connections held here for the rest of the session.
        /// </summary>
        private static void DropExpiredWaiters()
        {
            if (Waiting.Count == 0)
                return;
            var cutoff = Time.unscaledTime - WaitingExpirySeconds;
            List<uint> empty = null;
            foreach (var entry in Waiting)
            {
                List<NetworkConnectionToClient> stale = null;
                foreach (var waiter in entry.Value)
                    if (waiter.Value < cutoff)
                        (stale ?? (stale = new List<NetworkConnectionToClient>())).Add(waiter.Key);
                if (stale != null)
                    foreach (var waiter in stale)
                        entry.Value.Remove(waiter);
                if (entry.Value.Count == 0)
                    (empty ?? (empty = new List<uint>())).Add(entry.Key);
            }
            if (empty == null)
                return;
            foreach (var netId in empty)
                Waiting.Remove(netId);
        }

        private static void ForwardToOwner(FirstPersonController player, StatBonusMessage message, NetworkConnectionToClient sender)
        {
            var owner = player.connectionToClient;
            if (owner == null || owner == NetworkServer.localConnection || owner == sender || !Access.HasMod(player))
                return;
            owner.Send(message);
        }

        private static void ApplyLocal(FirstPersonController player, string statName, float value)
        {
            var stats = player.PlayerStats;
            var stat = Get(stats, statName);
            if (stat == null)
            {
                Plugin.Log.LogWarning($"Unknown stat {statName}");
                return;
            }

            stat.RemoveAllModifiersFromSource(Source);
            Applied.Remove(stat);
            Targets.TryGetValue(player.netId, out var values);
            values?.Remove(statName);
            if (!float.IsNaN(value))
            {
                // Final value = (base + flat) * multipliers, so a flat modifier of f moves it by f * multipliers.
                // Measure the multipliers with a one-point preview rather than re-deriving the game's formula.
                var without = stat.Value;
                var perPoint = stat.GetValueWithModifierPreview(new StatModifier(1f, StatModType.FLAT, Source)) - without;
                if (Mathf.Approximately(perPoint, 0f))
                {
                    Plugin.Log.LogWarning($"Can't set {Label(statName)}: its multipliers are zero");
                    return;
                }
                stat.AddModifier(new StatModifier((value / Scale(stat) - without) / perPoint, StatModType.FLAT, Source));
                Applied.Add(stat);
                if (values == null)
                    Targets[player.netId] = values = new Dictionary<string, float>();
                values[statName] = value;
            }

            // The host copies these into SyncVars only when told to, and the game reads the SyncVars, not the stats.
            if (NetworkServer.active)
            {
                switch (statName)
                {
                    case "MaxHealth": stats.ServerRefreshSyncMaxHealth(); break;
                    case "DiggingSpeed": stats.ServerRefreshSyncDiggingSpeed(); break;
                    case "MeleeAttackSpeed": stats.ServerRefreshSyncMeleeSpeed(); break;
                    case "MagicSpeed": stats.ServerRefreshSyncMagicSpeed(); break;
                }
            }
        }

        private static FirstPersonController FindPlayer(uint netId)
        {
            foreach (var player in PlayerActions.Players())
                if (player != null && player.netId == netId)
                    return player;
            return null;
        }
    }
}
