using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Words
{
    // WORD <-> ENUM, ONCE. every enum a file names is read and written here, so there is one loop
    // over the values, one comparison and one rule for lists, where there were thirty-odd copies
    // (cc_task_dedupe-methods.md #1). two conventions, because two kinds of file use them:
    //
    // - Id / TryParse: a data file's word. the value's name in snake case ("animal_handling",
    //   "next_turn_end"), unless the enum member carries a [Word] ("str", "bonus_action"). a member
    //   marked [Unread] is written but refused on reading. a [Flags] enum is a list joined with
    //   pipes, "attacks|saves", because a comma reads as the next field in a data file.
    //
    // - Name / TryName: the save file's and the settings file's word, the value's own name in
    //   lower case ("strength", "animalhandling"). it is what those files have always said, so a
    //   save written before an enum gained a [Word] still reads.
    //
    // neither takes digits: Enum.TryParse would read "3" as the third member, and reordering the
    // enum would re-mean every file that said it
    public static class EnumWords
    {
        public static string Id<T>(this T value) where T : struct, Enum
        {
            if (WordTable<T>.TryWord(value, out string word)) return word;

            if (!WordTable<T>.IsFlags) throw new ArgumentOutOfRangeException(nameof(value), value, null);

            long bits = Bits(value);

            return string.Join("|", WordTable<T>.Entries
                                                .Where(e => Bits(e.value) != 0 && (bits & Bits(e.value)) == Bits(e.value))
                                                .Select(e => e.word));
        }

        public static bool TryParse<T>(string word, out T value) where T : struct, Enum
        {
            if (WordTable<T>.IsFlags) return TryParseSet(word, out value);

            if (word != null && WordTable<T>.TryValue(word.Trim(), out T found))
            {
                value = found;
                return true;
            }

            value = WordTable<T>.Fallback;
            return false;
        }

        // the words a data file may use, in the enum's order. a [Flags] enum's empty set is left out:
        // "none" is not one of the things a list can hold
        public static IReadOnlyList<string> Ids<T>() where T : struct, Enum =>
            WordTable<T>.Entries.Where(e => e.read && !(WordTable<T>.IsFlags && Bits(e.value) == 0))
                                .Select(e => e.word)
                                .ToArray();

        // "attacks|saves". nothing at all, or the empty set's own word, is the empty set - when a
        // data file may name it
        static bool TryParseSet<T>(string words, out T set) where T : struct, Enum
        {
            set = default;

            bool emptyAllowed = WordTable<T>.Entries.Any(e => e.read && Bits(e.value) == 0);

            if (string.IsNullOrWhiteSpace(words)) return emptyAllowed;

            long bits = 0;

            foreach (string part in words.Split('|'))
            {
                if (!WordTable<T>.TryValue(part.Trim(), out T one)) return false;

                bits |= Bits(one);
            }

            set = (T)Enum.ToObject(typeof(T), bits);
            return true;
        }

        static long Bits<T>(T value) where T : struct, Enum => Convert.ToInt64(value);

        public static string Name<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Names<T>() where T : struct, Enum =>
            Enum.GetValues<T>().Select(Name).ToArray();

        public static bool TryName<T>(string word, out T value) where T : struct, Enum
        {
            value = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim();

            foreach (T one in Enum.GetValues<T>())
            {
                if (!string.Equals(Name(one), trimmed, StringComparison.OrdinalIgnoreCase)) continue;

                value = one;
                return true;
            }

            return false;
        }

        // AnimalHandling -> animal_handling
        public static string Snake(string pascal)
        {
            var s = new System.Text.StringBuilder();

            for (int i = 0; i < pascal.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascal[i])) s.Append('_');

                s.Append(char.ToLowerInvariant(pascal[i]));
            }

            return s.ToString();
        }
    }
}
