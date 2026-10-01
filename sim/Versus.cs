using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Monsters;
using Content.Schema;
using Content.Sheet;
using Core.Dice;
using Core.Combat;
using Core.Resolution;
using Core.Space;

namespace Sim
{
    // ONE MONSTER, EVERY CLASS: sim versus <monster> [count] [runs] - every class at levels 1 to 5 against that many
    // of one statblock, on the sample campaign's cellar, played by the AutoPlayer as `sim classes` plays. For a
    // monster no sample fight has (the ghoul, cc_task_e-shop-species-and-ui-notes.md 1.1). Prints, never retunes
    public static class Versus
    {
        public static int Run(string[] args, Func<string, string> find)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("sim versus <monster> [count] [runs]");
                return 1;
            }

            Library library = Library.Srd();
            Monster monster = library.Bestiary.Find(args[0]);

            if (monster == null)
            {
                Console.Error.WriteLine($"no monster '{args[0]}' in the SRD bestiary");
                return 1;
            }

            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? Math.Max(1, c) : 1;
            int runs = args.Length > 2 && int.TryParse(args[2], out int r) ? r : 200;

            Package pack = Package.Read(Path.GetDirectoryName(find("campaigns/sample_millbrook/pack.json")));
            MapLayout map = pack.Maps["mill_cellar"];

            Console.WriteLine($"{runs} fights per row: one hero against {count} {monster.Id}, on the cellar.");
            Console.WriteLine();
            Console.WriteLine("class      lvl   wins   rounds  hp left");

            foreach (string cls in Classes.All)
                for (int level = 1; level <= 5; level++)
                {
                    int wins = 0, rounds = 0, hp = 0;

                    for (int run = 0; run < runs; run++)
                    {
                        Hero hero = Classes.Make(library, cls, level);
                        var resolver = new StandardResolver(new SeededRng(run * 7919 + level * 131 + cls.Length * 17));
                        var foes = Enumerable.Range(1, count)
                                             .Select(i => new Battle.Foe(monster, map.SpawnAt(i) ?? map.SpawnAt(1) ?? map.Start))
                                             .ToList();

                        Battle battle = Battle.Set(library, map, hero, foes, resolver);
                        var session = new CombatSession(battle, library.Items, library.Forms);

                        if (new AutoPlayer().Play(session) == Outcome.HeroesWon) wins++;

                        rounds += battle.Fight.Round;
                        hp += Math.Max(0, hero.Actor.Health.Current) * 100 / Math.Max(1, hero.Actor.Health.Maximum);
                    }

                    Console.WriteLine($"{cls,-10} {level,3}  {100.0 * wins / runs,5:0.0}%  {(double)rounds / runs,6:0.0}  " +
                                      $"{(double)hp / runs,6:0}%");
                }

            return 0;
        }
    }
}
