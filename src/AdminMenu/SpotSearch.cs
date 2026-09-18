using System;
using System.Collections.Generic;

namespace AdminMenu
{
    /// <summary>
    /// A spot to put a player, as a distance forward and to the right of whoever the move is anchored on.
    /// Kept free of game types so the search order can be tested without the game.
    /// </summary>
    internal readonly struct SpotOffset : IEquatable<SpotOffset>
    {
        public readonly float Forward;
        public readonly float Right;

        public SpotOffset(float forward, float right)
        {
            Forward = forward;
            Right = right;
        }

        public float Distance => (float)Math.Sqrt(Forward * Forward + Right * Right);

        public bool Equals(SpotOffset other) => Forward.Equals(other.Forward) && Right.Equals(other.Right);
        public override bool Equals(object obj) => obj is SpotOffset other && Equals(other);
        public override int GetHashCode() => Forward.GetHashCode() * 397 ^ Right.GetHashCode();
        public override string ToString() => $"(forward {Forward:0.##}, right {Right:0.##})";
    }

    /// <summary>
    /// Where to put a teleported player. Dropping them at a fixed offset from the anchor put them inside the
    /// level whenever that offset was into a wall, and a <c>CharacterController</c> switched on inside solid
    /// rock is pushed out in whatever direction the overlap resolves — often downward, through the map.
    ///
    /// So instead of trusting one offset, the caller walks these candidates in order and takes the first one a
    /// player actually fits in. The order sweeps out from the anchor's facing direction and stays within the
    /// preferred distance, so a blocked spot falls back to one beside it rather than somewhere across the level.
    /// </summary>
    internal static class SpotSearch
    {
        /// <summary>The distance in front of the anchor a teleport aims for when nothing is in the way.</summary>
        public const float PreferredDistance = 1.5f;

        /// <summary>
        /// Candidates nearest the anchor's facing direction first, ending with the anchor's own position — the
        /// one spot known to fit a player, because somebody is standing in it.
        /// </summary>
        /// <param name="closerRings">Extra rings tried at fractions of the distance, for spots too tight for a full step.</param>
        public static List<SpotOffset> Candidates(float preferredDistance, int closerRings, int perRing)
        {
            var candidates = new List<SpotOffset>();
            var rings = Math.Max(1, closerRings + 1);
            for (var ring = 0; ring < rings; ring++)
            {
                // Rings work inward: a spot a full step away is roomier to stand in than one hugging the anchor.
                var distance = preferredDistance * (rings - ring) / rings;
                foreach (var degrees in Sweep(perRing))
                {
                    var radians = degrees * Math.PI / 180.0;
                    candidates.Add(new SpotOffset(
                        (float)(Math.Cos(radians) * distance),
                        (float)(Math.Sin(radians) * distance)));
                }
            }
            candidates.Add(new SpotOffset(0f, 0f));
            return candidates;
        }

        /// <summary>
        /// Angles from the facing direction, alternating right then left so both sides of the anchor are tried
        /// before turning any further away from it.
        /// </summary>
        private static IEnumerable<double> Sweep(int perRing)
        {
            var step = 360.0 / Math.Max(1, perRing);
            yield return 0.0;
            for (var angle = step; angle < 180.0; angle += step)
            {
                yield return angle;
                yield return -angle;
            }
            if (perRing > 1)
                yield return 180.0;
        }

        /// <summary>
        /// The first candidate <paramref name="fits"/> accepts by index, or the anchor's own position when none
        /// do. Never returns a spot the caller rejected: being stood on top of beats being dropped out of the world.
        /// </summary>
        public static SpotOffset Pick(IReadOnlyList<SpotOffset> candidates, Func<int, bool> fits)
        {
            for (var i = 0; i < candidates.Count; i++)
                if (fits(i))
                    return candidates[i];
            return new SpotOffset(0f, 0f);
        }
    }
}
