using System.Runtime.CompilerServices;

namespace TerraTechArchipelago
{
    // Which material CONFIGS belong to LOCKED blocks, kept where a per-frame
    // hook can afford to look. Keyed on BlockMatConfigProperties — the object
    // whose IsDamaged answer every material refresh consults.
    //
    // The game clears the damage look whenever it feels like it — measured:
    // MaterialSwapper.OnUpdate, OnSpawn and OnRecycle all reset it, and so
    // does the green pulse we play on unlock. So the look cannot be SET once;
    // it has to be RE-ASSERTED every time the game recomputes it, from inside
    // that very call.
    //
    // ⚠ That hook runs constantly, and the first attempt at it did four
    // reflective lookups per call and made the game stutter. This table is
    // the fix: registration happens on the slow path (spawn, sweep, unlock),
    // and the hook itself is one hash lookup. ConditionalWeakTable keys
    // weakly, so a recycled block's swapper just falls out — no leak, no
    // bookkeeping.
    internal static class LockedLook
    {
        private static readonly ConditionalWeakTable<object, object> Locked =
            new ConditionalWeakTable<object, object>();

        private static readonly object Tag = new object();

        public static void Mark(object swapper, bool locked)
        {
            if (swapper == null) return;
            Locked.Remove(swapper);
            if (locked) Locked.Add(swapper, Tag);
        }

        /// One dictionary probe. This is everything the per-frame hook does.
        public static bool IsLocked(object swapper)
        {
            object _;
            return swapper != null && Locked.TryGetValue(swapper, out _);
        }
    }
}
