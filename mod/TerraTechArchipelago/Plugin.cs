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
        public const string ModVersion = "0.1.0";
        public const string HarmonyId = "dk.solida.terratech.archipelago";

        // The build this mod was written against. A mismatch is reported, not
        // ignored: patching a game whose methods have moved is how a mod
        // "loads fine" and then silently stops locking anything.
        public const string TestedGameVersion = "1.4.x";
        public static string GameVersion { get; private set; } = "unknown";

        public static Plugin Instance { get; private set; }
        private static readonly List<string> Startup = new List<string>();

        private Harmony _harmony;
        private Bridge _bridge;
        private SlotState _state;
        private CarrierPools _pools;
        private bool _healthy;
        private double _lastSave;

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
            Log("TerraTech Archipelago " + ModVersion + " starting.");

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
            _pools = new CarrierPools();
            _healthy = true;
            Log("Patches applied. Waiting for the Archipelago client on port 24601.");
        }

        /// Called every frame by the loader's runner object.
        public void Update(double now)
        {
            if (!_healthy) return;

            _bridge.Tick();

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

            _state = SlotState.Load(slotKey, seed);
            BlockGate.LoadFrom(_state);
            BlockGate.Arm();

            _pools.Rebuild(
                Json.Int(line, "shop_checks", 0),
                Json.Int(line, "enemy_checks", 0),
                Json.Int(line, "crate_checks", 0),
                _state.SentLocations);

            // Rule 0: open vanilla availability so an early high-grade item
            // can actually be obtained.
            Discovery.OpenEverything();

            BlockVisuals.SweepAll();
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

            BlockVisuals.SweepAll();
            Log(display + " unlocked (from " + from + ").");
        }

        private void GrantGrade(string itemName, bool isNew)
        {
            _state.UnlockedBlocks.Add("grade:" + itemName);
            if (isNew) Log(itemName + " granted.");
        }

        private void GrantFiller(string itemName, string from, bool isNew)
        {
            if (!isNew) return;                  // filler is not replayed
            Rewards.Deliver(itemName, from);
        }

        private void OnDeathLink(string line)
        {
            string source = Json.Str(line, "source") ?? "someone";
            Log("DeathLink from " + source + ".");
            Rewards.ApplyDeathLink();
        }

        // --- events from the game ---------------------------------------

        public void OnBlockPickedUp(string blockId)
        {
            if (_state == null) return;
            if (!_state.SeenBlockPickups.Add(blockId)) return;

            string display = BlockNames.NameFor(blockId);
            if (display != null) SendCheck("Pick up " + display);

            int n = _state.Bump("blocks_collected");
            CheckMilestone("Collect", n);
        }

        public void OnBlockAttached(string blockId)
        {
            if (_state == null) return;
            if (!_state.SeenBlockAttaches.Add(blockId)) return;

            string display = BlockNames.NameFor(blockId);
            if (display != null) SendCheck("Attach " + display);
        }

        public void OnTankDestroyed(object tank)
        {
            if (_state == null) return;
            int n = _state.Bump("enemies_destroyed");
            CheckMilestone("Destroy", n);
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
        }

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
