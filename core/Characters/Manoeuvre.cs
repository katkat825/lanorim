using System;
using Core.Words;

namespace Core.Characters
{
    // the three plain things a turn can be spent on besides attacking and casting. anyone may take
    // one as an action; a feature can let a bonus action do it instead - that is the whole of the
    // Rogue's Cunning Action, and it is never an extra action (v1_class_roster.md).
    [Flags]
    public enum Manoeuvre
    {
        [Unread] None = 0,
        Dash = 1 << 0,
        Disengage = 1 << 1,
        Hide = 1 << 2,
    }

    public static class Manoeuvres
    {
        public static readonly Manoeuvre[] All = { Manoeuvre.Dash, Manoeuvre.Disengage, Manoeuvre.Hide };
    }
}
