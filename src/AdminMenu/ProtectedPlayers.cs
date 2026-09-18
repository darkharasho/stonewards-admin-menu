using System.Collections.Generic;

namespace AdminMenu
{
    /// <summary>
    /// The connections the host is holding invulnerable on behalf of a client's god mode, as plain ids so the
    /// bookkeeping can be tested without Mirror or the game.
    ///
    /// God mode has to be enforced on the host to work at all. The game decides damage and death together in
    /// one server-side call -- <c>PlayerStats.ServerUpdateHealth</c> clamps the health SyncVar and sets
    /// <c>isDead</c> the moment it reaches zero -- so a client noticing its own health drop and healing back
    /// afterwards is always too late. A volley big enough to cover the whole health bar killed outright.
    /// </summary>
    internal sealed class ProtectedPlayers
    {
        private readonly HashSet<int> _connections = new HashSet<int>();

        public int Count => _connections.Count;

        public bool Contains(int connectionId) => _connections.Contains(connectionId);

        /// <summary>
        /// Starts or stops protecting <paramref name="connectionId"/>. Returns whether that changed anything,
        /// so a client re-sending the state it already sent doesn't read as a toggle.
        /// </summary>
        public bool Set(int connectionId, bool isProtected)
        {
            return isProtected ? _connections.Add(connectionId) : _connections.Remove(connectionId);
        }

        /// <summary>
        /// Drops everyone not in <paramref name="liveConnectionIds"/>. Mirror hands out connection ids from a
        /// counter that restarts with the server, so a left-behind entry would eventually grant a completely
        /// different player somebody else's god mode.
        /// </summary>
        public void PruneTo(IEnumerable<int> liveConnectionIds)
        {
            var live = liveConnectionIds as ICollection<int> ?? new List<int>(liveConnectionIds);
            if (_connections.Count == 0)
                return;
            List<int> stale = null;
            foreach (var id in _connections)
                if (!live.Contains(id))
                    (stale ?? (stale = new List<int>())).Add(id);
            if (stale == null)
                return;
            foreach (var id in stale)
                _connections.Remove(id);
        }

        public void Clear() => _connections.Clear();

        /// <summary>
        /// Whether a health change should be vetoed. Only negative amounts are blocked: this sits in the
        /// funnel every absolute health change goes through, so healing and regen must pass untouched.
        /// </summary>
        public static bool BlocksDamage(float amount, bool isProtected) => amount < 0f && isProtected;
    }
}
