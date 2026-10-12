using System;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Inventory;
using Content.Items;
using Content.Play;
using Content.Schema;
using Content.Sheet;
using Core.Combat;
using Core.Dice;

namespace Sim
{
    // HOW OFTEN THE GM SCREEN RATTLES (cc_task_i Part 1): the sample campaign played to its end on the table's own
    // resolver, the hero's dice digital, counting the rolls made behind the screen (what rattled before task I) and the
    // secrets the run says were heard. Play time is estimated, not measured: a line 6 seconds, a choice 10, a
    // shop 90, a round of a fight 30. A sim has no clock, so the rattle cooldown is applied on that estimate.
    public static class Rattles
    {
        const double LineSeconds = 6, ChoiceSeconds = 10, ShopSeconds = 90, RoundSeconds = 30;

        public static int Run(string[] args, Func<string, string> find)
        {
            int runs = args.Length > 0 && int.TryParse(args[0], out int n) ? n : 20;
            string cls = args.Length > 1 ? args[1] : "fighter";

            Package pack = Package.Read(Path.GetDirectoryName(find("campaigns/sample_millbrook/pack.json")));
            Library library = Library.Srd();

            double behind = 0, heard = 0, rattled = 0, minutes = 0;

            for (int run = 0; run < runs; run++)
            {
                var rng = new SeededRng(run * 7919 + 3);
                Hero hero = Classes.Make(library, cls, 1);
                var resolver = new TableResolver(rng, new Digital(rng), a => ReferenceEquals(a, hero.Actor));
                var play = new CampaignRun(library, pack, hero, resolver, rng);

                double clock = 0;
                int count = 0;
                var gate = new Cooldown(CampaignRun.RattleSeconds);

                resolver.Rolled += roll => { if (!roll.OnTheTable) count++; };
                play.Heard += () =>
                {
                    heard++;
                    if (gate.Take(clock)) rattled++;
                };

                play.Start();

                for (int step = 0; step < 5000 && play.Now != Scene.Over && play.Now != Scene.Dead; step++)
                {
                    switch (play.Now)
                    {
                        case Scene.Line: clock += LineSeconds; play.Next(); break;
                        case Scene.Choice: clock += ChoiceSeconds; play.Choose(0); break;
                        case Scene.Shop: clock += ShopSeconds; play.LeaveShop(); break;
                        case Scene.Fight:
                            {
                                Battle battle = play.BattleFor(resolver);
                                var session = new CombatSession(battle, library.Items, library.Forms);
                                Outcome outcome = new AutoPlayer().Play(session);
                                clock += session.Fight.Round * RoundSeconds;
                                play.EndFight(outcome == Outcome.Open ? Outcome.Fled : outcome);
                                break;
                            }
                    }

                    while (play.Hero.PendingImprovements > 0) play.Hero.ImproveAsSuggested();
                }

                behind += count;
                minutes += clock / 60;
            }

            double hours = minutes / 60;

            Console.WriteLine($"the sample campaign as a {cls}, {runs} plays, about {minutes / runs:0} minutes each (estimated)");
            Console.WriteLine($"  rolls behind the screen (rattled before task I): {behind / runs:0.0} a play, {behind / hours:0.0} an hour");
            Console.WriteLine($"  secrets heard (encounter tables, <<roll heard>>):  {heard / runs:0.0} a play, {heard / hours:0.0} an hour");
            Console.WriteLine($"  rattles after the {CampaignRun.RattleSeconds:0}-second cooldown:      {rattled / runs:0.0} a play, {rattled / hours:0.0} an hour");

            return 0;
        }
    }
}
