using System;
using System.Collections.Generic;
using System.Linq;

namespace TerraTechArchipelago
{
    // CarrierPools — the rule that no Archipelago carrier outlives its purpose.
    //
    // Shops, enemies and crates carry checks. Once a grade's checks are all
    // taken, its carriers must stop appearing: an Archipelago block on an
    // enemy that gives nothing is worse than no block at all, because the
    // player will keep hunting it.
    //
    // The pools are rebuilt from slot data on every connect rather than kept
    // as a running tally. A tally can drift after a crash; a rebuild from the
    // authoritative list of sent locations cannot.
    internal sealed class CarrierPools
    {
        public sealed class Pool
        {
            public readonly string Family;                 // "Shop", "Enemy", "Crate"
            public readonly int Tier;
            public readonly List<string> Remaining = new List<string>();

            public Pool(string family, int tier) { Family = family; Tier = tier; }
            public bool Empty => Remaining.Count == 0;
        }

        private readonly Dictionary<string, Pool> _pools =
            new Dictionary<string, Pool>(StringComparer.Ordinal);

        private static string Key(string family, int tier) => family + "|" + tier;

        /// Build the pools from the seed's counts, minus what has been sent.
        public void Rebuild(int shopChecks, int enemyChecks, int crateChecks,
                            HashSet<string> sentLocations)
        {
            _pools.Clear();
            Add("Shop", shopChecks, sentLocations);
            Add("Enemy", enemyChecks, sentLocations);
            Add("Crate", crateChecks, sentLocations);

            int total = _pools.Values.Sum(p => p.Remaining.Count);
            Plugin.Log("Carrier pools rebuilt: " + total + " placements still to find.");
        }

        // The same weights the apworld uses when it names the locations, so
        // the two agree without either importing the other. ⚠ If one changes,
        // the other must: a mismatch shows up as locations the mod never
        // places, which is a seed that cannot be completed.
        private static readonly Dictionary<int, double> Weights = new Dictionary<int, double>
        {
            { 1, 0.30 }, { 2, 0.25 }, { 3, 0.20 }, { 4, 0.15 }, { 5, 0.10 },
        };

        private void Add(string family, int total, HashSet<string> sent)
        {
            if (total <= 0) return;

            var counts = new Dictionary<int, int>();
            int assigned = 0;
            for (int tier = 1; tier <= 5; tier++)
            {
                int n = (int)(total * Weights[tier]);
                counts[tier] = n;
                assigned += n;
            }
            counts[1] += total - assigned;   // remainder to grade 1, as the world does

            for (int tier = 1; tier <= 5; tier++)
            {
                var pool = new Pool(family, tier);
                for (int i = 1; i <= counts[tier]; i++)
                {
                    string name = family + " G" + tier + " #" + i;
                    if (!sent.Contains(name)) pool.Remaining.Add(name);
                }
                _pools[Key(family, tier)] = pool;
            }
        }

        /// Should a carrier of this family and grade be placed right now?
        public bool ShouldPlace(string family, int tier)
        {
            Pool pool;
            return _pools.TryGetValue(Key(family, tier), out pool) && !pool.Empty;
        }

        /// Take the next location name for a carrier, or null when the pool is
        /// dry. Taking removes it, so two carriers can never claim the same
        /// location even if both spawn in the same frame.
        public string Take(string family, int tier)
        {
            Pool pool;
            if (!_pools.TryGetValue(Key(family, tier), out pool) || pool.Empty)
                return null;
            string name = pool.Remaining[0];
            pool.Remaining.RemoveAt(0);
            return name;
        }

        /// Put one back — the carrier spawned but the player never took it, so
        /// the location must not be lost with the enemy that despawned.
        public void Return(string family, int tier, string locationName)
        {
            Pool pool;
            if (locationName == null) return;
            if (_pools.TryGetValue(Key(family, tier), out pool)
                && !pool.Remaining.Contains(locationName))
                pool.Remaining.Insert(0, locationName);
        }

        public int RemainingIn(string family, int tier)
        {
            Pool pool;
            return _pools.TryGetValue(Key(family, tier), out pool) ? pool.Remaining.Count : 0;
        }

        public int TotalRemaining => _pools.Values.Sum(p => p.Remaining.Count);
    }
}
