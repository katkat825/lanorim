using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Content.Sheet
{
    // SRD's nine alignments. identity, not mechanics: nothing in v1 reads it, and it is on the sheet
    // because character_sheet_decisions.md lists it among the fields every character has.
    public enum Alignment
    {
        LawfulGood,
        NeutralGood,
        ChaoticGood,
        LawfulNeutral,
        Neutral,
        ChaoticNeutral,
        LawfulEvil,
        NeutralEvil,
        ChaoticEvil,
    }

    public static class Alignments
    {
        public static readonly IReadOnlyList<Alignment> All =
            Enum.GetValues<Alignment>().ToList();

        // snake case - "lawful_good" - because the id is a key segment and "lawfulgood" reads as
        // a typo in a locale file
        public static string Id(this Alignment alignment) =>
            string.Concat(alignment.ToString()
                                   .Select((c, i) => i > 0 && char.IsUpper(c)
                                                         ? "_" + char.ToLowerInvariant(c)
                                                         : char.ToLowerInvariant(c).ToString()));

        public static bool TryParse(string id, out Alignment alignment)
        {
            foreach (Alignment a in All)
            {
                if (!string.Equals(a.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                alignment = a;
                return true;
            }

            alignment = Alignment.Neutral;
            return false;
        }

        public static string NameKey(this Alignment alignment) =>
            KeyConventions.Key(KeyConventions.UiNs, "alignment", alignment.Id(), "name");

        public static IEnumerable<string> Keys() => All.Select(a => a.NameKey());
    }
}
