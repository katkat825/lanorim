using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Core.Tables;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Space;

namespace Sim
{
    // TIER 2.13 OF THE 2026-09-24 RUN: every class at levels 1 to 5 against the sample campaign's
    // fights, played by the AutoPlayer through CombatSession (a stance first, heal when low, the best
    // previewed damage). Real heroes made the way creation makes them; the monsters' own brains.
    // Prints a table and marks the rows worth a look; it never retunes anything.
    public static class Classes
    {
        public static readonly string[] All =
            { "barbarian", "fighter", "rogue", "mage", "cleric", "paladin", "druid" };

        // the level each fight is met at in the sample campaign
        static readonly (string Table, string Entry, int MetAt)[] Fights =
        {
            ("set_pieces", "cellar_rats", 1),
            ("mill_road", "wolves", 2),
            ("mill_road", "bandits", 2),
            ("set_pieces", "goblin_camp", 3),
        };

        public static int Run(string[] args, Func<string, string> find)
        {
            int runs = args.Length > 0 && int.TryParse(args[0], out int n) ? n : 200;
            string only = args.Length > 1 ? args[1] : null;

            string folder = find("campaigns/sample_millbrook/pack.json");
            Package pack = Package.Read(Path.GetDirectoryName(folder));

            if (!pack.Sound)
            {
                Console.Error.WriteLine("the sample campaign does not load: " + string.Join("; ", pack.Problems));
                return 1;
            }

            Library library = Library.Srd();

            Console.WriteLine($"{runs} fights per row, the AutoPlayer against the sample campaign's fights.");
            Console.WriteLine("'<' marks a win rate under 75% at a level the fight is met at or after; '!' under 50%.");
            Console.WriteLine();
            Console.WriteLine("class      lvl  fight          wins   rounds  hp left");

            var outliers = new List<string>();

            foreach (string cls in All.Where(c => only == null || c == only))
                for (int level = 1; level <= 5; level++)
                    foreach ((string table, string entry, int metAt) in Fights)
                    {
                        EncounterEntry fight = pack.Encounter(table).Entries.Single(e => e.Id == entry);
                        MapLayout map = pack.Maps[fight.Map];

                        int wins = 0, rounds = 0, hp = 0;

                        for (int run = 0; run < runs; run++)
                        {
                            Hero hero = Make(library, cls, level);
                            var rng = new SeededRng(run * 7919 + level * 131 + cls.Length * 17 + entry.Length);
                            var resolver = new StandardResolver(rng);

                            Battle battle = Battle.From(library, map, hero, fight, resolver);
                            var session = new CombatSession(battle, library.Items, library.Forms);

                            Outcome outcome = new AutoPlayer().Play(session);

                            if (outcome == Outcome.HeroesWon) wins++;

                            rounds += battle.Fight.Round;
                            hp += Math.Max(0, hero.Actor.Health.Current) * 100 / Math.Max(1, hero.Actor.Health.Maximum);
                        }

                        double rate = 100.0 * wins / runs;
                        string mark = level >= metAt && rate < 50 ? "!" : level >= metAt && rate < 75 ? "<" : " ";

                        string row = $"{cls,-10} {level,3}  {entry,-13} {rate,5:0.0}%  {(double)rounds / runs,6:0.0}  {(double)hp / runs,6:0}%  {mark}";
                        Console.WriteLine(row);

                        if (mark != " ") outliers.Add(row);
                    }

            Console.WriteLine();
            Console.WriteLine(outliers.Count == 0 ? "no outliers" : $"{outliers.Count} rows worth a look:");
            foreach (string row in outliers) Console.WriteLine("  " + row);

            return 0;
        }

        // ONE FIGHT, EVERY LINE OF ITS LOG: sim trace <class> <level> <fight> [seed]
        public static int Trace(string[] args, Func<string, string> find)
        {
            if (args.Length < 3 || !int.TryParse(args[1], out int level))
            {
                Console.Error.WriteLine("sim trace <class> <level> <fight> [seed]");
                return 1;
            }

            int seed = args.Length > 3 && int.TryParse(args[3], out int s) ? s : 1;

            Package pack = Package.Read(Path.GetDirectoryName(find("campaigns/sample_millbrook/pack.json")));
            Library library = Library.Srd();

            EncounterEntry fight = pack.Encounters.SelectMany(t => t.Entries).Single(e => e.Id == args[2]);
            Hero hero = Make(library, args[0], level);

            Console.WriteLine($"{hero.Class.Id} {hero.Level}: {hero.Actor.Health.Maximum} hp, AC {hero.Actor.ArmorClass}, " +
                              $"attacks {string.Join(", ", hero.Attacks.Select(a => a.Id))}");

            var log = new FightLog();
            Battle battle = Battle.From(library, pack.Maps[fight.Map], hero, fight,
                                        new StandardResolver(new SeededRng(seed)), null, null, log);

            var session = new CombatSession(battle, library.Items, library.Forms);

            if (args.Length > 4 && args[4] == "options")
            {
                session.Start();

                foreach (ActionOption option in session.Options())
                {
                    string aims = "";

                    if (option.Enabled && session.Select(option))
                    {
                        aims = string.Join(", ", session.LegalTargets().Select(t => $"{t.Id}~{session.Preview(t).ExpectedDamage:0.0}"));
                        session.Cancel();
                    }

                    Console.WriteLine($"  option {option} [{option.Targeting}] -> {aims}");
                }
            }

            Outcome outcome = new AutoPlayer { Note = n => Console.WriteLine("  > " + n) }.Play(session);

            foreach (LogLine line in log.Lines) Console.WriteLine("  " + line);

            Console.WriteLine(outcome);
            return 0;
        }

        // the hero creation makes: the class's standard array, a human soldier, the first skills and
        // spells on offer, the suggested improvement at 4
        public static Hero Make(Library library, string cls, int level)
        {
            var making = new Content.Creation.Creation(library, library.Backgrounds);

            making.StartAt(level);
            making.Pick(library.Class(cls));
            making.Pick(library.Kind("human"));
            making.Pick(library.Background("soldier"));

            foreach (Skill skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList()) making.Train(skill);
            foreach (Skill skill in making.Skills.Take(making.ExpertisePicksLeft).ToList()) making.Master(skill);

            foreach (Core.Magic.Spell spell in making.SpellChoices.ToList())
                if (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0) making.Learn(spell);

            while (making.ImprovementPicksLeft > 0 && making.Improve(making.SuggestedImprovement())) { }

            making.Call("Sim");

            Hero hero = making.Finish();

            if (hero == null)
                throw new InvalidOperationException($"{cls} {level}: " + string.Join("; ", making.Problems));

            return hero;
        }
    }
}
