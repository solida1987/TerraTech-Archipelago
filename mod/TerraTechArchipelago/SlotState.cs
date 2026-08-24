using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TerraTechArchipelago
{
    // SlotState — what this seed has already done, on this machine.
    //
    // Two jobs, and the second is the one that bites if it is missed:
    //
    //   1. Which block licences have arrived, so locks survive a reload.
    //   2. Which locations have already been sent, so nothing is ever sent
    //      twice. Campaign missions repeat by design; without this a player
    //      would farm the same check forever.
    //
    // ⚠ The dedup key has exactly one form: the location's NAME as a string.
    // Diablo II's redelivery bug came from a key that was sometimes a number
    // and sometimes a slot name, so the two spellings never matched and the
    // dedup set was effectively wiped on every reconnect. There is nothing
    // here for it to flip to.
    //
    // Stored beside the save, never inside it: a corrupt sidecar costs the
    // seed's bookkeeping, a corrupt save costs the player's game.
    internal sealed class SlotState
    {
        private const string Magic = "TTAP1";

        public string SlotKey = "";
        public string Seed = "";

        public readonly HashSet<string> UnlockedBlocks = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> SentLocations = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> SeenBlockPickups = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> SeenBlockAttaches = new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Counters = new Dictionary<string, int>(StringComparer.Ordinal);

        /// Highest item index applied. Received items are replayed on every
        /// connect; this is what makes replay idempotent instead of doubling.
        public int HighestItemIndex = -1;

        private string _path;

        public static string PathFor(string slotKey)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TerraTechArchipelago");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "slot_" + Sanitise(slotKey) + ".txt");
        }

        private static string Sanitise(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.Length == 0 ? "unknown" : sb.ToString();
        }

        public static SlotState Load(string slotKey, string seed)
        {
            var state = new SlotState { SlotKey = slotKey, Seed = seed };
            state._path = PathFor(slotKey);
            if (!File.Exists(state._path)) return state;

            try
            {
                foreach (string raw in File.ReadAllLines(state._path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line == Magic) continue;
                    int sep = line.IndexOf('\t');
                    if (sep <= 0) continue;
                    string tag = line.Substring(0, sep);
                    string val = line.Substring(sep + 1);

                    switch (tag)
                    {
                        case "unlock": state.UnlockedBlocks.Add(val); break;
                        case "sent": state.SentLocations.Add(val); break;
                        case "pickup": state.SeenBlockPickups.Add(val); break;
                        case "attach": state.SeenBlockAttaches.Add(val); break;
                        case "index":
                            int idx;
                            if (int.TryParse(val, out idx)) state.HighestItemIndex = idx;
                            break;
                        case "count":
                            int tab = val.IndexOf('=');
                            int n;
                            if (tab > 0 && int.TryParse(val.Substring(tab + 1), out n))
                                state.Counters[val.Substring(0, tab)] = n;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                // A damaged sidecar must not stop the player from playing. The
                // cost is re-sending checks the server already has, and the
                // server ignores duplicates — so the failure is recoverable by
                // design rather than by luck.
                Plugin.Log("Could not read the slot file, starting fresh: " + e.Message);
            }
            return state;
        }

        public void Save()
        {
            if (_path == null) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(Magic);
                sb.Append("index\t").Append(HighestItemIndex).AppendLine();
                foreach (string s in UnlockedBlocks) sb.Append("unlock\t").AppendLine(s);
                foreach (string s in SentLocations) sb.Append("sent\t").AppendLine(s);
                foreach (string s in SeenBlockPickups) sb.Append("pickup\t").AppendLine(s);
                foreach (string s in SeenBlockAttaches) sb.Append("attach\t").AppendLine(s);
                foreach (var kv in Counters)
                    sb.Append("count\t").Append(kv.Key).Append('=').Append(kv.Value).AppendLine();

                // Write beside and move into place, so a crash mid-write can
                // never leave a half file that reads as "nothing done yet".
                string tmp = _path + ".part";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tmp, _path);
            }
            catch (Exception e)
            {
                Plugin.Log("Could not write the slot file: " + e.Message);
            }
        }

        public int Bump(string counter)
        {
            int n;
            Counters.TryGetValue(counter, out n);
            n += 1;
            Counters[counter] = n;
            return n;
        }
    }
}
