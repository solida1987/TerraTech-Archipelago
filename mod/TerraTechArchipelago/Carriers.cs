using System;
using System.Collections;
using System.Collections.Generic;

namespace TerraTechArchipelago
{
    // Carriers — the three location families that come from PLAYING rather
    // than from holding a block: Shop, Enemy and Crate.
    //
    // Until this file existed the apworld created all three (330 locations at
    // the default settings) and nothing in the game could ever check one. That
    // is not "fewer checks": Archipelago fills unreachable locations like any
    // other, so a seed could put progression behind them and then could not be
    // finished. CarrierPools was written for exactly this and never called —
    // Take, ShouldPlace and Return had no callers anywhere in the mod.
    //
    // Each family is claimed by something the game already does, and each
    // draws from an exhaustible pool, so the design's rule still holds: no
    // player ever hunts a carrier that has nothing left to give.
    //
    //   Shop   the FIRST purchase of a given block type. Its grade is the
    //          block's own (ManLicenses.GetBlockTier), so the pool a purchase
    //          draws from rises as the player's shopping does. Buying the same
    //          block twice is not a second check — the family cannot be farmed.
    //   Enemy  a hostile tech destroyed. Its grade is the highest grade among
    //          the blocks it was built from, so a scout and a boss are not
    //          worth the same. The wreck drops a crate: what the enemy was
    //          carrying, made real.
    //   Crate  a crate opened, ours or the game's own. Its grade is the
    //          player's current licence grade with that crate's corporation.
    //
    // ⚠ Why not an Archipelago block bolted onto the enemy, as the design
    // says? It is buildable — BlockManager.AddBlockToTech and IsPositionValid
    // are both in the assembly — but it needs a free attach point found on a
    // tech the mod did not build, at spawn time, and none of that can be
    // proven without playing. These shapes ride events the mod ALREADY
    // receives, so one session's log shows whether they fire. The visible
    // carrier block is a layer on top of this, not a replacement: the location
    // names, the pools and the grades are the same either way.
    internal static class Carriers
    {
        /// Block grade by block id. GetBlockTier is a reflective call and a
        /// destroyed tech asks it once per block, so the answer is kept.
        private static readonly Dictionary<string, int> TierCache =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// Crates already counted, by Visible.ID. A crate can arrive locked
        /// and be unlocked, or arrive open and only play its animation; both
        /// doors are hooked, so one crate must not pay out twice.
        private static readonly HashSet<int> CountedCrates = new HashSet<int>();

        private static int _unknownGrades;

        public static void Reset()
        {
            TierCache.Clear();
            CountedCrates.Clear();
            _unknownGrades = 0;
        }

        // --- grades -------------------------------------------------------

        /// The licence grade of a block, 1..5, or 0 when the game will not say.
        ///
        /// Read from ManLicenses.GetBlockTier — the game's own table. The
        /// apworld's copy is derived from block NAMES, which is a guess; this
        /// is the measurement, and it is what a check is graded by.
        public static int GradeOf(string blockId)
        {
            if (blockId == null) return 0;
            int cached;
            if (TierCache.TryGetValue(blockId, out cached)) return cached;

            int grade = 0;
            try
            {
                object licences = Discovery.FindManager(Reflect.ManLicenses);
                if (licences != null && Reflect.GetBlockTier != null
                    && Reflect.BlockTypesEnum != null)
                {
                    object bt = Enum.Parse(Reflect.BlockTypesEnum, blockId);
                    object v = Reflect.GetBlockTier.Invoke(licences, new object[] { bt, true });
                    if (v is int t && t >= 1 && t <= 5) grade = t;
                }
            }
            catch { /* an unknown block is grade 0, and Claim handles that */ }

            // ⚠ Only a real answer is cached. A 0 read before ManLicenses
            // exists would otherwise be remembered as this block's grade for
            // the rest of the session — the same trap as caching a RAM map's
            // "no" measured before the game had loaded.
            if (grade != 0) TierCache[blockId] = grade;
            return grade;
        }

        /// The grade of a whole tech: the highest grade it was built from.
        private static int GradeOfTech(object tank)
        {
            int best = 0;
            try
            {
                if (Reflect.TankBlockman == null || Reflect.AllBlocks == null) return 0;
                object blockman = Reflect.TankBlockman.GetValue(tank, null);
                if (blockman == null) return 0;
                var blocks = Reflect.AllBlocks.GetValue(blockman) as IEnumerable;
                if (blocks == null) return 0;
                foreach (object b in blocks)
                {
                    int g = GradeOf(Reflect.BlockIdOf(b));
                    if (g > best) best = g;
                }
            }
            catch { /* a tech being torn down mid-read is not an error */ }
            return best;
        }

        // --- the three doors ----------------------------------------------

        /// A block type was bought. Only the first purchase of a type pays.
        public static void OnPurchase(object blockTypeValue)
        {
            SlotState state = Plugin.Instance?.State;
            if (state == null) return;

            string id = Reflect.BlockTypeName(blockTypeValue);
            if (id == null) return;
            if (!state.SeenPurchases.Add(id)) return;   // bought before; not a check

            Claim("Shop", GradeOf(id), "bought " + (BlockNames.NameFor(id) ?? id));
        }

        /// A tech was destroyed. Hostile ones only.
        public static void OnTechDestroyed(object tank)
        {
            if (tank == null) return;
            // ⚠ The player's own wreck is not an enemy. Without this a player
            // could sit still, blow up their own tech, and farm this family
            // (and the "Destroy N enemies" milestones) from a standstill.
            if (IsPlayerTech(tank)) return;

            int grade = GradeOfTech(tank);
            if (!Claim("Enemy", grade, "salvaged from a grade " + grade + " tech")) return;

            // What the enemy was carrying. This crate is the design's
            // "Archipelago block on the enemy" seen from the player's side —
            // the reward comes off the wreck.
            Rewards.DropCrateAt(tank, 2);
        }

        /// Was this the player's own tech? Unknown counts as "yes": paying out
        /// for a wreck we cannot identify is the failure that cannot be undone.
        public static bool IsPlayerTech(object tank)
        {
            try
            {
                if (Reflect.TankIsPlayer == null) return true;
                return Reflect.TankIsPlayer.GetValue(tank, null) is bool b && b;
            }
            catch { return true; }
        }

        /// A crate was opened — one of ours, or one the game itself placed.
        public static void OnCrateOpened(object crate)
        {
            if (crate == null) return;

            // One crate, one check, whichever door it came through.
            int id = VisibleIdOf(crate);
            if (id != 0 && !CountedCrates.Add(id)) return;

            Claim("Crate", GradeOfCrate(crate), "opened a crate");
        }

        private static int GradeOfCrate(object crate)
        {
            try
            {
                object licences = Discovery.FindManager(Reflect.ManLicenses);
                if (licences == null || Reflect.GetCurrentLevel == null
                    || Reflect.CrateCorpType == null) return 0;
                object corp = Reflect.CrateCorpType.GetValue(crate, null);
                object lvl = Reflect.GetCurrentLevel.Invoke(licences, new[] { corp });
                if (lvl is int n && n >= 1 && n <= 5) return n;
            }
            catch { }
            return 0;
        }

        private static int VisibleIdOf(object crate)
        {
            try
            {
                if (Reflect.CrateVisible == null || Reflect.VisibleId == null) return 0;
                object vis = Reflect.CrateVisible.GetValue(crate, null);
                if (vis == null) return 0;
                object v = Reflect.VisibleId.GetValue(vis, null);
                return v is int n ? n : 0;
            }
            catch { return 0; }
        }

        // --- claiming -------------------------------------------------------

        /// Take the next location from a family's pool and send it.
        ///
        /// The grade decides which pool. A grade the game will not name falls
        /// back to the lowest pool that still has room — dropping the event
        /// instead would strand those locations forever, which is the very
        /// failure this file exists to end.
        private static bool Claim(string family, int grade, string flavour)
        {
            Plugin p = Plugin.Instance;
            if (p == null || p.Pools == null) return false;

            if (grade < 1 || grade > 5)
            {
                if (_unknownGrades++ == 0)
                    Plugin.Log("A " + family.ToLowerInvariant() + " check had no grade the "
                             + "game would name, so it draws from the lowest pool with room "
                             + "left. Please report this — it means block grades are not "
                             + "being read, and the grade spread is not what the seed meant.");
                grade = LowestNonEmpty(family);
                if (grade == 0) return false;
            }

            if (!p.Pools.ShouldPlace(family, grade))
            {
                // Not a failure: an exhausted grade is the design working. Fall
                // to one that is still open so play keeps paying out.
                grade = LowestNonEmpty(family);
                if (grade == 0) return false;
            }

            string location = p.Pools.Take(family, grade);
            if (location == null) return false;

            p.SendCheck(location);
            Feed.Say(location + " — " + flavour);
            return true;
        }

        private static int LowestNonEmpty(string family)
        {
            Plugin p = Plugin.Instance;
            if (p == null || p.Pools == null) return 0;
            for (int g = 1; g <= 5; g++)
                if (p.Pools.ShouldPlace(family, g)) return g;
            return 0;
        }

        /// What is still out there, for the log. Said once per connect: a
        /// player who never sees a carrier check needs to know whether the
        /// pools are empty or the hooks are dead, and from the outside those
        /// two look exactly the same.
        public static void ReportPools()
        {
            Plugin p = Plugin.Instance;
            if (p == null || p.Pools == null) return;
            var sb = new System.Text.StringBuilder("Carriers still to find: ");
            foreach (string family in new[] { "Shop", "Enemy", "Crate" })
            {
                int n = 0;
                for (int g = 1; g <= 5; g++) n += p.Pools.RemainingIn(family, g);
                sb.Append(family).Append('=').Append(n).Append(' ');
            }
            Plugin.Log(sb.ToString().TrimEnd());
        }
    }
}
