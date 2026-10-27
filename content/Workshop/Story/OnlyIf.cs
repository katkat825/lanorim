using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Dialogue;

namespace Content.Workshop.Story
{
    // ONE PART OF AN "ONLY IF" (cc_task_p Part 4), as a sentence the author picks: they remember that..., a count is at
    // least..., the hero is a Fighter, is level 3 or higher, can cast spells, is carrying rope, the last check passed, the
    // last fight was won, the GM's roll was at least... Each is a Yarn expression and back; anything the picker has no
    // sentence for is kept as written (Raw) and shown as its Yarn in Advanced
    public enum Term
    {
        Remembers,
        Forgot,
        CountAtLeast,
        CountAtMost,
        CountIs,
        HeroClass,
        HeroLevel,
        HeroCasts,
        Carrying,
        Passed,
        Failed,
        FightWas,
        FightBroke,
        RollAtLeast,
        EncounterFight,
        EncounterWas,
        FoundCoins,
        Raw,
    }

    public sealed record Clause(Term Term, string Name = "", int Number = 0)
    {
        public string Yarn => Term switch
        {
            Term.Remembers => Name,
            Term.Forgot => "not " + Name,
            Term.CountAtLeast => $"{Name} >= {Number}",
            Term.CountAtMost => $"{Name} <= {Number}",
            Term.CountIs => $"{Name} == {Number}",
            Term.HeroClass => $"{HeroVariables.Class} == \"{Name}\"",
            Term.HeroLevel => $"{HeroVariables.Level} >= {Number}",
            Term.HeroCasts => HeroVariables.Casts,
            Term.Carrying => $"{HeroVariables.HasItem}(\"{Name}\")",
            Term.Passed => RequestVariables.Passed,
            Term.Failed => "not " + RequestVariables.Passed,
            Term.FightWas => $"{RequestVariables.Fight} == \"{Name}\"",
            Term.FightBroke => RequestVariables.FightBroke,
            Term.RollAtLeast => $"{RequestVariables.Roll} >= {Number}",
            Term.EncounterFight => RequestVariables.EncounterFight,
            Term.EncounterWas => $"{RequestVariables.Encounter} == \"{Name}\"",
            Term.FoundCoins => $"{RequestVariables.LootGold} > 0",
            _ => Name,
        };

        static readonly (Regex Shape, Func<Match, Clause> Make)[] Shapes =
        {
            (new(@"^not \$passed$"), _ => new Clause(Term.Failed)),
            (new(@"^\$passed$"), _ => new Clause(Term.Passed)),
            (new(@"^\$fight_broke$"), _ => new Clause(Term.FightBroke)),
            (new(@"^\$fight == ""(\w+)""$"), m => new Clause(Term.FightWas, m.Groups[1].Value)),
            (new(@"^\$roll >= (\d+)$"), m => new Clause(Term.RollAtLeast, Number: int.Parse(m.Groups[1].Value))),
            (new(@"^\$encounter_fight$"), _ => new Clause(Term.EncounterFight)),
            (new(@"^\$encounter == ""(\w+)""$"), m => new Clause(Term.EncounterWas, m.Groups[1].Value)),
            (new(@"^\$loot_gold > 0$"), _ => new Clause(Term.FoundCoins)),
            (new(@"^\$hero_class == ""(\w+)""$"), m => new Clause(Term.HeroClass, m.Groups[1].Value)),
            (new(@"^\$hero_level >= (\d+)$"), m => new Clause(Term.HeroLevel, Number: int.Parse(m.Groups[1].Value))),
            (new(@"^\$hero_casts$"), _ => new Clause(Term.HeroCasts)),
            (new(@"^has_item\(""(\w+)""\)$"), m => new Clause(Term.Carrying, m.Groups[1].Value)),
            (new(@"^(\$\w+) >= (-?\d+)$"), m => new Clause(Term.CountAtLeast, m.Groups[1].Value, int.Parse(m.Groups[2].Value))),
            (new(@"^(\$\w+) <= (-?\d+)$"), m => new Clause(Term.CountAtMost, m.Groups[1].Value, int.Parse(m.Groups[2].Value))),
            (new(@"^(\$\w+) == (-?\d+)$"), m => new Clause(Term.CountIs, m.Groups[1].Value, int.Parse(m.Groups[2].Value))),
            (new(@"^not (\$\w+)$"), m => new Clause(Term.Forgot, m.Groups[1].Value)),
            (new(@"^(\$\w+)$"), m => new Clause(Term.Remembers, m.Groups[1].Value)),
        };

        public static Clause Of(string yarn)
        {
            string trimmed = (yarn ?? "").Trim();

            foreach ((Regex shape, Func<Match, Clause> make) in Shapes)
            {
                Match m = shape.Match(trimmed);
                if (m.Success) return make(m);
            }

            return new Clause(Term.Raw, trimmed);
        }
    }

    // A WHOLE CONDITION: its clauses, joined by "all of these" (and) or "any of these" (or). One that mixes the two, or
    // brackets, is one Raw clause, kept as written
    public sealed record OnlyIf(IReadOnlyList<Clause> Clauses, bool Any)
    {
        public string Yarn => string.Join(Any ? " or " : " and ", Clauses.Select(c => c.Yarn));

        public static OnlyIf Of(string yarn)
        {
            string text = (yarn ?? "").Trim();

            if (Regex.Replace(text, @"has_item\(""\w+""\)", "").Contains('(') ||
                Regex.IsMatch(text, @"\band\b") && Regex.IsMatch(text, @"\bor\b"))
                return new OnlyIf(new[] { new Clause(Term.Raw, text) }, false);

            bool any = Regex.IsMatch(text, @"\bor\b");
            var parts = Regex.Split(text, any ? @"\s+or\s+" : @"\s+and\s+").Select(Clause.Of).ToList();
            var read = new OnlyIf(parts, any);

            // a condition that doesn't come back as written stays as written
            return read.Yarn == text ? read : new OnlyIf(new[] { new Clause(Term.Raw, text) }, false);
        }
    }
}
