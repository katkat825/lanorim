using System;
using System.Collections.Generic;
using Core.Dice;
using Core.Words;

namespace Content.Schema
{
    // WHAT A CONTENT FILE IS ALLOWED TO SAY, derived from the enums rather than listed beside them.
    // A word list written out by hand is a second description of an enum and is free to drift from
    // it; asking the enum means adding a damage type adds the word that names it, and a reordered
    // enum cannot silently re-mean every file that used it.
    //
    // LIFTED from the old build near enough verbatim. The one thing that went is the die ladder -
    // lanorim's Die is the d4-d100 set with the side count as its value, so "d8" is still derived
    // and still cannot drift.
    public static class Vocabulary
    {
        // the enum value is the side count, so "d8" is derived and can't drift
        public static string NameOf(Die die) => die == Die.None ? "none" : "d" + (int)die;

        // a save's hit dice: "d8", or "none"
        public static bool TryDie(string word, out Die die)
        {
            if (!string.Equals(word?.Trim(), "none", StringComparison.OrdinalIgnoreCase))
                return DieExtensions.TryParse(word, out die);

            die = Die.None;
            return true;
        }

        public static string Offer(IEnumerable<string> words) => string.Join(", ", words);

        public static string Offer<TEnum>() where TEnum : struct, Enum => Offer(EnumWords.Names<TEnum>());
    }
}
