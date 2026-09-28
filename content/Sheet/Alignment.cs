using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Core.Words;

namespace Content.Sheet
{
    // SRD's nine alignments. identity, not mechanics: nothing in v1 reads it, and it is on the sheet
    // because character_sheet_decisions.md lists it among the fields every character has. the id is
    // snake case - "lawful_good" - because it is a key segment and "lawfulgood" reads as a typo in a
    // locale file
    [Fallback(Alignment.Neutral)]
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

        public static string NameKey(this Alignment alignment) => alignment.UiNameKey("alignment");

        public static IEnumerable<string> Keys() => All.Select(a => a.NameKey());
    }
}
