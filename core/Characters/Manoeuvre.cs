using System;

namespace Core.Characters
{
    // the three plain things a turn can be spent on besides attacking and casting. anyone may take
    // one as an action; a feature can let a bonus action do it instead - that is the whole of the
    // Rogue's Cunning Action, and it is never an extra action (v1_class_roster.md).
    [Flags]
    public enum Manoeuvre
    {
        None = 0,
        Dash = 1 << 0,
        Disengage = 1 << 1,
        Hide = 1 << 2,
    }

    public static class Manoeuvres
    {
        public static readonly Manoeuvre[] All = { Manoeuvre.Dash, Manoeuvre.Disengage, Manoeuvre.Hide };

        public static string Id(this Manoeuvre manoeuvre) => manoeuvre.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out Manoeuvre manoeuvre)
        {
            foreach (Manoeuvre m in All)
            {
                if (!string.Equals(m.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                manoeuvre = m;
                return true;
            }

            manoeuvre = Manoeuvre.None;
            return false;
        }
    }
}
