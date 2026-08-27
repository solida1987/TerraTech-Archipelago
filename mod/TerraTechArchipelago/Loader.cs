using System;
using System.Reflection;

namespace TerraTechArchipelago
{
    // Loader — the entry points a mod manager can call.
    //
    // TerraTech's community loaders do not agree on one convention, so the
    // mod answers to all of them. Every one funnels into Plugin.Init, which
    // is idempotent — being started twice must never patch twice.
    public static class ModEntry
    {
        /// 0ModManager / TTQMM style.
        public static void Init() => Plugin.Init();

        /// Some loaders look for a Main.
        public static void Main() => Plugin.Init();

        /// Official ManMods style: the game calls a well-known method on a
        /// type it discovers in the assembly.
        public static void EarlyInit() => Plugin.Init();

        /// Called once per frame by loaders that offer it. Loaders that do
        /// not will leave the bridge to the fallback pump below.
        public static void Update() => Plugin.Instance?.Update(Now());

        private static double Now()
        {
            try
            {
                Type time = Type.GetType("UnityEngine.Time, UnityEngine.CoreModule")
                            ?? Type.GetType("UnityEngine.Time, UnityEngine");
                PropertyInfo p = time?.GetProperty("realtimeSinceStartup",
                    BindingFlags.Public | BindingFlags.Static);
                object v = p?.GetValue(null, null);
                if (v is float f) return f;
            }
            catch { }
            return DateTime.UtcNow.TimeOfDay.TotalSeconds;
        }
    }
}
