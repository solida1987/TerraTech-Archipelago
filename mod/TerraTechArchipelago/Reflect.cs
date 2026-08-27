using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TerraTechArchipelago
{
    // Reflect — every place the mod reaches into TerraTech, resolved by name
    // in one file.
    //
    // Why reflection instead of compiling against the game's assembly: this
    // repository must be buildable by anyone who clones it, and shipping
    // Assembly-CSharp.dll would be redistributing the game. Resolving by name
    // at start-up costs a few milliseconds once and keeps the source clean of
    // anything that is not ours.
    //
    // The second reason matters more. Every lookup below can return null, and
    // Plugin checks them all before patching anything. A game update that
    // renames a method therefore produces one clear message naming the method
    // — never a mod that loads, half-works, and quietly stops locking blocks.
    internal static class Reflect
    {
        public static Assembly Game { get; private set; }

        // Types
        public static Type TankBlock { get; private set; }
        public static Type ManTechBuilder { get; private set; }
        public static Type ManPlayer { get; private set; }
        public static Type ManLicenses { get; private set; }
        public static Type ManTechMaterialSwap { get; private set; }
        public static Type ManPop { get; private set; }
        public static Type Tank { get; private set; }

        // Methods we patch
        public static MethodInfo TechBuilderCanAttach { get; private set; }
        public static MethodInfo BlockOnSpawn { get; private set; }
        public static MethodInfo BlockOnAttach { get; private set; }
        public static MethodInfo AddBlockToInventory { get; private set; }
        public static MethodInfo InventoryHostAdd { get; private set; }
        public static MethodInfo InventoryHostAddNet { get; private set; }
        public static MethodInfo TankDestroyedEvent { get; private set; }
        public static MethodInfo DamageColourFloat { get; private set; }
        public static MethodInfo AllowPlayerAttachBlock { get; private set; }
        public static MethodInfo LicenceLevelUp { get; private set; }
        public static MethodInfo MaxSupportedTier { get; private set; }
        public static MethodInfo PurchaseBlock { get; private set; }
        public static MethodInfo CrateUnlock { get; private set; }
        public static MethodInfo CrateOpenAnim { get; private set; }

        // The carrier families (Carriers.cs). All optional: a build that has
        // moved one of these loses that family's checks, and the mod says so
        // — it must never stop the block lock, which is the whole seed.
        public static Type CrateType { get; private set; }
        public static Type BlockTypesEnum { get; private set; }
        public static Type FactionEnum { get; private set; }
        public static MethodInfo GetBlockTier { get; private set; }
        public static MethodInfo GetCurrentLevel { get; private set; }
        public static MethodInfo GetLicense { get; private set; }
        public static MethodInfo GetMaxSupportedGrade { get; private set; }
        public static PropertyInfo LicenceNumGrades { get; private set; }
        public static PropertyInfo LicenceIsSupported { get; private set; }

        // DeathLink and the Scrapper trap. Both act on the player's own tech
        // through its BlockManager, and both are optional: losing them costs
        // one feature and must never cost the block lock.
        public static MethodInfo RemoveAllBlocks { get; private set; }
        public static MethodInfo DetachAndRestructure { get; private set; }
        public static object RemoveAllKick { get; private set; }
        public static FieldInfo RootBlock { get; private set; }
        public static PropertyInfo CrateCorpType { get; private set; }
        public static PropertyInfo VisibleId { get; private set; }
        public static PropertyInfo CrateVisible { get; private set; }
        public static PropertyInfo TankVisible { get; private set; }
        public static PropertyInfo TankBoundsCentre { get; private set; }
        public static PropertyInfo RewardSpawnerProp { get; private set; }
        public static MethodInfo RewardBlocksByCrate { get; private set; }
        public static MethodInfo BlocksInFrontOfCamera { get; private set; }
        public static MethodInfo SceneFromGameWorld { get; private set; }
        public static PropertyInfo ScenePositionProp { get; private set; }

        // Methods we call
        public static MethodInfo LockBlockAttach { get; private set; }
        public static MethodInfo UnlockBlockAttach { get; private set; }
        public static MethodInfo DiscoverEntireTier { get; private set; }
        public static MethodInfo SetBlockState { get; private set; }
        public static MethodInfo GetAllCorpIDs { get; private set; }
        public static MethodInfo AddMoney { get; private set; }
        public static MethodInfo StartMaterialPulse { get; private set; }
        public static MethodInfo SetDamageVisuals { get; private set; }
        public static MethodInfo SwapMaterialDamage { get; private set; }
        public static FieldInfo SwapperDamageable { get; private set; }
        public static PropertyInfo DamageableBlock { get; private set; }
        public static FieldInfo BlockSwapper { get; private set; }
        public static MethodInfo SwapperOnSpawn { get; private set; }
        public static FieldInfo SwapperConfig { get; private set; }
        public static MethodInfo ConfigIsDamaged { get; private set; }

        // The two enum values the pulse takes. Boxed once at resolve time:
        // Enum.Parse on every block in a sweep would be thousands of
        // reflective parses for a colour.
        public static object PulseTypeDamage { get; private set; }
        public static object PulseTypeHealing { get; private set; }
        public static object PulseColourDamage { get; private set; }
        public static object PulseColourHealing { get; private set; }

        // Properties we read
        public static PropertyInfo BlockTypeProp { get; private set; }
        public static PropertyInfo TankIsPlayer { get; private set; }
        public static PropertyInfo TankBlockman { get; private set; }

        // Fields we read
        public static FieldInfo LastPlayerTank { get; private set; }
        public static FieldInfo AllBlocks { get; private set; }

        private static readonly List<string> Missing = new List<string>();

        public static IReadOnlyList<string> MissingTargets => Missing;

        public static bool Resolve()
        {
            Missing.Clear();
            Game = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (Game == null)
            {
                Missing.Add("Assembly-CSharp (the game's own code)");
                return false;
            }

            TankBlock = T("TankBlock");
            ManTechBuilder = T("ManTechBuilder");
            ManPlayer = T("ManPlayer");
            ManLicenses = T("ManLicenses");
            ManTechMaterialSwap = T("ManTechMaterialSwap");
            ManPop = T("ManPop");
            Tank = T("Tank");

            TechBuilderCanAttach = M(ManTechBuilder, "CanBlockAttach");
            BlockOnSpawn = M(TankBlock, "OnSpawn") ?? M(TankBlock, "OnPool");
            BlockOnAttach = M(TankBlock, "OnAttach");
            AddBlockToInventory = M(ManPlayer, "AddBlockToInventory");
            // The inventory's own doorway. Measured: every route in — loose
            // pickup, packing a block from the world, refunds — ends in
            // HostAddItem. ManPlayer.AddBlockToInventory is just ONE caller,
            // and hooking only it meant a packed-up cab never counted as
            // collected. Possession is the event, not one route to it.
            InventoryHostAdd = M(T("SingleplayerInventory"), "HostAddItem");
            Type netInv = Game.GetType("NetInventory", false);
            InventoryHostAddNet = MOpt(netInv, "HostAddItem");
            DamageColourFloat = M(ManTechMaterialSwap, "GetDamageColourFloat");

            LockBlockAttach = M(TankBlock, "LockBlockAttach");
            UnlockBlockAttach = M(TankBlock, "UnlockBlockAttach");
            DiscoverEntireTier = M(ManLicenses, "DiscoverEntireTier");
            SetBlockState = M(ManLicenses, "SetBlockState");
            GetAllCorpIDs = M(ManLicenses, "GetAllCorpIDs");

            // THE gate. Measured in the game's IL: ManTechBuilder.CanBlockAttach
            // ends by asking ManBlockLimiter.AllowPlayerAttachBlock(block), and
            // so do the drag preview, the attach particles and the network
            // release path. One answer, every route, and it is handed the block.
            AllowPlayerAttachBlock = M(T("ManBlockLimiter"), "AllowPlayerAttachBlock");

            // A corporation reaching a new grade. Without this the seed's 40
            // grade locations could never be checked by anybody, and the
            // licence_master goal had nothing real behind it.
            LicenceLevelUp = M(ManLicenses, "LevelUp");
            MaxSupportedTier = MOpt(ManLicenses, "MaxSupportedTier");
            AddMoney = M(ManPlayer, "AddMoney");

            // --- the carrier families ------------------------------------
            //
            // Every lookup here is optional on purpose. Losing one costs that
            // family's checks and is reported; losing the block lock would
            // cost the seed, so the two must never share a failure.
            BlockTypesEnum = Game.GetType("BlockTypes", false);
            FactionEnum = Game.GetType("FactionSubTypes", false);
            GetBlockTier = MOpt(ManLicenses, "GetBlockTier");
            GetCurrentLevel = MOpt(ManLicenses, "GetCurrentLevel");

            // How many grades a corporation actually HAS. The apworld needs
            // this: a "reaches Grade N" location above a corporation's real
            // cap can never be checked, and whatever fill puts there is lost.
            //
            // ⚠ MaxSupportedTier was the design's answer and it is NOT one.
            // Measured in Marco's own 24/8 log, it returned 2147483647 for all
            // nine corporations -- int.MaxValue, which is the game saying "no
            // limit here" rather than answering the question. FactionLicense
            // carries the real numbers.
            GetLicense = MOpt(ManLicenses, "GetLicense");
            GetMaxSupportedGrade = MOpt(ManLicenses, "GetMaxSupportedGradeEditorOnly");
            Type licence = Game.GetType("FactionLicense", false);
            LicenceNumGrades = POpt(licence, "NumXpLevels");
            LicenceIsSupported = POpt(licence, "IsSupported");

            // A tech coming apart. RemoveAllBlocks is what DeathLink does to
            // the player -- in TerraTech, losing your tech IS the death --
            // and DetachBlockAndRestructure is the Scrapper trap.
            Type blockMan = Game.GetType("BlockManager", false);
            RemoveAllBlocks = MOpt(blockMan, "RemoveAllBlocks");
            DetachAndRestructure = MOpt(blockMan, "DetachBlockAndRestructure");
            RootBlock = FOpt(blockMan, "rootBlock");
            Type removeAction = Game.GetType("BlockManager+RemoveAllAction", false)
                                ?? Game.GetType("RemoveAllAction", false);
            if (removeAction != null)
            {
                // ApplyPhysicsKick scatters the blocks the way an explosion
                // does, so the player can drive back and rebuild. Recycle
                // would DELETE them, which is not a death, it is a robbery.
                try { RemoveAllKick = Enum.Parse(removeAction, "ApplyPhysicsKick"); }
                catch { RemoveAllBlocks = null; }
            }
            else RemoveAllBlocks = null;

            // Shop. The SERVER-side purchase, not RequestPurchaseBlock: the
            // request is the client asking, and a request that the game then
            // refuses (no money) must not pay out a check.
            PurchaseBlock = MOpt(T("ManPurchases"), "DoPurchaseBlock");

            // Crates. Both doors, because a crate can arrive locked (the
            // player unlocks it) or already open (it just plays its
            // animation). Carriers dedups on the crate's Visible.ID, so a
            // crate that goes through both still counts once.
            CrateType = Game.GetType("Crate", false);
            CrateUnlock = MOpt(CrateType, "Unlock");
            CrateOpenAnim = MOpt(CrateType, "PlayOpeningAnimation");
            CrateCorpType = POpt(CrateType, "CorpType");
            CrateVisible = POpt(CrateType, "visible");
            VisibleId = POpt(Game.GetType("Visible", false), "ID");
            TankVisible = POpt(Tank, "visible");
            TankBoundsCentre = POpt(Tank, "boundsCentreWorld");

            // Delivering an item as a crate that falls out of the sky.
            //
            // ⚠ The design named CrateSpawner.SpawnCrateDrop. Measured in the
            // assembly, the only thing that owns a CrateSpawner is ModePVP —
            // so in a campaign that object does not exist at all. ManSpawn's
            // RewardSpawner is the path the campaign itself uses for licence
            // rewards, and it is public.
            Type manSpawn = Game.GetType("ManSpawn", false);
            RewardSpawnerProp = POpt(manSpawn, "RewardSpawner");
            Type rewardSpawner = Game.GetType("RewardSpawner", false);
            RewardBlocksByCrate = MOpt(rewardSpawner, "RewardBlocksByCrate");
            BlocksInFrontOfCamera = MOpt(rewardSpawner, "AddBlocksInFrontOfCamera");

            // RewardBlocksByCrate wants a SCENE position; a tech reports a
            // game-world one. Getting this backwards drops the crate a tile
            // away rather than at the player.
            Type worldPos = Game.GetType("WorldPosition", false);
            SceneFromGameWorld = MOpt(worldPos, "FromGameWorldPosition");
            ScenePositionProp = POpt(worldPos, "ScenePosition");

            BlockTypeProp = P(TankBlock, "BlockType");

            // Reading the starting vehicle. Optional on purpose: if a game
            // update renames one of these, the mod must still lock blocks —
            // it just cannot tell which ones came free, and says so.
            // Optional, and genuinely so: these go through the Opt helpers,
            // which do NOT record a missing target. Losing the ability to read
            // the starting vehicle costs the player their free starting blocks
            // and nothing else — stopping the whole mod over it would turn a
            // small loss into no mod at all.
            // The lock's colour. Optional: a mod that locks blocks silently
            // is worse than one that locks them visibly, but far better than
            // no mod at all, so a rename here must not stop everything.
            StartMaterialPulse = MOpt(TankBlock, "StartMaterialPulse");
            // The lasting red. Measured: TankBlock.SetDamageVisualsActive(bool)
            // goes straight to MaterialSwapper.SwapMaterialDamage, which SWAPS
            // the material and leaves it swapped. StartMaterialPulse only
            // pulses — it fades, which is why a locked block went grey again
            // a moment after it spawned.
            SetDamageVisuals = MOpt(TankBlock, "SetDamageVisualsActive");

            // Holding the red needs more than setting it once. Measured:
            // MaterialSwapper.OnUpdate runs every frame and recomputes the
            // damage look from the block's real health, so our flag was set
            // and immediately written back. The prefix in Patch_DamageMaterial
            // rides that call instead of fighting it — and to know WHICH block
            // is being coloured, it walks MaterialSwapper -> Damageable.Block.
            Type swapper = Game.GetType("MaterialSwapper", false);
            SwapMaterialDamage = MOpt(swapper, "SwapMaterialDamage");
            SwapperDamageable = FOpt(swapper, "m_Damageable");
            BlockSwapper = FOpt(TankBlock, "m_MaterialSwapper");
            SwapperOnSpawn = MOpt(swapper, "OnSpawn");
            // The one question every material refresh asks. Owning the answer
            // beats forcing the calls around it: RefreshMaterialColourAndConfig
            // reads get_IsDamaged, so a locked block is red on EVERY path —
            // fresh from a vendor, after a heal pulse ends, after a restore.
            SwapperConfig = FOpt(swapper, "m_BlockMaterialConfigProperties");
            Type cfg = Game.GetType("MaterialSwapper+BlockMatConfigProperties", false);
            ConfigIsDamaged = MOpt(cfg, "get_IsDamaged");
            DamageableBlock = POpt(Game.GetType("Damageable", false), "Block");
            Type matTypes = Game.GetType("ManTechMaterialSwap+MaterialTypes", false);
            Type matCols = Game.GetType("ManTechMaterialSwap+MaterialColour", false);
            if (StartMaterialPulse != null && matTypes != null && matCols != null)
            {
                try
                {
                    PulseTypeDamage = Enum.Parse(matTypes, "Damage");
                    PulseTypeHealing = Enum.Parse(matTypes, "Healing");
                    PulseColourDamage = Enum.Parse(matCols, "Damage");
                    PulseColourHealing = Enum.Parse(matCols, "Healing");
                }
                catch { StartMaterialPulse = null; }
            }

            TankIsPlayer = POpt(Tank, "IsPlayer");
            TankBlockman = POpt(Tank, "blockman");
            LastPlayerTank = FOpt(ManPlayer, "m_LastPlayerTank");
            AllBlocks = FOpt(Game.GetType("BlockManager", false), "allBlocks");

            // The destroyed-tech event lives on a manager whose exact home has
            // moved between versions, so try the known spellings rather than
            // pinning one.
            TankDestroyedEvent = M(T("ManBlockLimiter"), "OnTankDestroyedEvent")
                                 ?? M(T("ManPop"), "OnTankDestroyed")
                                 ?? M(T("ManSpawn"), "OnTankDestroyed");
            if (TankDestroyedEvent == null) Missing.Add("a tech-destroyed event");

            return Missing.Count == 0;
        }

        private static Type T(string name)
        {
            Type t = Game.GetType(name, false);
            if (t == null) Missing.Add("type " + name);
            return t;
        }

        private static MethodInfo M(Type t, string name)
        {
            if (t == null) return null;
            MethodInfo m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                        | BindingFlags.Instance | BindingFlags.Static)
                            .FirstOrDefault(x => x.Name == name);
            if (m == null) Missing.Add(t.Name + "." + name);
            return m;
        }

        /// Look-ups whose absence is survivable. Nothing is added to Missing,
        /// so a rename here never stops the mod from locking blocks.
        private static MethodInfo MOpt(Type t, string name)
            => t?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                             | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(x => x.Name == name);

        private static PropertyInfo POpt(Type t, string name)
            => t?.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic
                                    | BindingFlags.Instance);

        private static FieldInfo FOpt(Type t, string name)
            => t?.GetField(name, BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.Instance | BindingFlags.Static);

        private static PropertyInfo P(Type t, string name)
        {
            if (t == null) return null;
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic
                                                 | BindingFlags.Instance);
            if (p == null) Missing.Add(t.Name + "." + name + " (property)");
            return p;
        }

        // --- helpers the patches use -------------------------------------

        /// The BlockTypes enum value of a TankBlock, as its enum NAME.
        /// Names, not numbers: enum values shift when blocks are added, but
        /// GSOFuelTank_121 is GSOFuelTank_121 forever, and the same string is
        /// what the apworld's table carries.
        public static string BlockIdOf(object tankBlock)
        {
            if (tankBlock == null || BlockTypeProp == null) return null;
            try
            {
                object v = BlockTypeProp.GetValue(tankBlock, null);
                return v?.ToString();
            }
            catch { return null; }
        }

        /// Same, for callers handed a raw BlockTypes value rather than a block.
        public static string BlockTypeName(object blockTypeOrBlock)
        {
            if (blockTypeOrBlock == null) return null;
            if (blockTypeOrBlock.GetType().IsEnum) return blockTypeOrBlock.ToString();
            return BlockIdOf(blockTypeOrBlock);
        }

        /// Colour one block: red while locked, a green flash when its licence
        /// lands. Borrowed from the game's own damage and repair vocabulary,
        /// so the player has nothing new to learn.
        public static void Pulse(object tankBlock, bool locked)
        {
            if (StartMaterialPulse == null || tankBlock == null) return;
            try
            {
                StartMaterialPulse.Invoke(tankBlock, new[]
                {
                    locked ? PulseTypeDamage : PulseTypeHealing,
                    locked ? PulseColourDamage : PulseColourHealing,
                });
            }
            catch { /* a block being torn down mid-sweep is not an error */ }
        }

        /// The MaterialSwapper on a block, or null. Slow path only: the
        /// per-frame hook never calls this — it reads LockedLook's table.
        /// The config object a swapper's refresh consults. Slow path only.
        public static object ConfigOf(object swapper)
        {
            if (swapper == null || SwapperConfig == null) return null;
            try { return SwapperConfig.GetValue(swapper); }
            catch { return null; }
        }

        public static object SwapperOf(object tankBlock)
        {
            if (tankBlock == null || BlockSwapper == null) return null;
            try { return BlockSwapper.GetValue(tankBlock); }
            catch { return null; }
        }

        /// Hold a block in the game's damage red for as long as it is locked.
        /// The TankBlock a MaterialSwapper is colouring, or null.
        public static object BlockOfSwapper(object swapper)
        {
            if (swapper == null || SwapperDamageable == null || DamageableBlock == null)
                return null;
            try
            {
                object dmg = SwapperDamageable.GetValue(swapper);
                return dmg == null ? null : DamageableBlock.GetValue(dmg, null);
            }
            catch { return null; }
        }

        public static void SetLockedLook(object tankBlock, bool locked)
        {
            if (SetDamageVisuals == null || tankBlock == null) return;
            try { SetDamageVisuals.Invoke(tankBlock, new object[] { locked }); }
            catch { /* a block being torn down mid-sweep is not an error */ }
        }

        public static void CallLock(object tankBlock, bool locked)
        {
            try
            {
                MethodInfo m = locked ? LockBlockAttach : UnlockBlockAttach;
                m?.Invoke(tankBlock, m.GetParameters().Length == 0
                    ? null : new object[] { true });
            }
            catch { /* one block failing to lock must not stop the sweep */ }
        }
    }
}
