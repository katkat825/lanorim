using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;

namespace Content.Spells
{
    // every spell name the SRD prints. a name on this list is a promise about what the spell does
    // (decisions_checklist.md section 1, the hard rule): a spell flagged as an approximation may
    // never be shown under one of them, and a test holds the locale to that.
    public static class SrdSpellNames
    {
        static IReadOnlyCollection<string> _all;

        public static IReadOnlyCollection<string> All => _all ??= Load();

        // case and spacing are not what makes two names the same spell to a player
        public static bool IsSrd(string name) =>
            !string.IsNullOrWhiteSpace(name) && All.Contains(Fold(name));

        static IReadOnlyCollection<string> Load()
        {
            if (!Json.TryParse(Srd.Read("reference/spell_names.json"),
                               out System.Text.Json.JsonDocument document, out string problem))
                throw new InvalidOperationException("the SRD spell name list does not parse: " +
                                                    problem);

            using (document)
                return new HashSet<string>(document.RootElement.Strings("names").Select(Fold),
                                           StringComparer.Ordinal);
        }

        static string Fold(string name) =>
            string.Join(" ", name.Trim().ToLowerInvariant()
                                 .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
