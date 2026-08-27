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

    /// The red that stays, attempt three — this one owns the ANSWER.
    ///
    /// Measured: every material refresh asks BlockMatConfigProperties for
    /// get_IsDamaged. Attempt two guarded SwapMaterialDamage instead, which
    /// only holds the red once the game enters its damage path — a block
    /// bought fresh from a vendor stayed grey until first scratched, and a
    /// repair's heal pulse ended green because the pulse machinery repainted
    /// without going through the guarded call. Owning the getter covers every
    /// path there is, because the refresh itself cannot ask anything else.
    ///
    /// Hot path is still one hash probe — the lag lesson stands.
    [HarmonyPatch]
    internal static class Patch_DamageLook
    {
        internal static bool Prepare() => Reflect.ConfigIsDamaged != null
                                       && Reflect.SwapperConfig != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.ConfigIsDamaged;

        internal static void Postfix(object __instance, ref bool __result)
        {
            if (__result) return;                // damaged for real; nothing to add
            if (LockedLook.IsLocked(__instance)) __result = true;
        }
    }

    // (The first attempt, for the record: a prefix that did its own
    // reflective lookups per call,
    // and the call comes from the material system rather than from anything
    // rare. Measuring first would have shown it was not even needed —
    // MaterialSwapper.OnUpdate returns immediately for a block whose material
    // type is Normal, so the game does not overwrite the damage look we set.
    //
    // ⚠ Reflection inside a hook the ENGINE calls is a different animal from
    // reflection at start-up. Cheap once, ruinous sixty times a second.

    /// THE lock. Every way a player can put a block on a tech ends here.
    ///
    /// ⚠ This replaces TankBlock.LockBlockAttach, which the first build used
    /// on the strength of its name. Reading the game's IL shows it is
    /// SetLockTimer(BlockLockTimer, 1f) — a ONE SECOND cooldown, not a lock.
    /// Blocks were "locked" for a second and then free, so everything on the
    /// ground and in the shops could be bolted straight on. A method's name is
    /// not its behaviour.
    [HarmonyPatch]
    internal static class Patch_AllowAttach
    {
        internal static bool Prepare() => Reflect.AllowPlayerAttachBlock != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.AllowPlayerAttachBlock;

        internal static void Postfix(object __0, ref bool __result)
        {
            if (!__result) return;              // already refused; leave it
            if (!BlockGate.Armed) return;
            string id = Reflect.BlockIdOf(__0);
            if (id == null) return;
            if (!BlockGate.IsUnlocked(id)) __result = false;
        }
    }

    // The timer-based lock is gone; see Patch_AllowAttach above. BlockGate calls the
    // game's own TankBlock.LockBlockAttach/UnlockBlockAttach — the mechanism
    // the tutorial uses, network-synced by the game. A postfix on
    // ManTechBuilder.CanBlockAttach was tried and removed: measured in the
    // real assembly, that method takes no arguments, so there is no block to
    // ask about — and the broken patch took the whole mod down with it.

    /// A corporation levelled up — the player earned it by playing.
    ///
    /// ⚠ This is what makes "reaches Grade N" a real location. Before it,
    /// those forty locations existed in every seed and nothing could ever
    /// check them, so anything placed behind one was unreachable — and the
    /// licence_master goal had nothing real behind it either.
    [HarmonyPatch]
    internal static class Patch_LicenceLevelUp
    {
        internal static bool Prepare() => Reflect.LicenceLevelUp != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.LicenceLevelUp;

        internal static void Postfix(object __0, int __1)
        {
            if (!BlockGate.Armed) return;
            Plugin.Instance?.OnLicenceGrade(__0, __1);
        }
    }

    /// A swapper being (re)initialised by the game's pool. This is the exact
    /// moment the damage look gets cleared for a reused block, so it is also
    /// the moment to re-register it. Runs only when a block enters the world,
    /// so the reflective walk back to the block is affordable here.
    [HarmonyPatch]
    internal static class Patch_SwapperSpawn
    {
        internal static bool Prepare() => Reflect.SwapperOnSpawn != null
                                       && Reflect.SwapperDamageable != null
                                       && Reflect.DamageableBlock != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.SwapperOnSpawn;

        /// A PREFIX on purpose: the game's own OnSpawn body runs its first
        /// material refresh, and the config must already be registered when
        /// that refresh asks its question — a postfix registered one refresh
        /// too late, which is exactly the vendor-fresh grey block.
        internal static void Prefix(object __instance)
        {
            if (!BlockGate.Armed) return;
            object block = Reflect.BlockOfSwapper(__instance);
            string id = Reflect.BlockIdOf(block);
            if (id == null) return;
            LockedLook.Mark(Reflect.ConfigOf(__instance), !BlockGate.IsUnlocked(id));
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

    /// Possession: the check fires when a block type enters the player's
    /// inventory, whatever door it came through.
    ///
    /// ⚠ This used to hook ManPlayer.AddBlockToInventory, which is only the
    /// loose-pickup route — a cab packed straight into the inventory never
    /// counted, and a player could fill their storage without a single check.
    /// Measured in the game's IL: every route ends in HostAddItem, so that is
    /// where the meaning lives. Picking up is always allowed, by design.
    [HarmonyPatch]
    internal static class Patch_InventoryAdd
    {
        internal static bool Prepare() => Reflect.InventoryHostAdd != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.InventoryHostAdd;

        internal static void Postfix(object __0)
        {
            if (!BlockGate.Armed) return;
            string id = Reflect.BlockTypeName(__0);
            if (id != null) Plugin.Instance?.OnBlockPickedUp(id);
        }
    }

    /// The same doorway on the multiplayer inventory.
    [HarmonyPatch]
    internal static class Patch_InventoryAddNet
    {
        internal static bool Prepare() => Reflect.InventoryHostAddNet != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.InventoryHostAddNet;

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

    /// A block bought from a vendor. The Shop carrier family's only door.
    ///
    /// ⚠ DoPurchaseBlock, not RequestPurchaseBlock. The request is the client
    /// asking; the game can still refuse it for want of money, and a check
    /// must never pay out for a purchase that did not happen.
    [HarmonyPatch]
    internal static class Patch_Purchase
    {
        internal static bool Prepare() => Reflect.PurchaseBlock != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.PurchaseBlock;

        // __1 is the BlockTypes argument: (uint shopBlockPoolID, BlockTypes
        // blockType, int count), verified against the game by tools/sigcheck.
        internal static void Postfix(object __1)
        {
            if (!BlockGate.Armed) return;
            Carriers.OnPurchase(__1);
        }
    }

    /// A locked crate being unlocked by the player.
    [HarmonyPatch]
    internal static class Patch_CrateUnlock
    {
        internal static bool Prepare() => Reflect.CrateUnlock != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.CrateUnlock;

        internal static void Postfix(object __instance)
        {
            if (!BlockGate.Armed) return;
            Carriers.OnCrateOpened(__instance);
        }
    }

    /// A crate opening. Hooked as well as Unlock because a crate that arrives
    /// already unlocked never goes through Unlock at all — it just plays this.
    /// Carriers dedups on the crate's Visible.ID, so a crate that goes through
    /// both doors still pays out exactly once.
    [HarmonyPatch]
    internal static class Patch_CrateOpen
    {
        internal static bool Prepare() => Reflect.CrateOpenAnim != null;

        internal static System.Reflection.MethodBase TargetMethod()
            => Reflect.CrateOpenAnim;

        internal static void Postfix(object __instance)
        {
            if (!BlockGate.Armed) return;
            Carriers.OnCrateOpened(__instance);
        }
    }

    // The damage-red tint on locked blocks is BlockVisuals' job, applied per
    // block at spawn. A postfix on ManTechMaterialSwap.GetDamageColourFloat
    // was tried and removed: its parameter is the MaterialColour ENUM, not a
    // block, so the hook could never know which block it was colouring.
}
