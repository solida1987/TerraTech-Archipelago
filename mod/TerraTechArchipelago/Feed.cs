using System;
using System.Reflection;

namespace TerraTechArchipelago
{
    // Telling the player what the multiworld just did, inside the game.
    //
    // A check that only appears in a log file might as well not have
    // happened: the player is driving, not reading BepInEx output. So every
    // check sent and every item received says so on screen.
    //
    // Three routes, in order of how well they fit:
    //
    //   1. UIMPChat.s_MissionUpdatesInst — the mission-updates feed. Measured
    //      as a STATIC instance, so it is there in single player too, and it
    //      is the channel the game itself uses for "something happened".
    //   2. UIMPChat.s_ChatInst — the chat window proper.
    //   3. ScreenLog — a Singleton.Manager, always present, plainer.
    //
    // Whatever happens, the line also goes to the mod's log, so a bug report
    // has it even when none of the three could be reached.
    internal static class Feed
    {
        private static bool _resolved;
        private static FieldInfo _missionInst;
        private static FieldInfo _chatInst;
        private static MethodInfo _addMission;
        private static Type _screenLog;
        private static MethodInfo _screenAdd;
        private static bool _warnedNoSurface;
        private static int _misses;

        /// ⚠ Resolving is retried, not decided once. At the main menu the
        /// chat's static instances are still null and ScreenLog is not up, so
        /// the first attempt fails for every session — and caching that
        /// failure meant nothing was ever shown in-game afterwards.
        private static void Resolve()
        {
            if (_resolved) return;
            try
            {
                Type chat = Reflect.Game?.GetType("UIMPChat", false);
                if (chat != null)
                {
                    const BindingFlags stat = BindingFlags.Public | BindingFlags.NonPublic
                                            | BindingFlags.Static;
                    _missionInst = chat.GetField("s_MissionUpdatesInst", stat);
                    _chatInst = chat.GetField("s_ChatInst", stat);
                    _addMission = chat.GetMethod("AddMissionMessage",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                _screenLog = Reflect.Game?.GetType("ScreenLog", false);
                _screenAdd = _screenLog?.GetMethod("Add",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch { /* the log fallback below still works */ }

            // Only call it resolved once something is actually reachable.
            _resolved = (_addMission != null
                         && (_missionInst?.GetValue(null) != null
                             || _chatInst?.GetValue(null) != null))
                        || Discovery.FindManager(_screenLog) != null;
        }

        /// Put one line in front of the player. Never throws: a mod that
        /// crashes while announcing good news is worse than a quiet one.
        public static void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Plugin.Log(text);

            Resolve();
            string line = "[AP] " + text;

            if (_addMission != null)
            {
                foreach (FieldInfo inst in new[] { _missionInst, _chatInst })
                {
                    if (inst == null) continue;
                    try
                    {
                        object ui = inst.GetValue(null);
                        if (ui == null) continue;
                        _addMission.Invoke(ui, new object[] { line });
                        return;
                    }
                    catch { /* try the next one */ }
                }
            }

            try
            {
                object sl = Discovery.FindManager(_screenLog);
                if (sl != null && _screenAdd != null)
                {
                    _screenAdd.Invoke(sl, new object[] { line });
                    return;
                }
            }
            catch { }

            // Only complain after several genuine misses. The first ones
            // happen at the main menu, where there is no HUD yet and nothing
            // is wrong — warning there would be crying wolf every session.
            if (++_misses >= 10 && !_warnedNoSurface)
            {
                _warnedNoSurface = true;
                Plugin.Log("No in-game message surface was reachable, so "
                         + "Archipelago activity will only appear in this log.");
            }
        }
    }
}
