using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;
using Core.Statistics;

namespace Sim
{
    // the balance harness and the small dev tools. never references Godot, so it runs anywhere
    // dotnet does and can play ten thousand fights while you make a cup of tea.
    public static class Program
    {
        public static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0].ToLowerInvariant() : "balance";

            switch (command)
            {
                case "locale": return Locale(args.Skip(1).ToArray());
                case "spells": return Spells();
                case "fairness": return Fairness();
                case "balance": return Balance(args.Skip(1).ToArray());
                case "help": return Help();

                default:
                    Console.Error.WriteLine($"no command '{command}'.");
                    return Help();
            }
        }

        static int Help()
        {
            Console.WriteLine("sim locale [path]   scaffold the English locale csv");
            Console.WriteLine("sim spells          print the spell catalogue and its problems");
            Console.WriteLine("sim fairness        chi-squared every die");
            Console.WriteLine("sim balance [runs]  play fights and print the win tables");
            return 0;
        }


        // --- locale -------------------------------------------------------------------------

        static int Locale(string[] args)
        {
            string path = args.Length > 0 ? args[0] : Find("game/locale/game.csv");

            string existing = File.Exists(path) ? File.ReadAllText(path) : "";

            string drafted = English.Draft(existing);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, drafted);

            int rows = drafted.Split('\n').Count(l => l.Length > 0) - 1;

            Console.WriteLine($"wrote {rows} rows to {path}");

            IReadOnlyList<string> problems =
                Content.Schema.Locale.Audit(drafted, EngineKeys.Sorted());

            foreach (string problem in problems) Console.Error.WriteLine("  " + problem);

            Console.WriteLine(problems.Count == 0
                                  ? "every key the engine emits has English"
                                  : $"{problems.Count} keys still have nothing to say");

            return problems.Count == 0 ? 0 : 1;
        }

        // the sim runs from bin/, so walk up until the repo root is underfoot
        static string Find(string relative)
        {
            var here = new DirectoryInfo(AppContext.BaseDirectory);

            while (here != null)
            {
                string candidate = Path.Combine(here.FullName, relative);

                if (File.Exists(candidate) || Directory.Exists(Path.GetDirectoryName(candidate)))
                    return candidate;

                here = here.Parent;
            }

            return relative;
        }


        // --- spells -------------------------------------------------------------------------

        static int Spells()
        {
            SpellBook book = SpellBook.Srd();

            Console.WriteLine(book);

            foreach (string problem in book.Problems) Console.Error.WriteLine("  " + problem);

            Console.WriteLine();

            foreach (IGrouping<int, Spell> level in book.All.GroupBy(s => s.Level))
            {
                Console.WriteLine($"-- level {level.Key} ({level.Count()})");

                foreach (Spell spell in level) Console.WriteLine("   " + spell);
            }

            Console.WriteLine();
            Console.WriteLine("approximations, which the sim should watch:");

            foreach (Spell spell in book.Approximations)
                Console.WriteLine("   " + spell.Id + " - " +
                                  string.Join(" ", spell.Effects.Select(e => e.Note)
                                                        .Where(n => n.Length > 0)));

            return book.Sound ? 0 : 1;
        }


        // --- fairness -----------------------------------------------------------------------

        static int Fairness()
        {
            bool sound = true;

            foreach (Die die in new[] { Die.D4, Die.D6, Die.D8, Die.D10, Die.D12, Die.D20 })
            {
                var rng = new SeededRng(Environment.TickCount + die.Sides());
                var tally = new FaceTally(die.Sides());

                for (int i = 0; i < 200_000; i++) tally.Add(die.Roll(rng));

                foreach (string line in tally.DebugLines()) Console.WriteLine(line);

                Console.WriteLine();

                if (tally.Verdict == Core.Statistics.Fairness.Biased) sound = false;
            }

            return sound ? 0 : 1;
        }


        // --- balance ------------------------------------------------------------------------

        const string Arena = @"
+-+-+-+-+-+-+-+-+-+-+
|@ . . . . . . . . .|
+ + + + + + + + + + +
|. . . . . . . . . .|
+ + + + + + + + + + +
|. . . . . . . . . .|
+ + + + + + + + + + +
|. . . . . . . . . .|
+ + + + + + + + + + +
|. . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+";

        static int Balance(string[] args)
        {
            int runs = args.Length > 0 && int.TryParse(args[0], out int n) ? n : 2000;

            Console.WriteLine($"{runs} fights per row. a hero of each level against goblins.");
            Console.WriteLine();
            Console.WriteLine("level  foes   wins   rounds  hp left  downed");

            foreach (int level in new[] { 1, 3, 5, 8, 12, 17, 20 })
                foreach (int foes in new[] { 1, 2, 4 })
                    Row(level, foes, runs);

            return 0;
        }

        static void Row(int level, int foes, int runs)
        {
            int wins = 0;
            int rounds = 0;
            int hp = 0;
            int downed = 0;

            for (int run = 0; run < runs; run++)
            {
                Outcome outcome = Fight(level, foes, run, out int lasted, out int left,
                                        out bool wentDown);

                if (outcome == Outcome.HeroesWon) wins++;

                rounds += lasted;
                hp += left;

                if (wentDown) downed++;
            }

            Console.WriteLine($"{level,5}  {foes,4}  {100.0 * wins / runs,5:0.0}%  " +
                              $"{(double)rounds / runs,6:0.0}  {(double)hp / runs,7:0.0}  " +
                              $"{100.0 * downed / runs,5:0.0}%");
        }

        static Outcome Fight(int level, int foes, int seed, out int rounds, out int hpLeft,
                             out bool wentDown)
        {
            MapReader.TryRead(Arena, out MapLayout map, out _);

            var field = new Battlefield(map);
            var fight = new Encounter(new StandardResolver(new SeededRng(seed * 31 + level * 7 + foes)),
                                      field);

            Actor hero = Hero(level);

            fight.Enlist(hero, new Cell(0, 2));
            fight.ArmOpportunity(hero, Longsword(hero));

            var brains = new Dictionary<Actor, ITactics>();

            for (int i = 0; i < foes; i++)
            {
                Actor goblin = Goblin(i);

                fight.Enlist(goblin, new Cell(9, Math.Min(4, i)));
                fight.ArmOpportunity(goblin, Scimitar);

                brains[goblin] = new BasicTactics(new[] { Scimitar });
            }

            fight.Begin();

            wentDown = false;

            Turn turn;

            while ((turn = fight.Next()) != null && fight.Round <= 40)
            {
                if (turn.Actor.Side == Allegiance.Hero) Play(fight, turn, hero);
                else brains[turn.Actor].Take(fight, turn);

                if (hero.IsDown) wentDown = true;
            }

            rounds = fight.Round;
            hpLeft = Math.Max(0, hero.Health.Current);

            return fight.Judge();
        }

        // the hero the sim plays: walk to the nearest foe and swing until the actions are gone.
        // deliberately no cleverer than the monsters, so a win rate measures the numbers and not
        // the tactics.
        static void Play(Encounter fight, Turn turn, Actor hero)
        {
            Attack sword = Longsword(hero);

            Actor quarry = fight.Field.Enemies(hero)
                                .OrderBy(a => fight.Field.Distance(hero, a))
                                .ThenBy(a => a.Id, StringComparer.Ordinal)
                                .FirstOrDefault();

            if (quarry == null)
            {
                fight.EndTurn();
                return;
            }

            while (turn.Can(Spend.Action))
            {
                if (fight.Hit(turn, quarry, sword) != null) continue;

                Cell? there = fight.Field.Where(quarry);

                if (!there.HasValue) break;

                Cell? step = fight.Field.Reachable(hero, turn.SquaresLeft)
                                  .OrderBy(p => Battlefield.Distance(p.Key, there.Value))
                                  .ThenBy(p => p.Value)
                                  .Select(p => (Cell?)p.Key)
                                  .FirstOrDefault();

                if (!step.HasValue || fight.Walk(turn, step.Value).Count < 2) break;
            }

            fight.EndTurn();
        }

        static Actor Hero(int level)
        {
            var hero = new Actor("hero", level, new AbilityScores(17, 12, 15, 10, 13, 8),
                                 Allegiance.Hero);

            // a Fighter's d10, SRD hit points: the first die is maximum, the rest average
            int constitution = hero.AbilityModifier(Ability.Constitution);
            int hp = 10 + constitution + (level - 1) * (Die.D10.Average() + 1 + constitution);

            hero.SetHealth(new Health(hp, Die.D10, level));
            hero.Armor = new ArmorProfile(ArmorWeight.Heavy, 16);
            hero.HasShield = true;
            hero.TrainSave(Ability.Strength);
            hero.TrainSave(Ability.Constitution);

            // the one place the sim models growth: SRD's ASI levels, spent on the attack ability
            foreach (int asi in new[] { 4, 8, 12, 16, 19 })
                if (level >= asi)
                    hero.Scores.Raise(Ability.Strength, 2);

            return hero;
        }

        static Attack Longsword(Actor hero) =>
            new Attack("longsword", DiceRoll.Parse("1d8"), DamageType.Slashing);

        static readonly Attack Scimitar =
            new Attack("scimitar", DiceRoll.Parse("1d6"), DamageType.Slashing,
                       Ability.Dexterity, finesse: true);

        static Actor Goblin(int i)
        {
            var goblin = new Actor("goblin_" + i, 1, new AbilityScores(8, 14, 10, 10, 8, 8));

            goblin.SetHealth(new Health(7, Die.D6, 2));
            goblin.Armor = new ArmorProfile(ArmorWeight.Light, 13);
            goblin.HasShield = true;

            return goblin;
        }
    }
}
