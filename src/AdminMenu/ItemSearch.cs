using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminMenu
{
    /// <summary>One spawnable item, kept free of game types so the search can be tested without the game.</summary>
    internal sealed class ItemEntry
    {
        public string Id;
        public string Name;
        public string Rarity;
        public bool Stackable;
        public int MaxStack = 1;
        /// <summary>Something digging drops or scrap refines into, shown again on the Resources tab.</summary>
        public bool IsResource;
    }

    /// <summary>
    /// Filters the item list by display name or raw ID. IDs match too, so an ID copied out of a log or
    /// another mod can be pasted straight into the box.
    /// </summary>
    internal static class ItemSearch
    {
        public static List<ItemEntry> Filter(IEnumerable<ItemEntry> items, string search)
        {
            var all = items ?? Enumerable.Empty<ItemEntry>();
            var term = (search ?? string.Empty).Trim();
            if (term.Length == 0)
                return all.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

            return all
                .Select(item => new { item, rank = Rank(item, term) })
                .Where(x => x.rank < int.MaxValue)
                .OrderBy(x => x.rank)
                .ThenBy(x => x.item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.item)
                .ToList();
        }

        /// <summary>Lower is better: exact name, then name prefix, then anything containing the term.</summary>
        private static int Rank(ItemEntry item, string term)
        {
            if (EqualsTerm(item.Name, term))
                return 0;
            if (StartsWith(item.Name, term))
                return 1;
            if (Contains(item.Name, term))
                return 2;
            if (Contains(item.Id, term))
                return 3;
            return int.MaxValue;
        }

        private static bool EqualsTerm(string text, string term) =>
            text != null && string.Equals(text, term, StringComparison.OrdinalIgnoreCase);

        private static bool StartsWith(string text, string term) =>
            text != null && text.StartsWith(term, StringComparison.OrdinalIgnoreCase);

        private static bool Contains(string text, string term) =>
            text != null && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
