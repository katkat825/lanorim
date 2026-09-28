using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Content.Schema
{
    // UNKNOWN KEYS ARE ERRORS. a typo used to load as nothing - "sway_dcie" was simply a sway with no
    // dice - which is how a setting could start life by accident (cc_task_dedupe-effects.md, Phase
    // 6). every reader checks each object against the keys it takes and names the nearest one.
    // "_" keys are comments and always allowed. an old key from before a rename is refused the
    // same way: the migrations and their retired-key table were one-time scaffolding, deleted
    // 2026-09-28 (cc_task_godfiles-dupes-efficiency.md A) - a public format brings them back on purpose.
    public static class Keyed
    {
        public static void OnlyKnown(JsonElement raw, IEnumerable<string> known, string where,
                                     List<string> problems)
        {
            if (raw.ValueKind != JsonValueKind.Object) return;

            var keys = known as ISet<string> ?? new HashSet<string>(known, StringComparer.Ordinal);

            foreach (JsonProperty property in raw.EnumerateObject())
            {
                if (property.Name.StartsWith("_") || keys.Contains(property.Name)) continue;

                string near = Nearest(property.Name, keys);

                problems.Add($"{where}: '{property.Name}' is not a key here" +
                             (near != null ? $" - did you mean '{near}'?" : ""));
            }
        }

        // the known key fewest edits away, if any is close enough to be the one meant
        public static string Nearest(string word, IEnumerable<string> known)
        {
            string best = null;
            int bestDistance = int.MaxValue;

            foreach (string key in known)
            {
                int d = Distance(word, key);

                if (d < bestDistance || d == bestDistance && string.CompareOrdinal(key, best) < 0)
                {
                    best = key;
                    bestDistance = d;
                }
            }

            return best != null && bestDistance <= Math.Max(2, word.Length / 3) ? best : null;
        }

        // edits between two words, a swapped pair of letters counting as one: "cots" is one from
        // "cost", not two
        static int Distance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;

                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);

                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }

            return d[a.Length, b.Length];
        }
    }
}
