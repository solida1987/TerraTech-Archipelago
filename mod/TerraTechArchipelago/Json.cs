using System;
using System.Globalization;
using System.Text;

namespace TerraTechArchipelago
{
    // Json — just enough to read the client's messages and write ours.
    //
    // Deliberately not a library. The mod is injected into a Unity game whose
    // own JSON assemblies are already loaded at versions we do not control;
    // adding another is how a mod breaks a game it never touched. The protocol
    // is ours and it is small: flat objects, string and integer fields.
    //
    // ⚠ This is a reader for OUR protocol, not a general JSON parser. It does
    // not handle nested objects or arrays of objects, and it should not grow
    // to — if the protocol needs those, the protocol is the thing to fix.
    internal static class Json
    {
        /// Read a string or number field as text. Null when absent.
        public static string Str(string json, string key)
        {
            int i = FindValue(json, key);
            if (i < 0) return null;

            if (json[i] == '"')
            {
                var sb = new StringBuilder();
                for (int p = i + 1; p < json.Length; p++)
                {
                    char c = json[p];
                    if (c == '\\' && p + 1 < json.Length)
                    {
                        char n = json[++p];
                        switch (n)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            case 'r': sb.Append('\r'); break;
                            case 'u':
                                if (p + 4 < json.Length)
                                {
                                    string hex = json.Substring(p + 1, 4);
                                    int code;
                                    if (int.TryParse(hex, NumberStyles.HexNumber,
                                                     CultureInfo.InvariantCulture, out code))
                                    {
                                        sb.Append((char)code);
                                        p += 4;
                                    }
                                }
                                break;
                            default: sb.Append(n); break;
                        }
                        continue;
                    }
                    if (c == '"') break;
                    sb.Append(c);
                }
                return sb.ToString();
            }

            int end = i;
            while (end < json.Length && json[end] != ',' && json[end] != '}') end++;
            return json.Substring(i, end - i).Trim();
        }

        public static int Int(string json, string key, int fallback)
        {
            string s = Str(json, key);
            int v;
            return s != null && int.TryParse(s, NumberStyles.Integer,
                                             CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        /// Index of the first character of "key"'s value, or -1.
        ///
        /// Matches on the quoted key followed by a colon, so a key name that
        /// also appears inside a VALUE cannot be mistaken for the field —
        /// "from":"index hunter" must not satisfy a lookup for "index".
        private static int FindValue(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int at = 0;
            while (true)
            {
                at = json.IndexOf(needle, at, StringComparison.Ordinal);
                if (at < 0) return -1;
                int p = at + needle.Length;
                while (p < json.Length && char.IsWhiteSpace(json[p])) p++;
                if (p < json.Length && json[p] == ':')
                {
                    p++;
                    while (p < json.Length && char.IsWhiteSpace(json[p])) p++;
                    return p < json.Length ? p : -1;
                }
                at = p;   // that was a value, not a key; keep looking
            }
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
