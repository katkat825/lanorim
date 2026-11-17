using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Creation;
using Content.Play;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Content.Tests
{
    // THE FEAST PLAYED THROUGH (cc_task_tf Part 3.4), the way a person picks: both ways in (the race, and the kitchens), a
    // fight lost in a campaign whose defeat continues, and the village loop - the inn and the farm twice each, the mill
    // once, after which it isn't offered. Every choice is picked by its line id; every fight's outcome is the test's
    public partial class FeastCampaignTests
    {
        sealed class Player
        {
            readonly CampaignRun _run;
            readonly Queue<string> _picks;
            readonly Func<string, Outcome> _fights;

            public readonly List<(string Node, string Map)> Scenes = new();
            public readonly List<string> Fought = new();
            public readonly List<IReadOnlyList<string>> HubOffers = new();

            public Player(CampaignRun run, IEnumerable<string> picks, Func<string, Outcome> fights)
            {
                _run = run;
                _picks = new Queue<string>(picks);
                _fights = fights;
            }

            public Scene Play(string start = null)
            {
                Scene now = _run.Start(start);

                for (int step = 0; step < 4000 && now != Scene.Over && now != Scene.Dead; step++)
                {
                    string node = _run.Talk.Node ?? "";
                    if (Scenes.Count == 0 || Scenes[^1].Node != node || Scenes[^1].Map != _run.MapId) Scenes.Add((node, _run.MapId));

                    now = now switch
                    {
                        Scene.Line => _run.Next(),
                        Scene.Choice => _run.Choose(Pick(node)),
                        Scene.Fight => Fight(),
                        Scene.Shop => _run.LeaveShop(),
                        Scene.Tip => _run.TipClosed(),
                        _ => throw new InvalidOperationException($"{now} at {node}"),
                    };
                }

                return now;
            }

            Scene Fight()
            {
                string id = _run.Fight.Entry.Id;
                Fought.Add(id);
                Scenes.Add(("fight:" + id, _run.Fight.MapId));
                return _run.EndFight(_fights(id));
            }

            int Pick(string node)
            {
                var options = _run.Talk.Choosing;

                if (node == "village_hub")
                    HubOffers.Add(options.Where(o => o.Offered).Select(o => o.Key.Split('.')[^1]).ToList());

                if (_picks.Count > 0)
                {
                    string wanted = _picks.Peek();
                    int at = options.ToList().FindIndex(o => o.Offered && o.Key.EndsWith(wanted, StringComparison.Ordinal));

                    if (at >= 0)
                    {
                        _picks.Dequeue();
                        return at;
                    }
                }

                return options.ToList().FindIndex(o => o.Offered);
            }

            public bool Picked => _picks.Count == 0;
        }

        static CampaignRun FeastRun(int seed, bool passEverything)
        {
            Hero hero = QuickHero.Make(Srd, "fighter", 1, "Tess");
            hero.ShopsFirst = false;

            var run = new CampaignRun(Srd, SampleCampaignTests.Sound(Folder), hero, new StandardResolver(new SeededRng(seed)),
                                      new SeededRng(seed));

            // the race is won on two passed checks of four; "always pass" makes the guest's way sure (WorkshopTest's rule)
            if (passEverything) run.Referee.Judge = a => a.Failed ? a.Rejudged(Math.Min(a.Against, a.Total)) : a;

            return run;
        }

        [Fact]
        public void TheRaceWayWithTheVillageLoopAndALostBearFight()
        {
            CampaignRun run = FeastRun(3, passEverything: true);

            var player = new Player(run, new[]
            {
                "plan_opt_race",
                "village_opt_inn", "village_inn_opt_later",
                "village_opt_farm", "village_farm_opt_later",
                "village_opt_mill",
                "village_opt_inn", "village_inn_opt_later",
                "village_opt_farm", "village_farm_opt_bear", "bear_opt_wait",
                "village_opt_skip",
            }, fight => fight == "pond_bear" ? Outcome.HeroesLost : Outcome.HeroesWon);

            Assert.Equal(Scene.Over, player.Play());
            Assert.True(player.Picked, "every planned choice was offered");

            // a lost fight goes on: the hero came round and the story carried on to the end
            Assert.Contains("pond_bear", player.Fought);
            Assert.False(run.Hero.Actor.IsDead);

            // the guest's way, to the end
            string[] nodes = player.Scenes.Select(s => s.Node).ToArray();
            Assert.Contains("the_race", nodes);
            Assert.Contains("banquet_guest", nodes);
            Assert.Contains("storehouse", nodes);
            Assert.Contains("the_feast_green", nodes);

            // the mill is offered until it's been visited, and never after
            Assert.Contains(player.HubOffers.First(), o => o == "village_opt_mill");
            int visited = player.Scenes.FindIndex(s => s.Node == "village_mill");
            Assert.True(visited > 0);
            Assert.All(player.HubOffers.Skip(3), offers => Assert.DoesNotContain("village_opt_mill", offers));
            Assert.True(player.Scenes.Count(s => s.Node == "village_farm") >= 2, "the farm twice");
            Assert.True(player.Scenes.Count(s => s.Node == "village_inn") >= 2, "the inn twice");

            // the board follows the story: each scene's <<map>>, a fight's own map while it's fought, and the scene after a
            // fight keeps that map until the next <<map>>
            Assert.Contains(player.Scenes, s => s.Node == "briarwood_inn" && s.Map == "briarwood_inn");
            Assert.Contains(player.Scenes, s => s.Node == "mill_yard" && s.Map == "mill_yard");
            Assert.Contains(player.Scenes, s => s.Node == "fight:pond_bear" && s.Map == "briarwood_pond");
            Assert.Contains(player.Scenes, s => s.Node == "bear_after" && s.Map == "briarwood_pond");
            Assert.Contains(player.Scenes, s => s.Node == "village_hub" && s.Map == "briarwood");
            Assert.Contains(player.Scenes, s => s.Node == "banquet_hall" && s.Map == "great_hall");
            Assert.Contains(player.Scenes, s => s.Node == "the_feast_green" && s.Map == "village_green");
        }

        [Fact]
        public void TheKitchenWay()
        {
            CampaignRun run = FeastRun(5, passEverything: false);

            var player = new Player(run, new[] { "plan_opt_kitchen", "village_opt_skip", "yard_opt_attack" }, _ => Outcome.HeroesWon);

            Assert.Equal(Scene.Over, player.Play());

            string[] nodes = player.Scenes.Select(s => s.Node).ToArray();
            Assert.Contains("celebration_begins", nodes);
            Assert.DoesNotContain("the_race", nodes);
            Assert.Contains("banquet_kitchen", nodes);
            Assert.Contains("storehouse", nodes);
            Assert.Contains("kitchen_hound", player.Fought);
            Assert.Contains(player.Scenes, s => s.Node == "fight:kitchen_hound" && s.Map == "kitchen_yard");
            Assert.Contains("the_feast_green", nodes);
        }
    }
}
