using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Combat;
using Content.Dialogue;
using Content.Encounters;
using Content.Play;
using Content.Schema;
using Content.Sheet;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Core.Tables;

namespace Content.Tests
{
    // FAR FEWER ROLLS BEHIND THE SCREEN (cc_task_i Part 1): the screen rattles only for a secret the player hears -
    // an encounter table, a <<roll ... heard>> - and not more than once every few minutes of play. the enemies'
    // attacks, damage, saves and initiative are in the fight log, so they're not secrets
    public class RattleTests
    {
        static Request Ask(string command) => Request.Parse(command, out _);

        [Fact]
        public void ARollIsSilentUnlessTheAuthorSaysItIsHeard()
        {
            Assert.False(Ask("roll 1d20").Heard);
            Assert.True(Ask("roll 1d20 heard").Heard);

            Assert.Null(Request.Parse("roll 1d20 loud", out string problem));
            Assert.Contains("heard", problem);
        }

        [Fact]
        public void ASilentRollIsStillBehindTheScreenButNotHeard()
        {
            Library srd = Library.Srd();
            var referee = new Referee(SampleHero(srd), new StandardResolver(new SeededRng(1)), new GmScreen(new SeededRng(2)), srd);

            Settled quiet = referee.Settle(Ask("roll 1d20"));
            Settled heard = referee.Settle(Ask("roll 1d20 heard"));

            Assert.True(quiet.Roll.IsHidden);
            Assert.False(quiet.Heard);
            Assert.True(heard.Heard);
        }

        [Theory]
        [InlineData("", Visibility.Hidden, true)]
        [InlineData(@"""rolled"": ""quiet"",", Visibility.Quiet, false)]
        [InlineData(@"""rolled"": ""shown"",", Visibility.Shown, false)]
        public void AnEncounterTableIsHeardUnlessItIsQuietOrInTheOpen(string extra, Visibility read, bool heard)
        {
            string json = $@"{{ ""tables"": [ {{ ""id"": ""north_road"", {extra}
                               ""entries"": [ {{ ""id"": ""crows"", ""kind"": ""line"" }} ] }} ] }}";

            Assert.True(EncounterReader.TryRead(json, out IReadOnlyList<EncounterTable> tables, out var problems),
                        string.Join("; ", problems));
            Assert.Equal(read, tables.Single().Visibility);

            var settled = new Settled(Ask("encounter north_road"), new Answer())
            {
                Table = new GmScreen(new SeededRng(3)).Consult(tables.Single()),
            };

            Assert.Equal(heard, settled.Heard);
            Assert.Equal(read != Visibility.Shown, settled.Table.Rolls.All(r => r.IsHidden));
        }

        [Fact]
        public void TheCooldownLetsOneThroughEveryFewMinutes()
        {
            var cooldown = new Cooldown(CampaignRun.RattleSeconds);

            Assert.True(cooldown.Take(10));
            Assert.False(cooldown.Take(10 + CampaignRun.RattleSeconds - 1));
            Assert.True(cooldown.Take(10 + CampaignRun.RattleSeconds));
        }

        // the sample campaign has one encounter table, a silent weather roll, and three fights full of the GM's
        // dice: one secret is heard, and none of the fights' rolls are
        [Fact]
        public void TheSampleCampaignIsHeardOnceAndItsFightsNotAtAll()
        {
            Library srd = Library.Srd();
            Package pack = Package.Read(SampleCampaignTests.Folder);
            var rng = new SeededRng(3);
            Hero hero = SampleHero(srd);
            var resolver = new TableResolver(rng, new Digital(rng), a => ReferenceEquals(a, hero.Actor));
            var run = new CampaignRun(srd, pack, hero, resolver, rng);

            int heard = 0, behind = 0;
            run.Heard += () => heard++;
            resolver.Rolled += roll => { if (!roll.OnTheTable) behind++; };

            run.Start();

            for (int step = 0; step < 5000 && run.Now != Scene.Over && run.Now != Scene.Dead; step++)
            {
                if (run.Now == Scene.Line) run.Next();
                else if (run.Now == Scene.Choice) run.Choose(0);
                else if (run.Now == Scene.Shop) run.LeaveShop();
                else if (run.Now == Scene.Fight)
                {
                    Outcome outcome = new AutoPlayer().Play(new CombatSession(run.BattleFor(resolver), srd.Items, srd.Forms));
                    run.EndFight(outcome == Outcome.Open ? Outcome.Fled : outcome);
                }

                while (run.Hero.PendingImprovements > 0) run.Hero.ImproveAsSuggested();
            }

            Assert.Equal(1, heard);
            Assert.True(behind > heard, $"only {behind} rolls behind the screen - the fights should have made more than the secrets");
        }

        static Hero SampleHero(Library srd) => SampleCampaignTests.Make(srd, "fighter");
    }
}
