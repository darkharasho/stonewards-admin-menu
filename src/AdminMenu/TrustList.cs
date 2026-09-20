using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminMenu
{
    /// <summary>
    /// Who the mod treats as an admin, as plain Steam IDs so the rule can be tested without Steam or the game.
    /// The sources are the host's configured <c>TrustedAdmins</c> list, the admins granted from the menu (see
    /// <see cref="AdminRoster"/>) and the mod author.
    ///
    /// The mod author is trusted on every install and cannot be configured away. Holding that grant in the
    /// config as a default was not enough: a host editing <c>TrustedAdmins</c> to add their own friends
    /// replaces the value and drops the author with it, so the grant kept getting lost by accident rather than
    /// on purpose. It is compiled in instead, documented on the mod's front page, and removable only by
    /// building the mod from source without it.
    /// </summary>
    internal static class TrustList
    {
        /// <summary>
        /// The IDs allowed to use the menu: whatever the host configured, plus <paramref name="ownerId"/>
        /// always. An unparseable owner ID is simply absent, so a typo can't throw at startup.
        /// </summary>
        public static HashSet<ulong> Resolve(string configuredList, string ownerId)
        {
            return Resolve(configuredList, ownerId, null);
        }

        /// <summary>
        /// As above, plus <paramref name="grantedIds"/> -- the players the host elevated from the menu, held
        /// in the <see cref="AdminRoster"/>. They join the configured list rather than replacing it: the
        /// config stays the fallback for granting admin to somebody who has never been in your lobby.
        /// </summary>
        public static HashSet<ulong> Resolve(string configuredList, string ownerId, IEnumerable<ulong> grantedIds)
        {
            var ids = Parse(configuredList);
            if (ulong.TryParse((ownerId ?? string.Empty).Trim(), out var owner))
                ids.Add(owner);
            if (grantedIds != null)
                foreach (var id in grantedIds)
                    if (id != 0)
                        ids.Add(id);
            return ids;
        }

        /// <summary>Reads a comma, semicolon or space separated ID list, ignoring anything that isn't an ID.</summary>
        public static HashSet<ulong> Parse(string list)
        {
            var ids = new HashSet<ulong>();
            if (string.IsNullOrEmpty(list))
                return ids;
            foreach (var part in list.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (ulong.TryParse(part.Trim(), out var id))
                    ids.Add(id);
            return ids;
        }

        /// <summary>
        /// The list as the host publishes it to the Steam lobby. Sorted because the host only rewrites the
        /// lobby data when it differs from what's there, and a set's enumeration order is not something to
        /// compare strings from: unsorted, the same list could look changed and be rewritten every check.
        /// </summary>
        public static string Format(IEnumerable<ulong> ids)
        {
            return string.Join(",", ids.OrderBy(id => id).Select(id => id.ToString()));
        }
    }
}
