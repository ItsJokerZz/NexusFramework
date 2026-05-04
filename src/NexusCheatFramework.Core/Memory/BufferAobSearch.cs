using System.Collections.Generic;

namespace NexusCheatFramework.Memory
{
    /// <summary>
    /// In-process pattern matching against a single contiguous byte buffer.
    /// Used by tests and by callers that already have memory in hand.
    /// </summary>
    public static class BufferAobSearch
    {
        public static int? IndexOf(byte[] haystack, AobPattern pattern, int start = 0)
        {
            if (haystack == null || pattern.Length == 0) return null;
            int limit = haystack.Length - pattern.Length;
            for (int i = start; i <= limit; i++)
            {
                if (pattern.FirstSolidIndex >= 0 &&
                    haystack[i + pattern.FirstSolidIndex] != pattern.FirstSolidValue) continue;
                if (pattern.Matches(haystack, i)) return i;
            }
            return null;
        }

        public static IReadOnlyList<int> AllIndexesOf(byte[] haystack, AobPattern pattern)
        {
            var hits = new List<int>();
            if (haystack == null || pattern.Length == 0) return hits;
            int limit = haystack.Length - pattern.Length;
            for (int i = 0; i <= limit; i++)
            {
                if (pattern.FirstSolidIndex >= 0 &&
                    haystack[i + pattern.FirstSolidIndex] != pattern.FirstSolidValue) continue;
                if (pattern.Matches(haystack, i)) hits.Add(i);
            }
            return hits;
        }
    }
}
