using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;

namespace TerraTechArchipelago
{
    // Plugin — the mod's spine.
    //
    // Owns the bridge, the slot file and the pools, and is the only thing that
    // touches the game from the game's own thread. Everything the socket says
    // is queued and drained in Update; nothing from a worker thread ever
    // reaches a TankBlock.
    public sealed class Plugin
    {
        public const string ModVersion = "0.3.0";
        public const string HarmonyId = "dk.solida.terratech.archipelago";

        // The build this mod was written against. A mismatch is reported, not
        // ignored: patching a game whose methods have moved is how a mod
        // "loads fine" and then silently stops locking anything.
        public const string TestedGameVersion = "1.4.x";
        /// The version of TerraTech we are actually running inside. Reported
        /// in the handshake so a bug report says which build met which mod —
        /// the first thing anybody needs and the easiest thing to forget.
        ///
        /// ⚠ This used to be a field nothing ever assigned, so every handshake
        /// claimed "unknown". A value that is never written is worse than no
        /// value: it looks like an answer.
        public static string GameVersion { get; private set; } = "unknown";

        private static void ReadGameVersion()
        {
            try
            {
                Type app = Type.GetType("UnityEngine.Application, UnityEngine.CoreModule")
                           ?? Type.GetType("UnityEngine.Application, UnityEngine");
                object v = app?.GetProperty("version",
                    System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Static)?.GetValue(null, null);
                if (v is string s && s.Length > 0) GameVersion = s;
            }
            catch { /* left as "unknown", which is then the truth */ }
        }

        public static Plugin Instance { get; private set; }
        private static readonly List<string> Startup = new List<string>();

        private Harmony _harmony;
        private Bridge _bridge;
        private SlotState _state;
        private CarrierPools _pools;
        private bool _healthy;
        private double _lastSave;
        private double _lastDiscoveryTry;
        private int _discoveryTries;
        private double _lastStarterTry;
        private double _lastResweep;
        private int _starterTries;

        // What this seed asked for. Read from slot_data at handshake, because
        // sending a check the seed has no location for is not harmless: the
        // launcher reports every one as an unknown location, and a seed with
        // pickup checks off would fill the log with thousands of them.
        private bool _pickupChecks = true;
        private bool _attachChecks;
        private string _goal = "licence_master";
        private int _corpsToMax = 3;
        private int _collectorPct = 50;
        private bool _goalSent;
        /// ⚠ The SEED's switch, not the launcher's. London has its own
        /// DeathLink toggle and gates the AP tag with it; this is what the
        /// player asked for when they made the yaml, and a seed that said no
        /// must not start broadcasting deaths because a global toggle is on.
        private bool _deathLink;
        private int _poolSize;   // how many blocks this seed shuffles

        // corp -> top grade this seed tracks, from slot_data's "max_grades"
        // ("GSO=5;GeoCorp=3"). Only corporations in here count toward the
        // licence_master goal — the seed only made locations for these.
        private readonly System.Collections.Generic.Dictionary<string, int> _maxGrades =
            new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);

        public static void Log(string message)
        {
            string line = "[Archipelago] " + message;
            Startup.Add(line);
            try { Console.WriteLine(line); } catch { }
            try
            {
                Type debug = Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule")
                             ?? Type.GetType("UnityEngine.Debug, UnityEngine");
                debug?.GetMethod("Log", new[] { typeof(object) })
                     ?.Invoke(null, new object[] { line });
            }
            catch { }
        }

        // --- lifecycle --------------------------------------------------

        /// Entry point. Called by the mod loader.
        public static void Init()
        {
            if (Instance != null) return;
            Instance = new Plugin();
            Instance.Start();
        }

        private void Start()
        {
            ReadGameVersion();
            Log("TerraTech Archipelago " + ModVersion + " starting, in "
                + "TerraTech " + GameVersion + ".");

            if (!Reflect.Resolve())
            {
                Log("REFUSING TO PATCH. This build of TerraTech does not have "
                    + "everything the mod needs:");
                foreach (string m in Reflect.MissingTargets) Log("   missing: " + m);
                Log("The mod was written against TerraTech " + TestedGameVersion
                    + ". Nothing has been changed — the game runs as normal.");
                return;
            }

            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(typeof(Plugin).Assembly);
            }
            catch (Exception e)
            {
                Log("Patching failed, the mod is inactive: " + e);
                return;
            }

            _bridge = new Bridge();
            _bridge.Start();   // the socket lives on its own thread from here
            _pools = new CarrierPools();
            _healthy = true;
            // Name what was actually patched. "Patches applied" on its own is
            // a claim; this is the evidence, and it is the first thing to look
            // at when a check that should have fired did not.
            foreach (var patched in _harmony.GetPatchedMethods())
                Log("   patched " + patched.DeclaringType?.Name + "." + patched.Name);

            Log("Patches applied. Waiting for the Archipelago client on port 24601.");
        }

        /// Called every frame by the loader's runner object.
        public void Update(double now)
        {
            if (!_healthy) return;

            _bridge.Tick();

            // Rule 0 — every vanilla block available to buy — can only be
            // applied once the campaign's licence manager exists, which is
            // long after the handshake. Retry until it takes; the call is a
            // no-op once done.
            // ⚠ Asked on every tick, not only until it has run once. Loading a
            // second campaign needs rule 0 applied again, and the old
            // condition made that impossible — Discovery now decides for
            // itself, by remembering which licence manager it opened.
            if (_state != null && now - _lastDiscoveryTry > 1.0)
            {
                _lastDiscoveryTry = now;
                Discovery.OpenEverything();
                if (!Discovery.HasRun) _discoveryTries++;
                if (!Discovery.HasRun && _discoveryTries == 30)
                    Log("Still cannot reach the licence manager after 30 tries. "
                      + "Shops will only stock what the game itself has unlocked "
                      + "— please report this.");
            }

            // The starting vehicle, captured the first time it exists. Same
            // retry shape as discovery, and for the same reason: the handshake
            // lands long before the player has a tech.
            if (_state != null && !StarterTech.Captured && now - _lastStarterTry > 1.0)
            {
                _lastStarterTry = now;
                StarterTech.TryCapture(_state);
                _starterTries++;
                // ⚠ Say so if it never lands. The first build retried forever
                // in silence, and a player whose starting blocks stayed locked
                // had nothing in the log to report. Thirty tries is half a
                // minute of being in a campaign — long past reasonable.
                if (!StarterTech.Captured && _starterTries == 30)
                    Log("Still cannot read the starting vehicle after 30 tries. "
                      + "Your starting blocks will stay locked until a licence "
                      + "arrives for them — please report this.");
            }

            // A slow standing sweep. Spawn hooks paint most blocks at
            // birth, but a block whose swapper was not ready at that moment
            // appeared colourless until the next item happened to land. Five
            // seconds is the ceiling on how long a wrong colour can live.
            if (_state != null && now - _lastResweep > 5.0)
            {
                _lastResweep = now;
                BlockVisuals.SweepAll();
            }

            string line;
            while (_bridge.Inbox.TryDequeue(out line))
                HandleLine(line);

            // Flush the slot file at most twice a second. Writing on every
            // check would hammer the disk during a busy fight; losing half a
            // second of bookkeeping costs nothing, because the server is the
            // authority and re-sends are free.
            if (_state != null && now - _lastSave > 0.5)
            {
                _lastSave = now;
                _state.Save();
            }
        }

        // --- messages from the client -----------------------------------

        private void HandleLine(string line)
        {
            try
            {
                string cmd = Json.Str(line, "cmd");
                switch (cmd)
                {
                    case "Handshake": OnHandshake(line); break;
                    case "Item": OnItem(line); break;
                    case "DeathLink": OnDeathLink(line); break;
                }
            }
            catch (Exception e)
            {
                Log("Could not handle a message: " + e.Message);
            }
        }

        private void OnHandshake(string line)
        {
            string slot = Json.Str(line, "slot") ?? "0";
            string seed = Json.Str(line, "seed") ?? "unknown";
            string slotKey = seed + "_" + slot;

            // A second handshake for the SAME slot is normal: the launcher
            // repeats it once the seed's slot_data arrives, which can be after
            // the mod dialled in. Re-reading the slot file then would throw
            // away anything collected in between that had not been written
            // yet — so keep the state we have and only refresh the settings.
            bool sameSlot = _state != null && _state.SlotKey == slotKey;
            if (!sameSlot)
            {
                _state = SlotState.Load(slotKey, seed);
                BlockGate.LoadFrom(_state);
                StarterTech.Reset();
                StarterTech.LoadFrom(_state);
                // A different seed is a different run: rule 0 has to be
                // applied to it, and the crates counted for the old one mean
                // nothing here.
                Discovery.Reset();
                // Only on a REAL slot change. A repeat handshake for the same
                // slot arrives mid-session when slot_data lands late, and
                // clearing the crates counted so far would let one crate in
                // the world claim a second location.
                Carriers.Reset();
            }
            BlockGate.Arm();

            // Toggles arrive as JSON true/false; Int() reads numbers, so ask
            // the text directly. Defaults match the world's own defaults, so a
            // handshake from an older client still behaves sensibly.
            _pickupChecks = !line.Contains("\"pickup_checks\":false");
            _attachChecks = line.Contains("\"attach_checks\":true");
            _deathLink = line.Contains("\"death_link\":true");
            _goal = Json.Str(line, "goal") ?? "licence_master";
            _corpsToMax = Json.Int(line, "corporations_to_max", 3);
            _collectorPct = Json.Int(line, "collector_percentage", 50);
            _goalSent = false;
            _starterTries = 0;
            _discoveryTries = 0;
            // The seed's block table is a JSON object of name -> id; its size
            // is the pool the collector goal is measured against.
            _poolSize = SlotData.CountBlockTable(line);

            _maxGrades.Clear();
            string caps = Json.Str(line, "max_grades") ?? "";
            foreach (string pair in caps.Split(';'))
            {
                int eq = pair.LastIndexOf('=');
                int cap;
                if (eq > 0 && int.TryParse(pair.Substring(eq + 1), out cap))
                    _maxGrades[pair.Substring(0, eq)] = cap;
            }

            _pools.Rebuild(
                Json.Int(line, "shop_checks", 0),
                Json.Int(line, "enemy_checks", 0),
                Json.Int(line, "crate_checks", 0),
                _state.SentLocations);
            Carriers.ReportPools();

            // Rule 0: open vanilla availability so an early high-grade item
            // can actually be obtained.
            Discovery.OpenEverything();

            BlockVisuals.SweepAll();
            Log("Swept " + BlockVisuals.LastSeen + " blocks in the world; "
                + BlockVisuals.LastLocked + " are locked and should look damaged. "
                + "Colour support: "
                + (Reflect.SetDamageVisuals != null ? "yes" : "MISSING"));
            Log("Connected to seed " + seed + " (slot " + slot + "). "
                + BlockGate.UnlockedCount + " block licences already held.");
        }

        private void OnItem(string line)
        {
            int index = Json.Int(line, "index", -1);
            if (index < 0) return;

            // Replay is idempotent: the client re-sends every item on connect,
            // and anything at or below the high-water mark is already applied.
            bool isNew = index > _state.HighestItemIndex;

            string name = Json.Str(line, "name") ?? "";
            string from = Json.Str(line, "from") ?? "someone";

            if (name.EndsWith(" Licence", StringComparison.Ordinal))
                GrantBlockLicence(name, from, isNew);
            else if (name.Contains(" Grade "))
                GrantGrade(name, isNew);
            else
                GrantFiller(name, from, isNew);

            if (isNew)
            {
                _state.HighestItemIndex = index;
                _state.Save();
            }
        }

        private void GrantBlockLicence(string itemName, string from, bool isNew)
        {
            string display = itemName.Substring(0, itemName.Length - " Licence".Length);
            string blockId = BlockNames.IdFor(display);
            if (blockId == null)
            {
                // The seed knows a block this build does not. Report it —
                // a licence that unlocks nothing is a dead item, and the
                // player deserves to know why their check did nothing.
                Log("WARNING: received a licence for '" + display
                    + "', which this build of TerraTech does not have.");
                return;
            }

            bool fresh = BlockGate.Grant(blockId);
            _state.UnlockedBlocks.Add(blockId);
            if (!fresh || !isNew) return;

            BlockVisuals.SweepAll(blockId);
            Feed.Say(display + " unlocked — from " + from);
            EvaluateGoal();
        }

        private void GrantGrade(string itemName, bool isNew)
        {
            _state.UnlockedBlocks.Add("grade:" + itemName);
            if (isNew) Feed.Say(itemName + " granted.");
            EvaluateGoal();
        }

        private void GrantFiller(string itemName, string from, bool isNew)
        {
            if (!isNew) return;                  // filler is not replayed
            Feed.Say(itemName + " — from " + from);
            Rewards.Deliver(itemName, from);
        }

        private void OnDeathLink(string line)
        {
            string source = Json.Str(line, "source") ?? "someone";
            if (!_deathLink)
            {
                // London's DeathLink toggle is launcher-wide; this seed said
                // no. Say so once rather than quietly taking the player's
                // tech apart in a seed that never asked for it.
                if (!_deathLinkWarned)
                {
                    _deathLinkWarned = true;
                    Log("A DeathLink arrived from " + source + ", but this seed was "
                      + "generated with death_link off, so nothing happens. Turn it "
                      + "off in the launcher too, or generate with death_link: true.");
                }
                return;
            }
            Log("DeathLink from " + source + ".");
            Rewards.ApplyDeathLink();
        }

        // --- events from the game ---------------------------------------

        /// The game's corporation codes, in the words the seed uses.
        /// Measured from FactionSubTypes; anything unknown is reported rather
        /// than guessed, because a wrong name is a check that never lands.
        private static string CorpName(object faction)
        {
            switch (faction?.ToString())
            {
                case "GSO": return "GSO";
                case "GC":  return "GeoCorp";
                case "VEN": return "Venture";
                case "HE":  return "Hawkeye";
                case "BF":  return "Better Future";
                case "SJ":  return "Space Junkers";
                case "SPE": return "Special";
                case "EXP": return "Experimental";
                default:    return null;
            }
        }

        /// A corporation reached a new grade, by the player's own play.
        public void OnLicenceGrade(object faction, int grade)
        {
            if (_state == null) return;
            if (grade < 1 || grade > 5) return;
            string corp = CorpName(faction);
            if (corp == null)
            {
                Log("Unknown corporation from the game: " + faction
                  + " — its grade checks cannot be sent. Please report this.");
                return;
            }
            SendCheck(corp + " reaches Grade " + grade);
            EvaluateGoal();
        }

        public void OnBlockPickedUp(string blockId)
        {
            if (_state == null) return;
            if (!_state.SeenBlockPickups.Add(blockId)) return;

            string display = BlockNames.NameFor(blockId);
            if (_pickupChecks && display != null) SendCheck("Pick up " + display);

            int n = _state.Bump("blocks_collected");
            CheckMilestone("Collect", n);
        }

        public void OnBlockAttached(string blockId)
        {
            if (_state == null) return;
            // Bolting a block straight from the ground is possession without
            // the inventory ever seeing it; the pickup check must not depend
            // on which of the two the player happened to do first.
            OnBlockPickedUp(blockId);
            if (!_state.SeenBlockAttaches.Add(blockId)) return;

            string display = BlockNames.NameFor(blockId);
            if (_attachChecks && display != null) SendCheck("Attach " + display);
        }

        public void OnTankDestroyed(object tank)
        {
            if (_state == null) return;
            // ⚠ The player's own wreck is not an enemy. This used to count
            // every destroyed tech, so a player could farm the "Destroy N
            // enemies" milestones by blowing up their own tech on the spot.
            if (Carriers.IsPlayerTech(tank))
            {
                // In TerraTech, losing your tech is the death there is.
                if (_deathLink && CertainlyPlayerTech(tank))
                    SendDeath("lost their tech");
                return;
            }

            int n = _state.Bump("enemies_destroyed");
            CheckMilestone("Destroy", n);
            Carriers.OnTechDestroyed(tank);
        }

        /// True only when the game POSITIVELY says this is the player's tech.
        ///
        /// ⚠ Deliberately not Carriers.IsPlayerTech, which answers "yes" when
        /// it cannot tell. Withholding a check on a wreck we cannot identify
        /// is cautious; broadcasting a death into everybody else's game on one
        /// is not. The two questions want opposite answers when unsure.
        private static bool CertainlyPlayerTech(object tank)
        {
            try
            {
                if (tank == null || Reflect.TankIsPlayer == null) return false;
                return Reflect.TankIsPlayer.GetValue(tank, null) is bool b && b;
            }
            catch { return false; }
        }

        private static readonly int[] Milestones =
            { 5, 10, 25, 50, 100, 200, 350, 500, 750, 1000 };

        private void CheckMilestone(string verb, int count)
        {
            foreach (int m in Milestones)
            {
                if (count != m) continue;
                SendCheck(verb == "Destroy"
                    ? "Destroy " + m + " enemies"
                    : "Collect " + m + " blocks");
                return;
            }
        }

        /// Send a location, once and only once.
        ///
        /// The name is the dedup key and it has exactly one form. This is the
        /// line that stops a repeatable campaign mission from paying out
        /// twice — and the reason Diablo II's redelivery bug cannot happen
        /// here.
        public void SendCheck(string locationName)
        {
            if (_state == null || locationName == null) return;
            if (!_state.SentLocations.Add(locationName)) return;

            _bridge.Send("{\"cmd\":\"Check\",\"locations\":[" + Json.Quote(locationName) + "]}");
            // Say every check out loud. A player testing this needs to see
            // that something happened, and a bug report that says "nothing
            // sent" is only useful if the log would have said otherwise.
            Feed.Say("Check sent — " + locationName
                   + "  (" + _state.SentLocations.Count + " total)");
        }

        /// Is the seed won?
        ///
        /// ⚠ This used to be missing entirely: SendGoal existed and nothing
        /// ever called it, so a seed could be played to the end and never
        /// completed. Nothing in generation or in a smoke test catches that —
        /// only playing does, or reading for callers.
        ///
        /// Checked after every grant. Cheap, and the alternative is a timer
        /// that reports the win some seconds after it happened.
        private void EvaluateGoal()
        {
            if (_goalSent || _state == null) return;

            bool won;
            switch (_goal)
            {
                case "licence_master":
                    // What the player REACHED, not what they were handed —
                    // and against each corporation's own measured cap, not a
                    // blanket 5. (⚠ history: the first version counted Grade 5
                    // ITEMS from the pool, and the very first pickup won the
                    // seed and released everything.)
                    if (_maxGrades.Count == 0) return;   // no ladder tracked
                    int maxed = 0;
                    foreach (var kv in _maxGrades)
                        if (_state.SentLocations.Contains(
                                kv.Key + " reaches Grade " + kv.Value))
                            maxed++;
                    won = maxed >= Math.Min(_corpsToMax, _maxGrades.Count);
                    break;

                case "collector":
                    // Licences held against the size of this seed's pool. The
                    // pool is what the world shuffled, which is exactly the set
                    // of licences that can ever arrive.
                    int held2 = 0;
                    foreach (string id in _state.UnlockedBlocks)
                        if (!id.StartsWith("grade:", StringComparison.Ordinal)) held2++;
                    int need = Math.Max(1, _poolSize * _collectorPct / 100);
                    won = _poolSize > 0 && held2 >= need;
                    break;

                default:
                    // ⚠ Reachable only from a seed made by an OLDER world. The
                    // ap_hunt goal was removed on 27 August because no core
                    // item and no core location ever existed, so it could be
                    // chosen and then never finished. Such a seed is still
                    // playable -- it just cannot end, and the player is told
                    // that once instead of discovering it after ten hours.
                    if (!_goalWarned)
                    {
                        _goalWarned = true;
                        Log("This seed asks for the goal \"" + _goal + "\", which this "
                          + "build does not have. It was generated with an older "
                          + "version of the world and cannot be completed. Generate "
                          + "again with goal: licence_master or collector.");
                    }
                    return;
            }

            if (!won) return;
            _goalSent = true;
            Log("Goal complete.");
            SendGoal();
        }

        private bool _goalWarned;
        private bool _deathLinkWarned;

        public void SendGoal()
        {
            _bridge.Send("{\"cmd\":\"Goal\"}");
        }

        public void SendDeath(string cause)
        {
            _bridge.Send("{\"cmd\":\"Death\",\"cause\":" + Json.Quote(cause) + "}");
        }

        internal CarrierPools Pools { get { return _pools; } }
        internal SlotState State { get { return _state; } }
    }
}
