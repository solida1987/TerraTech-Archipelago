using System;
using System.Collections.Generic;

namespace TerraTechArchipelago
{
    // Rewards — delivering what other worlds send.
    //
    // Everything arrives the same way: a crate falls out of the sky. TerraTech
    // already drops crates, players already know to drive to them, and it
    // turns "you received an item" from a line in a log into something that
    // happens in the world.
    //
    // ⚠ Our crates may only ever contain blocks the player has already
    // unlocked. Vanilla's own reward crates are untouched and keep giving
    // whatever the game intends — a vanilla-granted block is still
    // attach-locked, so nothing leaks through that door either.
    internal static class Rewards
    {
        private static readonly Dictionary<string, int> MoneyAmounts =
            new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "Block Bucks (small)", 5000 },
            { "Block Bucks (medium)", 25000 },
            { "Block Bucks (large)", 100000 },
        };

        public static void Deliver(string itemName, string from)
        {
            int amount;
            if (MoneyAmounts.TryGetValue(itemName, out amount))
            {
                GiveMoney(amount);
                Plugin.Log(itemName + " from " + from + " (+" + amount + ").");
                return;
            }

            switch (itemName)
            {
                case "Block Pack":
                    DropCrate(BlocksThePlayerMayUse(4));
                    Plugin.Log("Block Pack from " + from + " — a crate is inbound.");
                    break;

                case "Supply Crate":
                    DropCrate(BlocksThePlayerMayUse(2));
                    GiveMoney(10000);
                    Plugin.Log("Supply Crate from " + from + " — a crate is inbound.");
                    break;

                case "Scrapper Trap":
                    Plugin.Log("Scrapper Trap from " + from + ".");
                    Scrapper(3);
                    break;

                case "Bill Trap":
                    GiveMoney(-15000);
                    Plugin.Log("Bill Trap from " + from + " (-15000).");
                    break;

                // ⚠ "Pest Trap" was removed from the item pool on 27 August.
                // It was to spawn a small enemy wave and only ever logged a
                // line, so a player could receive it, see nothing happen, and
                // have no way to tell that from a trap that had simply missed.
                // ManPop.TryToSpawn could carry it -- but where it spawns
                // cannot be predicted from outside a running game, and a trap
                // that sometimes does nothing is the same lie in a new hat.

                default:
                    Plugin.Log("Received " + itemName + " from " + from + ".");
                    break;
            }
        }

        /// Pick blocks the player is actually allowed to attach.
        ///
        /// Sending a locked block would be a gift the player can look at and
        /// not use — technically harmless, and exactly the kind of small lie
        /// the rest of this project refuses to tell.
        private static List<string> BlocksThePlayerMayUse(int count)
        {
            var pool = new List<string>(BlockGate.UnlockedIds);
            var picked = new List<string>();
            if (pool.Count == 0) return picked;

            var rng = new Random();
            for (int i = 0; i < count; i++)
                picked.Add(pool[rng.Next(pool.Count)]);
            return picked;
        }

        private static void GiveMoney(int amount)
        {
            try
            {
                object player = Discovery.FindManager(Reflect.ManPlayer);
                if (player == null || Reflect.AddMoney == null) return;
                Reflect.AddMoney.Invoke(player, new object[] { amount });
            }
            catch (Exception e)
            {
                Plugin.Log("Could not grant money: " + e.Message);
            }
        }

        /// A crate of blocks, dropped where the player is.
        private static void DropCrate(List<string> blockIds)
            => Spawn(blockIds, PlayerTank());

        /// A crate off a wreck. Used by the Enemy carrier family, so what the
        /// enemy "was carrying" lands where it died rather than at the player.
        public static void DropCrateAt(object tank, int count)
            => Spawn(BlocksThePlayerMayUse(count), tank);

        /// Put a crate in the world.
        ///
        /// ⚠ The design named CrateSpawner.SpawnCrateDrop. Measured in the
        /// assembly, the only type that owns a CrateSpawner is ModePVP — so in
        /// a campaign there is no such object, and the call could never have
        /// worked. ManSpawn.RewardSpawner is the path the campaign uses for
        /// its own licence rewards, and it is public.
        ///
        /// Three levels, each a real fallback rather than a silent one:
        /// a crate at the tech, blocks in front of the camera, or a log line
        /// saying plainly that neither was available.
        private static void Spawn(List<string> blockIds, object tankForPosition)
        {
            if (blockIds == null || blockIds.Count == 0)
            {
                Plugin.Log("   (no unlocked blocks yet — nothing to put in the crate)");
                return;
            }

            object spawner = RewardSpawner();
            object list = BlockTypeList(blockIds);
            if (spawner == null || list == null)
            {
                Plugin.Log("   crate contents (could not reach the game's reward spawner): "
                         + string.Join(", ", blockIds.ToArray()));
                return;
            }

            try
            {
                object scenePos;
                if (Reflect.RewardBlocksByCrate != null
                    && TryScenePosition(tankForPosition, out scenePos))
                {
                    object corp = Enum.ToObject(Reflect.FactionEnum, 1);   // GSO
                    Reflect.RewardBlocksByCrate.Invoke(spawner, new[] { list, scenePos, corp });
                    Plugin.Log("   a crate is inbound: " + string.Join(", ", blockIds.ToArray()));
                    return;
                }

                if (Reflect.BlocksInFrontOfCamera != null)
                {
                    // No position to aim at — the player may not have a tech.
                    // The game's own "here are your blocks" path needs none.
                    Reflect.BlocksInFrontOfCamera.Invoke(spawner, new[] { list });
                    Plugin.Log("   blocks delivered in front of you: "
                             + string.Join(", ", blockIds.ToArray()));
                    return;
                }
            }
            catch (Exception e)
            {
                Plugin.Log("   could not drop the crate: " + e.Message);
            }

            Plugin.Log("   crate contents: " + string.Join(", ", blockIds.ToArray()));
        }

        private static object RewardSpawner()
        {
            try
            {
                object manSpawn = Discovery.FindManager(Reflect.Game?.GetType("ManSpawn", false));
                if (manSpawn == null || Reflect.RewardSpawnerProp == null) return null;
                return Reflect.RewardSpawnerProp.GetValue(manSpawn, null);
            }
            catch { return null; }
        }

        private static object PlayerTank()
        {
            try
            {
                object player = Discovery.FindManager(Reflect.ManPlayer);
                if (player == null || Reflect.LastPlayerTank == null) return null;
                return Reflect.LastPlayerTank.GetValue(player);
            }
            catch { return null; }
        }

        /// A tech's position, in the SCENE coordinates RewardBlocksByCrate
        /// wants. ⚠ A tech reports a GAME-WORLD position; handing that over
        /// unconverted drops the crate a tile away from the player.
        private static bool TryScenePosition(object tank, out object scenePos)
        {
            scenePos = null;
            if (tank == null || Reflect.TankBoundsCentre == null
                || Reflect.SceneFromGameWorld == null || Reflect.ScenePositionProp == null)
                return false;
            try
            {
                object gameWorld = Reflect.TankBoundsCentre.GetValue(tank, null);
                if (gameWorld == null) return false;
                object worldPos = Reflect.SceneFromGameWorld.Invoke(null, new[] { gameWorld });
                if (worldPos == null) return false;
                scenePos = Reflect.ScenePositionProp.GetValue(worldPos, null);
                return scenePos != null;
            }
            catch { return false; }
        }

        /// The block ids as the game's own List&lt;BlockTypes&gt;.
        private static object BlockTypeList(List<string> blockIds)
        {
            if (Reflect.BlockTypesEnum == null) return null;
            try
            {
                Type listType = typeof(List<>).MakeGenericType(Reflect.BlockTypesEnum);
                object list = Activator.CreateInstance(listType);
                System.Reflection.MethodInfo add = listType.GetMethod("Add");
                foreach (string id in blockIds)
                {
                    // A block the seed knows and this build does not must not
                    // take the whole crate down with it.
                    try { add.Invoke(list, new[] { Enum.Parse(Reflect.BlockTypesEnum, id) }); }
                    catch { Plugin.Log("   (skipped unknown block " + id + ")"); }
                }
                return list;
            }
            catch { return null; }
        }

        /// A DeathLink death, in TerraTech's own vocabulary: the tech comes
        /// apart where it stands.
        ///
        /// ApplyPhysicsKick scatters the blocks the way an explosion does, so
        /// the player walks back and rebuilds. Recycle would DELETE them, and
        /// that is not a death, it is a robbery — a DeathLink must cost time,
        /// never progress.
        public static void ApplyDeathLink()
        {
            object tank = PlayerTank();
            if (tank == null)
            {
                Plugin.Log("   (no tech to lose right now — the death is skipped)");
                return;
            }
            if (Reflect.TankBlockman == null || Reflect.RemoveAllBlocks == null
                || Reflect.RemoveAllKick == null)
            {
                // ⚠ Say it. A DeathLink that silently does nothing makes a
                // player think the feature works while their partner dies
                // alone.
                Plugin.Log("   DeathLink cannot be applied on this build of TerraTech "
                         + "— BlockManager.RemoveAllBlocks was not found. Please report "
                         + "this; deaths are arriving and doing nothing.");
                return;
            }
            try
            {
                object blockman = Reflect.TankBlockman.GetValue(tank, null);
                if (blockman == null) return;
                Reflect.RemoveAllBlocks.Invoke(blockman, new[] { Reflect.RemoveAllKick });
                Feed.Say("Your tech came apart — DeathLink.");
            }
            catch (Exception e)
            {
                Plugin.Log("   could not apply the death: " + e.Message);
            }
        }

        /// The Scrapper trap: a few blocks knocked off, nothing destroyed.
        ///
        /// ⚠ allowHeadlessTech is false and the root block is skipped, so the
        /// trap can never leave the player sitting in a wreck with no cab.
        /// It is meant to cost a minute, not a run.
        private static void Scrapper(int count)
        {
            object tank = PlayerTank();
            if (tank == null || Reflect.TankBlockman == null
                || Reflect.DetachAndRestructure == null || Reflect.AllBlocks == null)
            {
                Plugin.Log("   (nothing to scrap right now)");
                return;
            }
            try
            {
                object blockman = Reflect.TankBlockman.GetValue(tank, null);
                if (blockman == null) return;
                var blocks = new List<object>();
                var all = Reflect.AllBlocks.GetValue(blockman) as System.Collections.IEnumerable;
                object root = Reflect.RootBlock?.GetValue(blockman);
                if (all == null) return;
                foreach (object b in all)
                    if (b != null && !ReferenceEquals(b, root)) blocks.Add(b);

                if (blocks.Count == 0)
                {
                    Plugin.Log("   (nothing to scrap right now)");
                    return;
                }

                var rng = new Random();
                int knocked = 0;
                for (int i = 0; i < count && blocks.Count > 0; i++)
                {
                    int k = rng.Next(blocks.Count);
                    object block = blocks[k];
                    blocks.RemoveAt(k);
                    try
                    {
                        Reflect.DetachAndRestructure.Invoke(
                            blockman, new object[] { block, false, false });
                        knocked++;
                    }
                    catch { /* one block refusing is not the trap failing */ }
                }
                Feed.Say(knocked + " block(s) knocked off — Scrapper Trap.");
            }
            catch (Exception e)
            {
                Plugin.Log("   could not scrap: " + e.Message);
            }
        }
    }
}
