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
        public static MethodInfo TankDestroyedEvent { get; private set; }
        public static MethodInfo DamageColourFloat { get; private set; }

        // Methods we call
        public static MethodInfo LockBlockAttach { get; private set; }
        public static MethodInfo UnlockBlockAttach { get; private set; }
        public static MethodInfo DiscoverEntireTier { get; private set; }
        public static MethodInfo SetBlockState { get; private set; }
        public static MethodInfo GetAllCorpIDs { get; private set; }
        public static MethodInfo AddMoney { get; private set; }

        // Properties we read
        public static PropertyInfo BlockTypeProp { get; private set; }

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
            DamageColourFloat = M(ManTechMaterialSwap, "GetDamageColourFloat");

            LockBlockAttach = M(TankBlock, "LockBlockAttach");
            UnlockBlockAttach = M(TankBlock, "UnlockBlockAttach");
            DiscoverEntireTier = M(ManLicenses, "DiscoverEntireTier");
            SetBlockState = M(ManLicenses, "SetBlockState");
            GetAllCorpIDs = M(ManLicenses, "GetAllCorpIDs");
            AddMoney = M(ManPlayer, "AddMoney");

            BlockTypeProp = P(TankBlock, "BlockType");

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
