using System;
using System.Collections;
using System.Collections.Generic;

namespace TerraTechArchipelago
{
    // The blocks the player begins with.
    //
    // Every block in the game is locked except these. Getting this wrong is
    // not a small bug: with an empty starter set the player cannot re-attach
    // a wheel knocked off their own first tech, and the run is over before it
    // starts.
    //
    // Two rules, and the second one matters more than it looks:
    //
    //   1. Capture once, at the beginning of the campaign, from the live tech
    //      rather than a hand-written list that would drift from whatever the
    //      game actually hands out.
    //   2. REMEMBER it. Re-reading the player's tech on a later session would
    //      mark everything they had built by then as free, and quietly undo
    //      the seed. So the set lives in the slot file, and the capture only
    //      ever runs when that file has nothing in it.
    internal static class StarterTech
    {
        public static bool Captured { get; private set; }

        public static void Reset() => Captured = false;

        /// Load a set captured on an earlier session, if there is one.
        /// Returns true when nothing more needs doing.
        public static bool LoadFrom(SlotState state)
        {
            if (state == null || state.StarterBlocks.Count == 0) return false;
            BlockGate.MarkStarter(state.StarterBlocks);
            Captured = true;
            return true;
        }

        /// Try to read the player's current tech. Returns false while there is
        /// no player tech yet — the frame pump simply asks again.
        public static bool TryCapture(SlotState state)
        {
            if (Captured) return true;
            if (state == null) return false;
            if (Reflect.LastPlayerTank == null || Reflect.TankBlockman == null
                || Reflect.AllBlocks == null)
            {
                // The mod could not find the way in. Locking every block with
                // no free starting set would be worse than not locking at all,
                // so say so and leave the starter set empty-but-final rather
                // than retrying forever.
                Plugin.Log("Cannot read the starting vehicle on this build of "
                         + "TerraTech, so no blocks are treated as free. Report "
                         + "this — a seed is much harder without them.");
                Captured = true;
                return true;
            }

            object player = Discovery.FindManager(Reflect.ManPlayer);
            if (player == null) return false;

            object tank;
            try { tank = Reflect.LastPlayerTank.GetValue(player); }
            catch { return false; }
            if (tank == null) return false;

            var ids = new List<string>();
            try
            {
                object blockman = Reflect.TankBlockman.GetValue(tank, null);
                if (blockman == null) return false;
                var blocks = Reflect.AllBlocks.GetValue(blockman) as IEnumerable;
                if (blocks == null) return false;
                foreach (object b in blocks)
                {
                    string id = Reflect.BlockIdOf(b);
                    if (id != null) ids.Add(id);
                }
            }
            catch (Exception e)
            {
                Plugin.Log("Could not read the starting vehicle: " + e.Message);
                return false;
            }

            // An empty tech is not a captured tech — the player has not spawned
            // yet. Asking again next frame costs nothing.
            if (ids.Count == 0) return false;

            foreach (string id in ids) state.StarterBlocks.Add(id);
            BlockGate.MarkStarter(state.StarterBlocks);
            state.Save();
            Captured = true;

            // NOW sweep. The handshake's sweep runs at the main menu, where
            // there are no blocks to colour — it reported "swept 0" every
            // time. This is the first moment the world is really populated.
            int seen = BlockVisuals.SweepAll();
            Plugin.Log("Swept " + seen + " blocks now the world is loaded; "
                     + BlockVisuals.LastLocked + " are locked and shown as damaged.");
            return true;
        }
    }
}
