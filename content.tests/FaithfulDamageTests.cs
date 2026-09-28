using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // damage: and what rides on it - a later splash, a burning, a smite, a leap, extra dice (DamageHandler)
    public class FaithfulDamageTests : FaithfulSpellFixture
    {
        [Fact]
        public void VitriolicSphereSplashesAgainAtTheEndOfTheTargetsNextTurn()
        {
            Assert.True(Faithful("vitriolic_sphere"));

            // after initiative everything is a one: a failed save, 10d4 = 10, and 5d4 = 5 later
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 8);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("vitriolic_sphere"), Aim.On(new Cell(8, 2)), turn: mine,
                      fight: fight);

            Assert.Equal(90, goblin.Health.Current);

            fight.EndTurn();
            fight.Next();
            Assert.Equal(90, goblin.Health.Current);
            fight.EndTurn();

            Assert.Equal(85, goblin.Health.Current);
        }

        [Fact]
        public void DivineSmiteIsABonusActionRightAfterAMeleeHit()
        {
            Assert.True(Faithful("divine_smite"));

            // the hit (15), the sword's d8 (1), then the smite's 2d8 and 1d8 against the undead
            Encounter fight = Duel(Script(20, 1, 15, 1, 8, 8, 8), out Caster wizard, out Actor me,
                                   out Actor goblin, tags: "undead");
            var cast = new Incantation(fight.Resolver);

            fight.Arm(me, new SpellReaction(wizard, Book.Find("divine_smite"), cast));
            fight.ChooseReactionsWith(me, new Yes());

            Turn mine = fight.Next();
            var sword = new Attack("sword", new DiceRoll(1, Die.D8), DamageType.Slashing);
            fight.Hit(mine, goblin, sword);

            Assert.Equal(100 - 1 - 24, goblin.Health.Current);
            Assert.Equal(0, mine.BonusActions);
        }

        [Fact]
        public void TheDefaultChooserNeverSpendsASlotOnASmiteUnasked()
        {
            Encounter fight = Duel(Script(20, 1, 15, 1), out Caster wizard, out Actor me,
                                   out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            fight.Arm(me, new SpellReaction(wizard, Book.Find("divine_smite"), cast));
            fight.ChooseReactionsWith(me, ReactionChoosers.WhenItHelps);

            Turn mine = fight.Next();
            fight.Hit(mine, goblin,
                      new Attack("sword", new DiceRoll(1, Die.D8), DamageType.Slashing));

            Assert.Equal(1, mine.BonusActions);
        }

        [Fact]
        public void SearingSmiteBurnsAtTheStartOfEachTurnUntilASave()
        {
            Assert.True(Faithful("searing_smite"));

            // hit 15, sword 1, smite 1; the goblin's first turn: burn 1, save 1 (fails); its
            // second: burn 1, save 20 (ends it)
            Encounter fight = Duel(Script(20, 1, 15, 1, 1, 1, 1, 1, 20), out Caster wizard,
                                   out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            fight.Arm(me, new SpellReaction(wizard, Book.Find("searing_smite"), cast));
            fight.ChooseReactionsWith(me, new Yes());

            Turn mine = fight.Next();
            fight.Hit(mine, goblin,
                      new Attack("sword", new DiceRoll(1, Die.D8), DamageType.Slashing));
            Assert.Equal(98, goblin.Health.Current);

            fight.EndTurn();
            fight.Next();
            Assert.Equal(97, goblin.Health.Current);
            fight.EndTurn();

            fight.Next();
            fight.EndTurn();
            fight.Next();
            Assert.Equal(96, goblin.Health.Current);
            fight.EndTurn();

            fight.Next();
            fight.EndTurn();
            fight.Next();
            Assert.Equal(96, goblin.Health.Current);
        }

        [Fact]
        public void SearingSmitesSaveComesRightAfterTheBurnAndNotAtTheEndOfTheTurn()
        {
            // cc_task_dedupe-effects.md 3a #2: repeat_save and end_save are one save track now,
            // and a burning's track is the one kind that saves at the START of the turn, after the
            // burn. hit 15, sword 1, smite 1; the goblin's first turn: burn 1, save 1 (fails). the
            // next roll is a 6: a save at the end of that turn would take it and end the spell;
            // the right timing spends it on the second turn's burn
            Encounter fight = Duel(Script(20, 1, 15, 1, 1, 1, 1, 6, 1), out Caster wizard,
                                   out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            fight.Arm(me, new SpellReaction(wizard, Book.Find("searing_smite"), cast));
            fight.ChooseReactionsWith(me, new Yes());

            Turn mine = fight.Next();
            fight.Hit(mine, goblin,
                      new Attack("sword", new DiceRoll(1, Die.D8), DamageType.Slashing));
            fight.EndTurn();

            fight.Next();
            Assert.Equal(97, goblin.Health.Current);
            fight.EndTurn();

            fight.Next();
            fight.EndTurn();
            fight.Next();
            Assert.Equal(91, goblin.Health.Current);
        }

        [Fact]
        public void ProduceFlameLightsOnTheCastAndHurlsOnlyOnALaterAction()
        {
            Assert.True(Faithful("produce_flame"));

            // the cast (a bonus action) hurls nothing; the hurl (an action) hits on a 15 for 1
            Encounter fight = Duel(Script(20, 1, 15, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 8);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting flame = cast.Cast(wizard, Book.Find("produce_flame"), Aim.At(goblin),
                                      turn: mine, fight: fight);

            Assert.True(flame.Cast, flame.Refusal);
            Assert.Equal(0, flame.TotalDamage);
            Assert.Equal(0, mine.BonusActions);
            Assert.False(wizard.Actor.IsConcentrating);

            Casting hurl = cast.Again(wizard, Book.Find("produce_flame"), Aim.At(goblin), mine, fight);

            Assert.True(hurl.Cast, hurl.Refusal);
            Assert.True(goblin.Health.Current < 100);
        }

        [Fact]
        public void ChromaticOrbTakesTheChosenTypeAndLeapsOnMatchingDice()
        {
            Assert.True(Faithful("chromatic_orb"));

            // cast at level 2, so 4d8: hit (15), 2, 2, 5, 1 - a pair - leaps to the second goblin:
            // hit (15), 1, 2, 3, 4 - no pair, so no second leap even at level 2
            var cast = new Incantation(new StandardResolver(
                new ScriptedRng(15, 2, 2, 5, 1, 15, 1, 2, 3, 4, 15, 8, 8, 8, 8)));
            Caster wizard = Wizard(out _);
            Actor first = Goblin("first");
            Actor second = Goblin("second");
            Actor third = Goblin("third");
            third.SetDefense(DamageType.Cold, Defense.Immune);

            Casting refused = cast.Cast(wizard, Book.Find("chromatic_orb"), Aim.At(first));
            Assert.False(refused.Cast);

            Casting orb = cast.Cast(wizard, Book.Find("chromatic_orb"),
                                    Aim.At(first, second, third).Choosing(DamageType.Cold),
                                    castAt: 2);

            Assert.True(orb.Cast, orb.Refusal);
            Assert.Equal(100 - 10, first.Health.Current);
            Assert.Equal(100 - 10, second.Health.Current);
            Assert.Equal(100, third.Health.Current);
            Assert.Equal(2, orb.Landings.Count);
        }

        [Fact]
        public void CallLightningBoltsFallOnlyUnderTheCloudAndAStormAddsADie()
        {
            Assert.True(Faithful("call_lightning"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 8);
            fight.Setting.Add("storm");
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting bolt = cast.Cast(wizard, Book.Find("call_lightning"), Aim.On(new Cell(8, 2)),
                                     turn: mine, fight: fight);

            // ones everywhere: a failed save, 3d10 + the storm's 1d10 = 4
            Assert.True(bolt.Cast, bolt.Refusal);
            Assert.Equal(96, goblin.Health.Current);

            Assert.Single(cast.ZonesOf(wizard.Actor));
            Assert.Equal(new Cell(2, 2), cast.ZonesOf(wizard.Actor).Single().Centre);
        }

        [Fact]
        public void DissonantWhispersSendsAFailedSaveRunningOnItsReaction()
        {
            // SRD p.124
            Spell murmur = Book.Find("dissonant_whispers");
            Assert.False(murmur.Renamed);

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, murmur, Aim.At(goblin), turn: mine, fight: fight);

            Assert.Equal(97, goblin.Health.Current);
            Assert.True(fight.Field.Distance(me, goblin) > 1);
            Assert.Equal(0, fight.ReactionsLeft(goblin));
        }

        [Fact]
        public void DragonsBreathBreathesTheChosenElementOnLaterActions()
        {
            // SRD p.126: a bonus action to cast, then a Magic action to exhale each time
            Spell breath = Book.Find("dragons_breath");
            Assert.False(breath.Renamed);
            Assert.Equal(Spend.Bonus, breath.CastingTime);

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin);
            goblin.SetDefense(DamageType.Cold, Defense.Immune);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting gift = cast.Cast(wizard, breath, Aim.Nothing.Choosing(DamageType.Cold),
                                     turn: mine, fight: fight);
            Assert.True(gift.Cast, gift.Refusal);
            Assert.Equal(100, goblin.Health.Current);

            // the breath remembers the cold picked at the cast - and the goblin is immune to it
            Casting puff = cast.Again(wizard, breath, Aim.Toward(Facing.East), mine, fight);
            Assert.True(puff.Cast, puff.Refusal);
            Assert.Equal(100, goblin.Health.Current);
            Assert.Single(puff.Landings);
        }
    }
}
