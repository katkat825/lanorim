using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Content.Sheet;
using Core.Characters;

namespace Content.Creation
{
    // A HERO MADE WITH EVERY DEFAULT, at any level: the class's standard array, a human soldier, the first skills and
    // expertise on offer, the first spells, and the suggested improvement at each level that has one. What the sim
    // fights with, what `--begin` plays with, and the hero "Play this map" sends onto a map being built (cc_task_l Part 5).
    // Was the sim's Classes.Make and a copy of its first half in the game's QuickStart (agreement #9)
    public static class QuickHero
    {
        // every default creation offers, with the class, species and background already picked: learn says whether to
        // take the first spells too (a picture of the spell page wants them left)
        // `species` (cc_task_q's test hero): human unless asked; a species with lineages takes its first
        public static void Defaults(Creation making, Library library, bool learn, string learnFirst = null, string species = "human")
        {
            Species.Kind kind = library.Kind(species) ?? library.Kind("human");
            making.Pick(kind);
            if (library.LineagesOf(kind.Id).FirstOrDefault() is { } lineage) making.Pick(lineage);
            making.Pick(library.Background("soldier"));
            making.SuggestTraits();

            foreach (Skill skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList()) making.Train(skill);
            foreach (Skill skill in making.Skills.Take(making.ExpertisePicksLeft).ToList()) making.Master(skill);

            if (!learn) return;

            if (learnFirst != null && library.Spells.Find(learnFirst) is { } first) making.Learn(first);

            foreach (Core.Magic.Spell spell in making.SpellChoices.ToList())
                if (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0) making.Learn(spell);
        }

        // the whole hero. `improve: false` leaves the improvements unpicked (the sim's MageBalance always has)
        public static Hero Make(Library library, string cls, int level, string name, string learnFirst = null,
                                bool improve = true, string species = "human")
        {
            var making = new Creation(library, library.Backgrounds);

            making.StartAt(Math.Clamp(level, 1, 20));
            making.Pick(library.Class(cls));

            Defaults(making, library, learn: true, learnFirst, species);

            while (improve && making.ImprovementPicksLeft > 0 && making.Improve(making.SuggestedImprovement())) { }

            making.Call(name);

            return making.Finish() ?? throw new InvalidOperationException(
                $"{cls} {level}: " + string.Join("; ", making.Problems));
        }

        // the classes a quick hero can be, in the roster's order
        public static IEnumerable<string> Classes(Library library) => library.Classes.Select(c => c.Id);
    }
}
