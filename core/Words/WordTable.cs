using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Core.Words
{
    // ONE ENUM'S WORDS, read once from its declaration: the snake-case name unless a [Word] says
    // otherwise, whether a data file may use it ([Unread]), and what a failed parse leaves ([Fallback]).
    // in value order, which is the declaration order for every enum here and is stable where that
    // isn't promised
    static class WordTable<T> where T : struct, Enum
    {
        public static readonly bool IsFlags = typeof(T).IsDefined(typeof(FlagsAttribute), false);

        public static readonly T Fallback = ReadFallback();

        public static readonly IReadOnlyList<(T value, string word, bool read)> Entries = ReadEntries();

        static readonly Dictionary<T, string> ByValue = Entries.ToDictionary(e => e.value, e => e.word);

        static readonly Dictionary<string, T> ByWord =
            Entries.Where(e => e.read)
                   .ToDictionary(e => e.word, e => e.value, StringComparer.OrdinalIgnoreCase);

        public static bool TryWord(T value, out string word) => ByValue.TryGetValue(value, out word);

        public static bool TryValue(string word, out T value) => ByWord.TryGetValue(word, out value);

        static T ReadFallback() =>
            typeof(T).GetCustomAttribute<FallbackAttribute>()?.Value is T fallback ? fallback : default;

        static IReadOnlyList<(T, string, bool)> ReadEntries()
        {
            var entries = new List<(T, string, bool)>();

            foreach (T value in Enum.GetValues<T>())
            {
                FieldInfo field = typeof(T).GetField(value.ToString(), BindingFlags.Public | BindingFlags.Static);
                string word = field?.GetCustomAttribute<WordAttribute>()?.Word ?? EnumWords.Snake(value.ToString());
                bool read = field == null || !field.IsDefined(typeof(UnreadAttribute), false);

                entries.Add((value, word, read));
            }

            return entries;
        }
    }
}
