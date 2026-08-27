using System;
using System.Collections.Generic;

namespace TerraTechArchipelago
{
    // BlockVisuals — making the lock legible.
    //
    // A block that simply refuses to attach reads as a bug. TerraTech already
    // has a vocabulary for "this block is not right yet": damaged blocks glow
    // red, repaired ones flash green. Borrowing it means the player has
    // nothing new to learn — the game is speaking to them in a language they
    // already know from their first hour.
    //
    // Both halves are per-instance, through the game's own
    // TankBlock.StartMaterialPulse: MaterialColour.Damage while a block is
    // locked, MaterialColour.Healing when its licence lands.
    //
    // ⚠ An earlier attempt tinted blocks by patching
    // ManTechMaterialSwap.GetDamageColourFloat instead. Measured against the
    // real assembly, that method is handed the colour ENUM and not a block, so
    // it could never know which block it was colouring — and asking it for an
    // argument it does not have took every other patch down with it.
    internal static class BlockVisuals
    {
        /// Apply the current lock state to one block instance.
        public static void ApplyLockState(object tankBlock)
        {
            if (tankBlock == null) return;
            string id = Reflect.BlockIdOf(tankBlock);
            if (id == null) return;
            // Colour only; the refusal lives in Patch_AllowAttach.
            //
            // Two halves, and both matter. Registering the swapper is what
            // makes the red SURVIVE — the game recomputes the damage look
            // whenever it wants (OnUpdate, OnSpawn, OnRecycle, even our own
            // green pulse), and Patch_DamageLook re-asserts it from inside
            // those very calls. The direct set just makes it show right now
            // instead of at the next recompute.
            bool locked = !BlockGate.IsUnlocked(id);
            LockedLook.Mark(Reflect.ConfigOf(Reflect.SwapperOf(tankBlock)), locked);
            // Repaint now rather than at the next refresh the game happens to
            // run; with the getter owned, the refresh can only agree.
            Reflect.SetLockedLook(tankBlock, locked);
        }

        /// Re-apply to every block currently in the world.
        ///
        /// Called when a licence arrives. Walking every block sounds heavy,
        /// but it happens only on an item — a handful of times an hour — and
        /// the alternative is keeping a live index of every block instance,
        /// which is a second source of truth waiting to disagree with the
        /// first.
        /// Sweep, and flash green on the blocks that just became usable.
        ///
        /// `justUnlocked` is the block id whose licence arrived. Only those
        /// instances pulse: flashing the whole tech green would say "all of
        /// this changed", which is not what happened.
        public static int SweepAll(string justUnlocked)
        {
            return Sweep(justUnlocked);
        }

        public static int SweepAll() => Sweep(null);

        /// How the last sweep went, so "the blocks are not red" can be
        /// answered with a number instead of a guess.
        public static int LastSeen { get; private set; }
        public static int LastLocked { get; private set; }

        private static int Sweep(string justUnlocked)
        {
            int touched = 0;
            try
            {
                Type tankBlockType = Reflect.TankBlock;
                if (tankBlockType == null) return 0;

                // UnityEngine.Object.FindObjectsOfType, reached reflectively so
                // this file needs no Unity reference of its own.
                Type unityObject = Type.GetType("UnityEngine.Object, UnityEngine.CoreModule")
                                   ?? Type.GetType("UnityEngine.Object, UnityEngine");
                if (unityObject == null) return 0;

                var find = unityObject.GetMethod("FindObjectsOfType",
                    new[] { typeof(Type) });
                if (find == null) return 0;

                if (find.Invoke(null, new object[] { tankBlockType }) is Array all)
                {
                    int locked = 0;
                    foreach (object block in all)
                    {
                        string bid = Reflect.BlockIdOf(block);
                        if (bid != null && !BlockGate.IsUnlocked(bid)) locked++;
                        ApplyLockState(block);
                        // No green pulse. It was tried: the pulse machinery
                        // has its own timers, and a pulse that missed its
                        // ending left blocks green for good. The lock's whole
                        // vocabulary is now one colour — red means locked,
                        // its absence means yours — and the feed announces
                        // the unlock in words.
                        touched++;
                    }
                    LastSeen = touched;
                    LastLocked = locked;
                }
            }
            catch (Exception e)
            {
                Plugin.Log("Could not sweep block states: " + e.Message);
            }
            return touched;
        }

        /// Blocks currently attached to the player's tech, by id.
        ///
        /// Used once, at first spawn, to learn what the starting vehicle is
        /// made of. Reading it from the live tech is the whole point: a
        /// hand-written starter list would drift the first time the developers
        /// change the campaign's opening vehicle, and the player would find
        /// their own starting blocks locked.
        public static List<string> BlocksOnTech(object tank)
        {
            var ids = new List<string>();
            if (tank == null) return ids;
            try
            {
                var blocksProp = tank.GetType().GetProperty("blockman")
                                 ?? tank.GetType().GetProperty("BlockMan");
                object blockman = blocksProp?.GetValue(tank, null);
                if (blockman == null) return ids;

                var iter = blockman.GetType().GetMethod("IterateBlocks",
                    Type.EmptyTypes);
                if (iter?.Invoke(blockman, null) is System.Collections.IEnumerable blocks)
                {
                    foreach (object b in blocks)
                    {
                        string id = Reflect.BlockIdOf(b);
                        if (id != null && !ids.Contains(id)) ids.Add(id);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log("Could not read the starting vehicle: " + e.Message);
            }
            return ids;
        }
    }
}
