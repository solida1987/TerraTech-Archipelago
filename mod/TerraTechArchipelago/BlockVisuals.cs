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
    // The red comes from Patch_DamageColour forcing the damage value to 1 for
    // locked blocks. This class handles the per-instance side: applying the
    // attach lock as blocks appear, and sweeping every block in the world when
    // an item lands.
    internal static class BlockVisuals
    {
        /// Apply the current lock state to one block instance.
        public static void ApplyLockState(object tankBlock)
        {
            if (tankBlock == null) return;
            string id = Reflect.BlockIdOf(tankBlock);
            if (id == null) return;
            Reflect.CallLock(tankBlock, !BlockGate.IsUnlocked(id));
        }

        /// Re-apply to every block currently in the world.
        ///
        /// Called when a licence arrives. Walking every block sounds heavy,
        /// but it happens only on an item — a handful of times an hour — and
        /// the alternative is keeping a live index of every block instance,
        /// which is a second source of truth waiting to disagree with the
        /// first.
        public static int SweepAll()
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
                    foreach (object block in all)
                    {
                        ApplyLockState(block);
                        touched++;
                    }
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
