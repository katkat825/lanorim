using System;
using System.Collections.Generic;
using Content.Screens;
using Core.Localization;

namespace Content.Creation
{
    public sealed partial class Creation
    {
        // THE TITLE SAYS WHO YOU'RE MAKING (cc_task_m2 Part 1): "A new character" before anything is picked, "A new
        // Fighter" with a class, "A new Elf Fighter" with a species too, and "A new Elf" with a species and no class.
        // A key and the keys of the names that fill it: the screen says each name, then the line, so a language that
        // puts the species after the class only reorders its own row's placeholders
        public (string Key, IReadOnlyList<string> Names) Title =>
            (Class, Species) switch
            {
                (null, null) => (TitleKey, Array.Empty<string>()),
                (_, null) => (TitleClassKey, new[] { KeyConventions.ClassName(Class.Id) }),
                (null, _) => (TitleSpeciesKey, new[] { Species.NameKey }),
                _ => (TitleSpeciesClassKey, new[] { Species.NameKey, KeyConventions.ClassName(Class.Id) }),
            };

        static readonly Func<string, string> K = ScreenKeys.For("create");

        public static readonly string TitleKey = K("title");

        public static readonly string TitleClassKey = K("title_class");

        public static readonly string TitleSpeciesKey = K("title_species");

        public static readonly string TitleSpeciesClassKey = K("title_species_class");

        public static IEnumerable<string> TitleKeys() =>
            new[] { TitleKey, TitleClassKey, TitleSpeciesKey, TitleSpeciesClassKey };
    }
}
