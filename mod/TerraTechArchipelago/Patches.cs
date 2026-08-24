using System;
using System.Collections.Generic;
using HarmonyLib;

namespace TerraTechArchipelago
{
    // Patches — where the mod touches the game.
    //
    // Every patch here is deliberately small and reversible. The rule we hold
    // ourselves to: a patch either answers a question (may this attach?) or
    // reports an event (a block was picked up). None of them change what the
    // game does otherwise, because the design keeps vanilla progression whole.
    //
    // ⚠ TerraTech has no official modding API for these methods. A game update
    // can move them. Plugin.VerifyPatchTargets checks every target exists at
    // start-up and refuses loudly rather than running half-patched — a silent
    // half-patched state is how a player ends up with a seed that cannot be
    // completed and no idea why.

    /// The chokepoint. Every attach the builder performs asks here first.
    [HarmonyPatch]
    internal static class Patch_CanBlockAttach
    {
        internal static bool Prepare() => Reflect.TechBuilderCanAttach != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.TechBuilderCanAttach;

        internal static void Postfix(object __0, ref bool __result)
        {
            if (!__result) return;              // already refused; leave it
            if (!BlockGate.Armed) return;

            string id = Reflect.BlockIdOf(__0);
            if (id == null) return;             // not a block we can identify
            if (!BlockGate.IsUnlocked(id))
                __result = false;
        }
    }

    /// The per-instance lock, applied as blocks come into the world. This is
    /// what makes the block visibly refuse and go red, rather than silently
    /// failing at the last moment.
    [HarmonyPatch]
    internal static class Patch_BlockSpawn
    {
        internal static bool Prepare() => Reflect.BlockOnSpawn != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.BlockOnSpawn;

        internal static void Postfix(object __instance)
        {
            if (!BlockGate.Armed) return;
            BlockVisuals.ApplyLockState(__instance);
        }
    }

    /// Pickup: the check fires here. Note it fires whether or not the block is
    /// locked — picking up is always allowed, by design.
    [HarmonyPatch]
    internal static class Patch_AddBlockToInventory
    {
        internal static bool Prepare() => Reflect.AddBlockToInventory != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.AddBlockToInventory;

        internal static void Postfix(object __0)
        {
            if (!BlockGate.Armed) return;
            string id = Reflect.BlockTypeName(__0);
            if (id != null) Plugin.Instance?.OnBlockPickedUp(id);
        }
    }

    /// Attach: the second check family, and the moment a formerly locked
    /// block finally goes on.
    [HarmonyPatch]
    internal static class Patch_BlockAttached
    {
        internal static bool Prepare() => Reflect.BlockOnAttach != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.BlockOnAttach;

        internal static void Postfix(object __instance)
        {
            if (!BlockGate.Armed) return;
            string id = Reflect.BlockIdOf(__instance);
            if (id != null) Plugin.Instance?.OnBlockAttached(id);
        }
    }

    /// Enemy destroyed: kill milestones, and Archipelago carrier blocks that
    /// were riding on the tech.
    [HarmonyPatch]
    internal static class Patch_TankDestroyed
    {
        internal static bool Prepare() => Reflect.TankDestroyedEvent != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.TankDestroyedEvent;

        internal static void Postfix(object __0)
        {
            if (!BlockGate.Armed) return;
            Plugin.Instance?.OnTankDestroyed(__0);
        }
    }

    /// The damage colour. Locked blocks are pushed to full damage red so the
    /// state is visible at a glance, using the signal the player already
    /// knows rather than a new one they have to learn.
    [HarmonyPatch]
    internal static class Patch_DamageColour
    {
        internal static bool Prepare() => Reflect.DamageColourFloat != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.DamageColourFloat;

        internal static void Postfix(object __0, ref float __result)
        {
            if (!BlockGate.Armed) return;
            string id = Reflect.BlockIdOf(__0);
            if (id == null) return;
            if (!BlockGate.IsUnlocked(id)) __result = 1f;   // fully "damaged" = red
        }
    }
}
