using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Tables;

namespace Sim
{
    // IS THE MAGE TOO SQUISHY? (cc_task_ui-issues-9-30.md 5) - measured, never changed. A mage who knows
    // Mage Armor, at levels 1 to 3, against the sample campaign's four fights, played by the AutoPlayer:
    //
    //   pre-cast   Mage Armor cast before the fight (BeforeTheFight, a slot spent) or not
    //   hit die    the SRD's d6, or a d8 (+2 hit points at level 1 and +1 a level after: the averages)
    //   damage     every enemy's damage at 100%, 85% or 75%
    //
    // The last two are NOT rules and nothing in the game has them: here the d8 is a raised maximum on
    // the hero, and the lighter damage is the difference handed back the moment a hit lands (so a hit
    // that would no longer drop the mage doesn't). Kathleen decides whether either becomes a rule.
    //
    //   sim mage-balance [runs]
    public static class MageBalance
    {
        static readonly (string Table, string Entry)[] Fights =
        {
            ("set_pieces", "cellar_rats"), ("mill_road", "wolves"), ("mill_road", "bandits"), ("set_pieces", "goblin_camp"),
        };

        public static int Run(string[] args, Func<string, string> find)
        {
            int runs = args.Length > 0 && int.TryParse(args[0], out int n) ? n : 300;

            Package pack = Package.Read(Path.GetDirectoryName(find("campaigns/sample_millbrook/pack.json")));
            Library library = Library.Srd();

            Console.WriteLine($"{runs} fights per cell: a mage who knows Mage Armor, the AutoPlayer, the sample campaign's fights.");
            Console.WriteLine("win% / rounds / dropped% (the mage went to 0 hit points at least once).");
            Console.WriteLine();

            foreach (bool precast in new[] { false, true })
                foreach (bool d8 in new[] { false, true })
                    foreach (double damage in new[] { 1.0, 0.85, 0.75 })
                    {
                        Console.WriteLine($"## {(precast ? "Mage Armor before the fight" : "no pre-cast")}, " +
                                          $"hit die {(d8 ? "d8" : "d6")}, enemy damage {damage * 100:0}%");
                        Console.WriteLine($"lvl  {string.Join("  ", Fights.Select(f => $"{f.Entry,-20}"))}  all");

                        for (int level = 1; level <= 3; level++)
                        {
                            var cells = new List<(int Wins, int Rounds, int Dropped)>();

                            foreach ((string table, string entry) in Fights)
                            {
                                EncounterEntry fight = pack.Encounter(table).Entries.Single(e => e.Id == entry);
                                cells.Add(Play(library, pack, fight, level, runs, precast, d8, damage));
                            }

                            string row = string.Join("  ", cells.Select(c => Cell(c, runs)));
                            var all = (cells.Sum(c => c.Wins), cells.Sum(c => c.Rounds), cells.Sum(c => c.Dropped));
                            Console.WriteLine($"{level,3}  {row}  {Cell(all, runs * cells.Count)}");
                        }

                        Console.WriteLine();
                    }

            return 0;
        }

        static string Cell((int Wins, int Rounds, int Dropped) c, int runs) =>
            $"{100.0 * c.Wins / runs,4:0}% {(double)c.Rounds / runs,4:0.0}r {100.0 * c.Dropped / runs,3:0}%d".PadRight(20);

        static (int Wins, int Rounds, int Dropped) Play(Library library, Package pack, EncounterEntry fight, int level,
                                                        int runs, bool precast, bool d8, double damage)
        {
            int wins = 0, rounds = 0, dropped = 0;

            for (int run = 0; run < runs; run++)
            {
                Hero hero = Mage(library, level);

                if (d8)
                {
                    int more = 2 + (level - 1);
                    hero.Actor.Boons.Add(Boon.Of(new BoonSpec { Duration = Duration.Permanent }, "sim_d8", "sim", null, more));
                    hero.Actor.Mend(more);
                }

                var resolver = new StandardResolver(new SeededRng(run * 7919 + level * 131 + fight.Id.Length * 17));

                if (precast) BeforeTheFight.Cast(hero, library.Spells.Find("mage_armor"), resolver, out _);

                var watch = new Watch(hero.Actor, damage);
                Battle battle = Battle.From(library, pack.Maps[fight.Map], hero, fight, resolver, null, null, watch);
                var session = new CombatSession(battle, library.Items, library.Forms);

                if (new AutoPlayer().Play(session) == Outcome.HeroesWon) wins++;

                rounds += battle.Fight.Round;
                if (watch.Dropped) dropped++;
            }

            return (wins, rounds, dropped);
        }

        // the mage creation makes, with Mage Armor among the spells
        static Hero Mage(Library library, int level)
        {
            var making = new Content.Creation.Creation(library, library.Backgrounds);

            making.StartAt(level);
            making.Pick(library.Class("mage"));
            making.Pick(library.Kind("human"));
            making.Pick(library.Background("soldier"));

            foreach (Skill skill in making.SkillChoices.Take(making.SkillPicksLeft).ToList()) making.Train(skill);
            foreach (Skill skill in making.Skills.Take(making.ExpertisePicksLeft).ToList()) making.Master(skill);

            making.Learn(library.Spells.Find("mage_armor"));

            foreach (Core.Magic.Spell spell in making.SpellChoices.ToList())
                if (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0) making.Learn(spell);

            making.Call("Sim");

            return making.Finish() ?? throw new InvalidOperationException(string.Join("; ", making.Problems));
        }

        // hands back the part of an enemy's hit the lighter damage wouldn't have done, and notes a drop
        sealed class Watch : CombatObserver
        {
            readonly Actor _mage;
            readonly double _damage;

            public Watch(Actor mage, double damage)
            {
                _mage = mage;
                _damage = damage;
            }

            public bool Dropped { get; private set; }

            public override void Dealt(Harm harm)
            {
                if (_damage >= 1.0 || !ReferenceEquals(harm.Target, _mage) || harm.By == null || harm.By.Side == _mage.Side) return;

                int before = _mage.Health.Current + harm.Suffered;
                int lighter = (int)Math.Ceiling(harm.Rolled * _damage);
                int left = Math.Max(0, before - lighter);

                if (left > _mage.Health.Current) _mage.Mend(left - _mage.Health.Current);
            }

            public override void Downed(Actor actor)
            {
                if (ReferenceEquals(actor, _mage) && _mage.IsDown) Dropped = true;
            }
        }
    }
}
