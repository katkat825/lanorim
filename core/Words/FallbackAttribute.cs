using System;

namespace Core.Words
{
    // WHAT A FAILED EnumWords.TryParse LEAVES IN ITS OUT VALUE, when that isn't the enum's default:
    // a size that isn't one reads as Medium, an alignment that isn't one as Neutral. a reader that
    // reports the problem and carries on reads this value
    [AttributeUsage(AttributeTargets.Enum)]
    public sealed class FallbackAttribute : Attribute
    {
        public FallbackAttribute(object value) => Value = value;

        public object Value { get; }
    }
}
