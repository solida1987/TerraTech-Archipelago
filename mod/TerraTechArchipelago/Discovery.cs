using System;
using System.Collections;
using System.Reflection;

namespace TerraTechArchipelago
{
    // Discovery — rule 0 made real.
    //
    // TerraTech normally reveals blocks as you climb a corporation's licence
    // grades, and shops only stock what you have revealed. That is exactly the
    // wrong shape for a multiworld: if the first item you receive is a grade 4
    // rocket, and grade 4 stock is invisible until hours from now, the item is
    // worthless the moment it arrives.
    //
    // So the mod opens the whole catalogue at seed start. The player still
    // earns every licence grade, still runs the campaign, still faces enemies
    // that ramp the same way — measured, not assumed: ManPop weights spawns on
    // biome (m_CorpPerBiome) and the player's own tech history
    // (GetHistoryScore, OnPlayerTechChanged), never on block discovery.
    //
    // What changes is only who holds the key. Archipelago does.
    internal static class Discovery
    {
        private static bool _done;

        public static bool HasRun => _done;

        /// Reveal every corporation and grade. Called once, after the licence
        /// manager exists and before the player can reach a shop.
        public static void OpenEverything()
        {
            if (_done) return;

            object licences = FindManager(Reflect.ManLicenses);
            if (licences == null)
            {
                Plugin.Log("Licence manager not available yet; discovery deferred.");
                return;
            }

            int opened = 0;
            try
            {
                IEnumerable corps = Reflect.GetAllCorpIDs?.Invoke(licences, null) as IEnumerable;
                if (corps == null)
                {
                    Plugin.Log("Could not list corporations; blocks stay on vanilla availability.");
                    return;
                }

                foreach (object corp in corps)
                {
                    // Grades run 1..5. Asking for a grade a corporation does
                    // not have is harmless — the game clamps — and trying is
                    // cheaper than hard-coding which corporation stops where.
                    for (int tier = 1; tier <= 5; tier++)
                    {
                        try
                        {
                            Reflect.DiscoverEntireTier?.Invoke(licences, new object[] { corp, tier });
                            opened++;
                        }
                        catch (TargetParameterCountException)
                        {
                            // Signature differs in this build; try the single
                            // argument form before giving up on the corporation.
                            try
                            {
                                Reflect.DiscoverEntireTier?.Invoke(licences, new object[] { corp });
                                opened++;
                                break;
                            }
                            catch { }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log("Discovery failed: " + e.Message);
                return;
            }

            _done = true;
            Plugin.Log("Vanilla block availability opened (" + opened +
                       " corporation/grade calls). Archipelago is now the only lock.");
        }

        public static void Reset() => _done = false;

        /// Unity singletons in TerraTech are reached through a static
        /// `inst` field on the manager type. Looked up rather than assumed,
        /// so a build that renames it reports instead of crashing.
        public static object FindManager(Type managerType)
        {
            if (managerType == null) return null;
            try
            {
                FieldInfo f = managerType.GetField("inst",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (f != null) return f.GetValue(null);

                PropertyInfo p = managerType.GetProperty("inst",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                return p?.GetValue(null, null);
            }
            catch { return null; }
        }
    }
}
