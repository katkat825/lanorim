using System;

namespace Core.Words
{
    // A VALUE THE GAME WRITES BUT A DATA FILE MAY NOT NAME. Condition.None is "none" on a sheet, and
    // a spell that afflicts "none" is a typo; Trigger.Targeted is a moment the engine raises and no
    // reaction is written against. EnumWords.Id still names it, EnumWords.TryParse refuses it
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UnreadAttribute : Attribute
    {
    }
}
