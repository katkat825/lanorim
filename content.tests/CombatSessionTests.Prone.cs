using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Combat;
using Core.Resolution;

namespace Content.Tests
{
    // STAND UP STANDS THE MINI UP (cc_task_working-notes-10-01.md 1.2): "all the mechanics are there, but my
    // mini is still on its back". The rules cleared Prone without a word to anyone watching, so the board never
    // heard it. The board lays a piece down and stands it up on the fight's own events; these hold the events
    // to the rules. (The figure itself, upright and centred in its square, is checked in Godot: check-table.)
    public partial class CombatSessionTests
    {
        sealed class ProneWatch : CombatObserver
        {
            public List<(Actor Who, Condition What, bool On)> Heard { get; } = new();

            public List<Actor> Down { get; } = new();

            public override void ConditionChanged(Actor actor, Condition condition, bool applied) =>
                Heard.Add((actor, condition, applied));

            public override void Downed(Actor actor) => Down.Add(actor);
        }

        static ProneWatch Watching(CombatSession session)
        {
            var watch = new ProneWatch();
            ((Observers)session.Fight.Observer).Add(watch);
            return watch;
        }

        [Fact]
        public void StandUpTellsWhoeverIsWatching()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(8, 1) });
            ProneWatch watch = Watching(session);
            Actor hero = session.Hero.Actor;

            hero.Apply(Condition.Prone);

            ActionOption stand = session.Options().First(o => o.Kind == OptionKind.StandUp);
            Assert.True(session.Take(stand).Done);

            Assert.False(hero.Has(Condition.Prone));
            Assert.Contains((hero, Condition.Prone, false), watch.Heard);
        }

        [Fact]
        public void KnockedProneThenStandingUpIsHeardOnThenOff()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(1, 0) });
            ProneWatch watch = Watching(session);
            Actor goblin = session.Fight.Actors.First(a => a.Side != Allegiance.Hero);

            // the hero's Shove, as the More Actions menu takes it
            ActionOption shove = session.Options().First(o => o.Kind == OptionKind.Shove && o.ShoveMode == "prone");
            session.Select(shove);
            Assert.True(session.Confirm(goblin).Done);
            Assert.True(goblin.Has(Condition.Prone));
            Assert.Contains((goblin, Condition.Prone, true), watch.Heard);

            // the goblin's own turn: its tactics stand it up, through the fight, so it is heard too
            session.EndTurn();

            Assert.False(goblin.Has(Condition.Prone));
            Assert.Contains((goblin, Condition.Prone, false), watch.Heard);
        }

        // a hero at 0 is Unconscious and Prone (SRD 5.2.1) and is told as Downed; brought back by the death save,
        // it is still Prone until it stands, and standing is heard
        [Fact]
        public void AHeroBackFromTheFloorIsStillProneUntilItStands()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(8, 1) });
            ProneWatch watch = Watching(session);
            Actor hero = session.Hero.Actor;

            hero.Suffer(hero.Health.Current + 1, DamageType.Bludgeoning);
            Assert.True(hero.Has(Condition.Prone));

            Core.Rules.Checks.DeathSave(new StandardResolver(new Core.Dice.ScriptedRng(19)), hero);
            Assert.False(hero.IsDown);
            Assert.True(hero.Has(Condition.Prone));

            Assert.True(session.Fight.StandUp(session.Turn));
            Assert.Contains((hero, Condition.Prone, false), watch.Heard);
        }
    }
}
