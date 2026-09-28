using System.Collections.Generic;
using Core.Words;

namespace Core.Magic
{
    [Fallback(School.Evocation)]
    public enum School
    {
        Abjuration,
        Conjuration,
        Divination,
        Enchantment,
        Evocation,
        Illusion,
        Necromancy,
        Transmutation,
    }

    public static class Schools
    {
        public static readonly IReadOnlyList<School> All = new[]
        {
            School.Abjuration, School.Conjuration, School.Divination, School.Enchantment,
            School.Evocation, School.Illusion, School.Necromancy, School.Transmutation,
        };

    }
}
