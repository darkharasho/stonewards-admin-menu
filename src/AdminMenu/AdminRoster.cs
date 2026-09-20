using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminMenu
{
    /// <summary>One saved admin: the Steam ID that grants access, and a name to recognise them by.</summary>
    internal struct AdminEntry
    {
        public ulong Id;
        public string Name;
    }

    /// <summary>
    /// The admins the host granted from the menu, kept as plain IDs and names so the bookkeeping can be
    /// tested without Steam or the game.
    ///
    /// Access is decided by Steam ID alone; the name is a label, captured when the grant is made and
    /// refreshed when that player is seen again, so the Admins tab can show something more useful than a
    /// 17-digit number. It is stored alongside the ID rather than looked up later because a host usually
    /// wants to revoke somebody who isn't online, and there is nothing to look a name up from then.
    /// </summary>
    internal sealed class AdminRoster
    {
        private const char EntrySeparator = ',';
        private const char NameSeparator = '=';
        private static readonly char[] Separators = { ',', ';' };

        private readonly Dictionary<ulong, string> _admins = new Dictionary<ulong, string>();

        /// <summary>
        /// Reads a stored list, skipping anything that isn't an ID so a hand-edited config can't throw at
        /// startup. A bare ID with no name is kept: that's what hand-editing one in looks like.
        /// </summary>
        public static AdminRoster Parse(string stored)
        {
            var roster = new AdminRoster();
            if (string.IsNullOrEmpty(stored))
                return roster;

            foreach (var part in stored.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                var split = part.IndexOf(NameSeparator);
                var id = split < 0 ? part : part.Substring(0, split);
                var name = split < 0 ? string.Empty : part.Substring(split + 1);
                if (ulong.TryParse(id.Trim(), out var steamId) && steamId != 0)
                    roster._admins[steamId] = Clean(name);
            }
            return roster;
        }

        /// <summary>
        /// The list as it is stored. Ordered by ID because it is written back whenever it changes and
        /// compared against what's already there: a dictionary's enumeration order is not something to
        /// compare strings from, and unsorted the same roster could look changed and be rewritten every time.
        /// </summary>
        public string Format()
        {
            return string.Join(EntrySeparator.ToString(), _admins
                .OrderBy(admin => admin.Key)
                .Select(admin => admin.Value.Length == 0 ? admin.Key.ToString() : admin.Key + NameSeparator.ToString() + admin.Value));
        }

        public IEnumerable<ulong> Ids => _admins.Keys;

        /// <summary>Everyone on the roster, by name, which is the order the menu lists them in.</summary>
        public IEnumerable<AdminEntry> Entries => _admins
            .OrderBy(admin => admin.Value, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(admin => admin.Key)
            .Select(admin => new AdminEntry { Id = admin.Key, Name = admin.Value });

        public bool Contains(ulong id) => _admins.ContainsKey(id);

        /// <summary>The stored name for <paramref name="id"/>, or nothing for an ID the roster doesn't hold.</summary>
        public string NameOf(ulong id)
        {
            return _admins.TryGetValue(id, out var name) ? name : string.Empty;
        }

        /// <summary>
        /// Grants admin to <paramref name="id"/>, or updates the name of somebody already on the roster.
        /// Returns whether that changed anything, so the config is only written when it really differs.
        /// A zero ID -- a player whose Steam ID hasn't synced yet -- is refused: storing it would grant admin
        /// to everyone else whose ID is likewise unset.
        /// </summary>
        public bool Set(ulong id, string name)
        {
            if (id == 0)
                return false;
            var clean = Clean(name);
            if (_admins.TryGetValue(id, out var existing) && existing == clean)
                return false;
            _admins[id] = clean;
            return true;
        }

        /// <summary>Revokes admin from <paramref name="id"/>, reporting whether they were on the roster.</summary>
        public bool Remove(ulong id) => _admins.Remove(id);

        /// <summary>
        /// A name safe to store next to an ID. Players choose their own names, so one can hold the
        /// separators this format is built from; they become spaces rather than splitting the entry in two
        /// and taking the next admin with it.
        /// </summary>
        private static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            var cleaned = name;
            foreach (var separator in new[] { EntrySeparator, NameSeparator, ';' })
                cleaned = cleaned.Replace(separator, ' ');
            return cleaned.Trim();
        }
    }
}
