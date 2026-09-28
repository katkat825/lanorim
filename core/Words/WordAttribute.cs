using System;

namespace Core.Words
{
    // THE WORD A DATA FILE USES FOR ONE ENUM VALUE, when it is not the value's name in snake case:
    // "str" for Strength, "bonus_action" for Bonus. everything else is derived (EnumWords.Id), so an
    // enum declares only the words that are exceptions, and the rest cannot drift from the names
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class WordAttribute : Attribute
    {
        public WordAttribute(string word) => Word = word;

        public string Word { get; }
    }
}
