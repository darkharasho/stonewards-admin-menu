using System.Collections.Generic;

namespace AdminMenu
{
    /// <summary>
    /// The stat values other peers have reported for their own players, as plain numbers so the bookkeeping
    /// can be tested without Mirror or the game.
    ///
    /// The game syncs only four stat values (max health, digging speed, melee and magic attack speed); every
    /// other <see cref="CharacterStat"/> is worked out on each peer from the upgrades that peer knows about.
    /// A peer therefore holds nothing but base values for somebody else's player, which is why the Stats
    /// panel used to show an admin the wrong numbers for everyone but themselves even while setting them
    /// worked. <see cref="StatEditor"/> asks the player's own client for the real ones and puts the reply
    /// here; the panel prefers it over the local copy for as long as it stays fresh.
    ///
    /// Snapshots are positional -- one value per entry of <see cref="StatEditor.RingStats"/>, in that order --
    /// so one from a build with a different stat list is dropped rather than read against the wrong names.
    /// </summary>
    internal sealed class StatSnapshots
    {
        private readonly struct Snapshot
        {
            public Snapshot(float[] values, float received)
            {
                Values = values;
                Received = received;
            }

            public float[] Values { get; }
            public float Received { get; }
        }

        private readonly Dictionary<uint, Snapshot> _snapshots = new Dictionary<uint, Snapshot>();
        private readonly int _statCount;
        private readonly float _freshForSeconds;

        public StatSnapshots(int statCount, float freshForSeconds)
        {
            _statCount = statCount;
            _freshForSeconds = freshForSeconds;
        }

        /// <summary>
        /// Records what a peer reported for <paramref name="netId"/>. Returns whether it was kept: a reply
        /// that doesn't carry exactly one value per stat came from a different build of the mod and is
        /// ignored, leaving any earlier snapshot in place.
        /// </summary>
        public bool Store(uint netId, float[] values, float now)
        {
            if (values == null || values.Length != _statCount)
                return false;
            _snapshots[netId] = new Snapshot(values, now);
            return true;
        }

        /// <summary>The reported value of stat <paramref name="index"/>, if one arrived recently enough.</summary>
        public bool TryGet(uint netId, int index, float now, out float value)
        {
            value = 0f;
            if (index < 0 || index >= _statCount || !TryGetFresh(netId, now, out var snapshot))
                return false;
            value = snapshot.Values[index];
            return true;
        }

        /// <summary>Whether real values are in hand for this player, which is what the panel's note turns on.</summary>
        public bool IsFresh(uint netId, float now) => TryGetFresh(netId, now, out _);

        /// <summary>Drops everything. Levels end by respawning every player, with fresh netIds.</summary>
        public void Clear() => _snapshots.Clear();

        private bool TryGetFresh(uint netId, float now, out Snapshot snapshot)
        {
            return _snapshots.TryGetValue(netId, out snapshot) && now - snapshot.Received <= _freshForSeconds;
        }
    }
}
