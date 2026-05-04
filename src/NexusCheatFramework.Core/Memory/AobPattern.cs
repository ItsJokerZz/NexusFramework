using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NexusCheatFramework.Memory
{
    /// <summary>
    /// Parsed array-of-bytes pattern with optional wildcards.
    /// Supports tokens: "AB" (exact byte), "??", "?", "**" (wildcard byte).
    /// Tokens may be space-separated or run together (e.g. "488B????89").
    /// </summary>
    public sealed class AobPattern
    {
        public string Original { get; }
        public byte?[] Bytes { get; }
        public int Length => Bytes.Length;

        /// <summary>Index of the first non-wildcard byte (-1 if pattern is all wildcards).</summary>
        public int FirstSolidIndex { get; }
        public byte FirstSolidValue { get; }

        private AobPattern(string original, byte?[] bytes)
        {
            Original = original;
            Bytes = bytes;
            FirstSolidIndex = -1;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i].HasValue)
                {
                    FirstSolidIndex = i;
                    FirstSolidValue = bytes[i]!.Value;
                    break;
                }
            }
        }

        public static AobPattern Parse(string pattern)
        {
            if (pattern is null) throw new ArgumentNullException(nameof(pattern));
            var trimmed = pattern.Trim();
            if (trimmed.Length == 0) throw new ArgumentException("Pattern is empty.", nameof(pattern));

            var tokens = TokenizePattern(trimmed);
            if (tokens.Count == 0) throw new ArgumentException("Pattern produced no tokens.", nameof(pattern));

            var bytes = new byte?[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (IsWildcard(t)) { bytes[i] = null; continue; }
                if (t.Length != 2 || !IsHex(t[0]) || !IsHex(t[1]))
                    throw new ArgumentException($"Invalid token '{t}' in pattern.", nameof(pattern));
                bytes[i] = byte.Parse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return new AobPattern(trimmed, bytes);
        }

        private static bool IsWildcard(string t)
            => t == "??" || t == "?" || t == "**" || t == "*";

        private static bool IsHex(char c)
            => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        private static List<string> TokenizePattern(string s)
        {
            var hasSeparator = s.Any(char.IsWhiteSpace);
            var result = new List<string>();

            if (hasSeparator)
            {
                foreach (var raw in s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var token = raw;
                    if (token == "?") token = "??";
                    else if (token == "*") token = "**";
                    result.Add(token);
                }
                return result;
            }

            // Run-together pattern: pairs of chars; "??" and "**" act as wildcards.
            int i = 0;
            while (i < s.Length)
            {
                if (i + 1 >= s.Length)
                    throw new ArgumentException("Run-together pattern has odd length.");
                var c1 = s[i];
                var c2 = s[i + 1];
                if ((c1 == '?' && c2 == '?') || (c1 == '*' && c2 == '*'))
                    result.Add(new string(new[] { c1, c2 }));
                else if (IsHex(c1) && IsHex(c2))
                    result.Add(new string(new[] { c1, c2 }));
                else
                    throw new ArgumentException($"Invalid characters '{c1}{c2}' in run-together pattern.");
                i += 2;
            }
            return result;
        }

        public bool Matches(byte[] buffer, int offset)
        {
            if (offset < 0 || offset + Length > buffer.Length) return false;
            for (int j = 0; j < Length; j++)
            {
                var pb = Bytes[j];
                if (!pb.HasValue) continue;
                if (buffer[offset + j] != pb.Value) return false;
            }
            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder(Length * 3);
            for (int i = 0; i < Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Bytes[i].HasValue ? Bytes[i]!.Value.ToString("X2") : "??");
            }
            return sb.ToString();
        }
    }
}
