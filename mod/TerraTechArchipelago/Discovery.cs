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

        /// The ManLicenses instance rule 0 was applied to. A new campaign
        /// brings a new one, and availability has to be opened again for it.
        ///
        /// ⚠ Reset() existed from the first build and nothing ever called it,
        /// so _done was a one-way latch: open a second campaign in the same
        /// session and every shop stayed on vanilla availability for the rest
        /// of it. An early high-grade item would then be unbuyable, which
        /// looks exactly like bad luck.
        private static object _openedFor;

        public static bool HasRun => _done;

        // Says the "waiting" line once, not every frame.
        private static bool _deferredOnce;

        /// Reveal every corporation and grade. Called once, after the licence
        /// manager exists and before the player can reach a shop.
        public static void OpenEverything()
        {
            object licences = FindManager(Reflect.ManLicenses);
            // Same campaign, already opened: the cheap early exit that lets
            // the frame pump keep asking without cost.
            if (_done && ReferenceEquals(licences, _openedFor)) return;
            if (licences == null)
            {
                // Expected, and not a failure: the handshake lands at the main
                // menu, and ManLicenses only exists once a campaign is loaded.
                // The frame pump keeps asking (see Plugin.Update) — this used
                // to return here for good, which meant rule 0 never ran and
                // every shop stayed on vanilla availability for the whole seed.
                if (!_deferredOnce)
                {
                    _deferredOnce = true;
                    Plugin.Log("Licence manager not up yet — opening vanilla "
                             + "availability as soon as a campaign loads.");
                }
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

            if (_done) Plugin.Log("A new campaign — opening vanilla availability again.");
            _done = true;
            _openedFor = licences;
            Plugin.Log("Vanilla block availability opened (" + opened +
                       " corporation/grade calls). Archipelago is now the only lock.");

            // The licence grades, read from the running game and logged in a
            // greppable shape. This line is the ONLY approved source for
            // growing Data.GRADE_LOCATION_RANGE in the apworld: a "reaches
            // Grade N" location above a corporation's real cap can never be
            // checked, and whatever the fill puts there is lost.
            //
            // ⚠ It used to print MaxSupportedTier alone, and that number is
            // not an answer. Marco's 24/8 log has it returning 2147483647 for
            // all nine corporations — int.MaxValue, the game saying "nothing
            // caps this here". A whole plan rested on grepping that line.
            //
            // So every source is printed, side by side, and the one that gives
            // a number in 1..8 wins. Printing them all is the point: if the
            // preferred source ever goes the way of MaxSupportedTier, the log
            // says so instead of quietly handing over a plausible wrong cap.
            try
            {
                IEnumerable corps2 = Reflect.GetAllCorpIDs?.Invoke(licences, null) as IEnumerable;
                if (corps2 != null)
                {
                    var sb = new System.Text.StringBuilder("LICENCE CAPS: ");
                    foreach (object corp in corps2)
                        sb.Append(CapReport(licences, corp)).Append(' ');
                    Plugin.Log(sb.ToString().TrimEnd());
                    Plugin.Log("   (grades=the corporation's real number of grades; "
                             + "supported=is it in this campaign; the rest are the "
                             + "sources that disagree)");
                }
            }
            catch (Exception e) { Plugin.Log("Could not read licence caps: " + e.Message); }
        }

        /// One corporation's grade cap, with every source that claims to know.
        ///
        /// Shaped for grepping: "GSO grades=5 supported=yes editor=5 tier=?".
        private static string CapReport(object licences, object corp)
        {
            var sb = new System.Text.StringBuilder(CorpLabel(corp));

            object licence = null;
            try { licence = Reflect.GetLicense?.Invoke(licences, new[] { corp }); }
            catch { }

            sb.Append(" grades=").Append(Num(Read(Reflect.LicenceNumGrades, licence)));
            sb.Append(" supported=").Append(
                Read(Reflect.LicenceIsSupported, licence) is bool b ? (b ? "yes" : "no") : "?");
            sb.Append(" editor=").Append(Num(Call(licences, Reflect.GetMaxSupportedGrade, corp)));
            sb.Append(" tier=").Append(Num(Call(licences, Reflect.MaxSupportedTier, corp)));
            return sb.ToString();
        }

        /// A corporation's NAME. ⚠ GetAllCorpIDs yields plain ints, so the old
        /// line printed "0=... 1=..." and a reader had to know the enum by
        /// heart to use it.
        private static string CorpLabel(object corp)
        {
            try
            {
                if (Reflect.FactionEnum != null && corp != null)
                    return Enum.ToObject(Reflect.FactionEnum, Convert.ToInt32(corp)).ToString();
            }
            catch { }
            return corp?.ToString() ?? "?";
        }

        /// A number, or "?" when the source did not answer. int.MaxValue is
        /// not an answer — it is what MaxSupportedTier says when nothing caps
        /// the corporation, and reading it as a grade is how a seed ends up
        /// with locations at grade 2147483647.
        private static string Num(object v)
        {
            if (!(v is int n)) return "?";
            return n >= 1 && n <= 8 ? n.ToString() : "?(" + n + ")";
        }

        private static object Read(System.Reflection.PropertyInfo p, object target)
        {
            if (p == null || target == null) return null;
            try { return p.GetValue(target, null); }
            catch { return null; }
        }

        private static object Call(object target, System.Reflection.MethodInfo m, object arg)
        {
            if (m == null || target == null) return null;
            try { return m.Invoke(target, new[] { arg }); }
            catch { return null; }
        }

        public static void Reset()
        {
            _done = false;
            _openedFor = null;
        }

        /// Unity singletons in TerraTech are reached through a static
        /// `inst` field on the manager type. Looked up rather than assumed,
        /// so a build that renames it reports instead of crashing.
        public static object FindManager(Type managerType)
        {
            if (managerType == null) return null;
            try
            {
                // ⚠ Every manager in TerraTech is Singleton.Manager<T>, and the
                // `inst` field lives on THAT generic base — not on ManPlayer or
                // ManLicenses themselves. GetField does not return a base
                // class's STATIC members without FlattenHierarchy, so asking
                // the manager type directly returned null for every single one.
                //
                // That one wrong lookup is why the starting vehicle was never
                // read, why vanilla availability never opened, and why granted
                // money went nowhere — three symptoms, one cause, and all of
                // them silent.
                Type baseMgr = Reflect.Game?.GetType("Singleton+Manager`1", false);
                if (baseMgr != null)
                {
                    Type closed = baseMgr.MakeGenericType(managerType);
                    FieldInfo gf = closed.GetField("inst",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    object v = gf?.GetValue(null);
                    if (v != null) return v;
                }

                FieldInfo f = managerType.GetField("inst",
                    BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (f != null) return f.GetValue(null);

                PropertyInfo p = managerType.GetProperty("inst",
                    BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                return p?.GetValue(null, null);
            }
            catch { return null; }
        }
    }
}
