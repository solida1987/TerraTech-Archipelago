using System;

namespace TerraTechArchipelago
{
    // Reading slot_data, with nothing from Unity or the game in it.
    //
    // Its own file so the proof harness can compile it on the desktop
    // runtime. A copy in the harness would have been easier and is exactly
    // the mistake that let two versions of this project's source drift
    // apart in the first place.
    internal static class SlotData
    {
        /// How many blocks the seed's own table lists.
        ///
        /// Counted rather than parsed: the table is a flat name -> number map
        /// inside slot_data, and the mod has no JSON library worth the weight.
        /// Counting the entries is all the collector goal needs.
        internal static int CountBlockTable(string line)
        {
            int at = line.IndexOf("\"blocks\":", StringComparison.Ordinal);
            if (at < 0) return 0;
            int open = line.IndexOf('{', at);
            if (open < 0) return 0;
            int depth = 0, count = 0;
            bool inString = false, escaped = false, sawEntry = false;
            for (int i = open; i < line.Length; i++)
            {
                char c = line[i];
                if (escaped) { escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"') { inString = !inString; if (inString) sawEntry = true; continue; }
                if (inString) continue;
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) break; }
                else if (c == ',' && depth == 1) count++;
            }
            return sawEntry ? count + 1 : 0;
        }
    }
}
