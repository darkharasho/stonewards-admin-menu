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
        }

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
            if (!player.isLocalPlayer && !NetworkServer.active)
                return "As a client you see this player's base stats without their upgrades; the value you set still lands exactly for them and the host.";
            if (!player.isLocalPlayer && !Access.HasMod(player))
                return $"{player.playerName} doesn't have the admin menu, so only host-side stats (health, defense, damage) change.";
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

            // Forget stat objects whose player left or respawned.
            if (Applied.Count > 0 && GameManager.Instance == null)
                Applied.Clear();
        }

        private static void RegisterHandlers()
        {
            // Mirror drops every handler when the server or client shuts down, so register again after each start.
            if (NetworkServer.active && !_serverHandler)
            {
                NetworkServer.ReplaceHandler<StatBonusMessage>(OnServerMessage);
                _serverHandler = true;
            }
            else if (!NetworkServer.active)
            {
                _serverHandler = false;
            }

            if (NetworkClient.active && !_clientHandler)
            {
                NetworkClient.ReplaceHandler<StatBonusMessage>(OnClientMessage);
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
