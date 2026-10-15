using System;
using System.Collections.Generic;

namespace AdminMenu
{
    /// <summary>
    /// How many items a quick offer may send to a forge whose host doesn't run this mod. Such a host credits
    /// an offered item only when the item's fly-in animation finishes, and it animates from a pool of 20
    /// visuals per resource; an item that finds the pool empty is never animated and so never credited, though
    /// it has already left the inventory. This keeps the items we have in flight under the pool's size: each
    /// send counts against <c>capacity</c> until <c>window</c> seconds later, by which time its animation has
    /// finished and handed the visual back. Kept free of game types so it can be tested without the game.
    /// </summary>
    internal sealed class OfferBudget
    {
        private readonly int _capacity;
        private readonly float _window;
        private readonly Queue<KeyValuePair<float, int>> _sent = new Queue<KeyValuePair<float, int>>();
        private int _inFlight;

        public OfferBudget(int capacity, float window)
        {
            _capacity = capacity;
            _window = window;
        }

        /// <summary>Claims up to <paramref name="wanted"/> items at <paramref name="now"/> and returns how many were granted.</summary>
        public int Take(float now, int wanted)
        {
            while (_sent.Count > 0 && now - _sent.Peek().Key >= _window)
                _inFlight -= _sent.Dequeue().Value;

            var granted = Math.Min(wanted, _capacity - _inFlight);
            if (granted <= 0)
                return 0;
            _sent.Enqueue(new KeyValuePair<float, int>(now, granted));
            _inFlight += granted;
            return granted;
        }
    }

    internal static class ModVersion
    {
        /// <summary>Whether a dotted version string is at least <paramref name="minimum"/>; unparsable counts as older.</summary>
        public static bool AtLeast(string version, string minimum)
        {
            return Version.TryParse(version, out var parsed) && Version.TryParse(minimum, out var floor) && parsed >= floor;
        }
    }
}
