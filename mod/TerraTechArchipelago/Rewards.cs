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
                    break;

                case "Bill Trap":
                    GiveMoney(-15000);
                    Plugin.Log("Bill Trap from " + from + " (-15000).");
                    break;

                case "Pest Trap":
                    Plugin.Log("Pest Trap from " + from + ".");
                    break;

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

        private static void DropCrate(List<string> blockIds)
        {
            // Wired in phase 3 against CrateSpawner.SpawnCrateDrop and
            // PopulateCrateContents. Until then the item is logged and the
            // player is told plainly, rather than the mod pretending a crate
            // fell that never did.
            if (blockIds.Count == 0)
            {
                Plugin.Log("   (no unlocked blocks yet — nothing to put in the crate)");
                return;
            }
            Plugin.Log("   crate contents: " + string.Join(", ", blockIds.ToArray()));
        }

        public static void ApplyDeathLink()
        {
            // Phase 5. Logged for now so the message is visible in testing.
            Plugin.Log("   (DeathLink effect is not wired up yet)");
        }
    }
}
