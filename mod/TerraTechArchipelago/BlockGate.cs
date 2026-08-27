using System;
using System.Collections.Generic;

namespace TerraTechArchipelago
{
    // BlockGate — the one place that answers "may this block be attached?".
    //
    // Rule 0 of the design: the game's own availability is opened up
    // completely (see Discovery.cs), so a block from any corporation can be
    // bought the moment the multiworld sends its licence. This class is then
    // the ONLY lock left.
    //
    // The lock is the game's own: TankBlock.LockBlockAttach() on each
    // instance, the same mechanism the tutorial uses, network-synced by
    // NetTech.OnServerSetLockBlockAttach. Every block is locked as it comes
    // into the world (BlockVisuals.ApplyLockState from the spawn patch) and
    // unlocked when its licence arrives.
    //
    // A second layer was tried and removed. The plan was a Harmony postfix on
    // ManTechBuilder.CanBlockAttach as a belt to the braces — but measured in
    // the real assembly that method takes no arguments, so there is no block
    // to ask about. The patch threw at PatchAll and took every other patch
    // down with it, leaving a mod that loaded, reported itself healthy, and
    // locked nothing. tools/sigcheck now checks every patch against the game
    // so that class of mistake cannot ship again.
    //
    // ⚠ A locked block that slips through is not a cosmetic bug: it hands the
    // player progression the seed did not grant, and the logic behind the
    // seed stops being true.
    internal static class BlockGate
    {
        /// Block ids (the game's BlockTypes names) the player may attach.
        private static readonly HashSet<string> Unlocked =
            new HashSet<string>(StringComparer.Ordinal);

        /// Blocks that were on the starting vehicle. Always free — read from
        /// the live tech at first spawn, never from a hand-written list that
        /// could drift away from what the game actually gives.
        private static readonly HashSet<string> Starter =
            new HashSet<string>(StringComparer.Ordinal);

        /// Set once the seed is known. Before that the gate stays open: a
        /// player poking at the main menu must not find their blocks locked
        /// by a session that has not started.
        public static bool Armed { get; private set; }

        public static void Arm() => Armed = true;

        public static void Disarm()
        {
            Armed = false;
            Unlocked.Clear();
            Starter.Clear();
        }

        public static void LoadFrom(SlotState state)
        {
            Unlocked.Clear();
            foreach (string id in state.UnlockedBlocks) Unlocked.Add(id);
        }

        public static void MarkStarter(IEnumerable<string> blockIds)
        {
            foreach (string id in blockIds) Starter.Add(id);
            Plugin.Log("Starting vehicle carries " + Starter.Count + " block types; those stay free.");
        }

        public static bool IsStarter(string blockId) => Starter.Contains(blockId);

        public static bool IsUnlocked(string blockId)
        {
            if (!Armed) return true;
            if (blockId == null) return true;
            return Starter.Contains(blockId) || Unlocked.Contains(blockId);
        }

        /// Grant a block. Returns false when it was already granted, so the
        /// caller can skip the unlock effects on a replayed item.
        public static bool Grant(string blockId)
        {
            if (blockId == null) return false;
            return Unlocked.Add(blockId);
        }

        public static int UnlockedCount => Unlocked.Count;

        public static IEnumerable<string> UnlockedIds => Unlocked;
    }
}
