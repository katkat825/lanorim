using System.Collections.Generic;
using System.Linq;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Tests
{
    // the spells the unattended run of 2026-09-24 made faithful, held to what SRD 5.2.1 says they
    // do - and the new conditions they needed, held to the SRD's core effects
    public class FaithfulSpellTests
    {
        static readonly SpellBook Book = SpellBook.Srd();

        // eleven by five, all floor
        const string Hall = @"
+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+";

        // a script: the two initiative rolls, then whatever else, then ones for ever after
        static IRng Script(params int[] rolls) =>
            new ScriptedRng(rolls.Concat(Enumerable.Repeat(1, 200)).ToArray());

        static Encounter Field(IRng rng)
        {
            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);

            return new Encounter(new StandardResolver(rng), new Battlefield(map), new CombatLog());
        }

        static Caster Wizard(out Actor actor, int level = 17)
        {
            actor = new Actor("wizard", level, new AbilityScores(10, 10, 14, 18, 10, 10),
                              Allegiance.Hero);
            actor.SetHealth(new Health(80, Die.D6, level));

            var caster = new Caster(actor, Ability.Intelligence,
                                    SpellSlots.For(CasterProgression.Full, level));

            foreach (Spell spell in Book.All) caster.Learn(spell);

            return caster;
        }

        static Actor Goblin(string id = "goblin", int hp = 100, params string[] tags)
        {
            var goblin = new Actor(id, 1, new AbilityScores());
            goblin.SetHealth(new Health(hp));
            goblin.Armor = new ArmorProfile(ArmorWeight.Heavy, 10);

            foreach (string tag in tags) goblin.Tag(tag);

            return goblin;
        }

        // the wizard at (2,2) going first, the goblin beside it at (3,2) going second
        static Encounter Duel(IRng rng, out Caster wizard, out Actor me, out Actor goblin,
                              int x = 3, params string[] tags)
        {
            Encounter fight = Field(rng);
            wizard = Wizard(out me);
            goblin = Goblin(tags: tags);

            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(goblin, new Cell(x, 2));
            fight.Begin();

            return fight;
        }

        static bool Faithful(string id) => !Book.Find(id).Approximated;


        // --- the conditions ---------------------------------------------------------------------

        [Fact]
        public void TheNewConditionsLoadByName()
        {
            foreach (string id in new[] { "blinded", "charmed", "deafened", "incapacitated",
                                          "invisible", "paralyzed", "petrified" })
                Assert.True(Conditions.TryParse(id, out _), id);
        }

        [Fact]
        public void AHitFromBesideAParalyzedCreatureIsACriticalAndFromAfarItIsNot()
        {
            var resolver = new StandardResolver(new ScriptedRng(15));
            var attacker = new Actor("attacker", 1, new AbilityScores(), Allegiance.Hero);
            Actor target = Goblin();
            target.Apply(Condition.Paralyzed);

            var sword = new Attack("sword", new DiceRoll(1, Die.D8), DamageType.Slashing);

            Assert.True(Strike.Roll(resolver, attacker, target, sword, close: true).IsCritical);
            Assert.False(Strike.Roll(resolver, attacker, target, sword, close: false).IsCritical);
        }

        [Fact]
        public void ParalyzedAndStunnedCreaturesAreAttackedWithAdvantageAndFailStrengthSaves()
        {
            foreach (Condition condition in new[] { Condition.Paralyzed, Condition.Stunned,
                                                    Condition.Petrified, Condition.Unconscious })
            {
                Actor target = Goblin();
                target.Apply(condition);

                Assert.Equal(Advantage.Advantage, target.AdvantageAgainstMe);
                Assert.True(target.IsIncapacitated, condition.Id());

                Attempt save = Checks.Save(new StandardResolver(new ScriptedRng(20)), target,
                                           Ability.Strength, 5);
                Assert.False(save.Succeeded, condition.Id());
            }
        }

        [Fact]
        public void ProneIsAdvantageFromBesideAndDisadvantageFromAfar()
        {
            var attacker = new Actor("attacker", 1, new AbilityScores(), Allegiance.Hero);
            Actor target = Goblin();
            target.Apply(Condition.Prone);

            Assert.Equal(Advantage.Advantage, Strike.Lean(attacker, target, close: true));
            Assert.Equal(Advantage.Disadvantage, Strike.Lean(attacker, target, close: false));
        }

        [Fact]
        public void BlindedAndInvisibleAreTheSightRule()
        {
            var attacker = new Actor("attacker", 1, new AbilityScores(), Allegiance.Hero);
            Actor target = Goblin();

            target.Apply(Condition.Invisible);
            Assert.Equal(Advantage.Disadvantage, Strike.Lean(attacker, target));
            Assert.Equal(Advantage.Advantage, Strike.Lean(target, attacker));

            target.Remove(Condition.Invisible);
            attacker.Apply(Condition.Blinded);
            Assert.Equal(Advantage.Disadvantage, Strike.Lean(attacker, target));
            Assert.Equal(Advantage.Advantage, Strike.Lean(target, attacker));
        }

        [Fact]
        public void AdvantageAndDisadvantageCancelHoweverManySourcesEachSideHas()
        {
            // a hidden, poisoned attacker (advantage, disadvantage) at a restrained target
            // (advantage): SRD says flat, however the sources stack up
            var attacker = new Actor("attacker", 1, new AbilityScores(), Allegiance.Hero);
            attacker.Boons.Add(new Boon("hidden", duration: Duration.Encounter,
                                        advantageOnAttacks: true));
            attacker.Apply(Condition.Poisoned);

            Actor target = Goblin();
            target.Apply(Condition.Restrained);

            Assert.True(Strike.Lean(attacker, target).IsFlat());
            Assert.Equal(1, Strike.Lean(attacker, target).Dice());
        }

        [Fact]
        public void PetrifiedResistsEverythingAndCannotBePoisoned()
        {
            Actor statue = Goblin();
            statue.Apply(Condition.Poisoned);
            statue.Apply(Condition.Petrified);

            Assert.False(statue.Has(Condition.Poisoned));
            Assert.False(statue.Apply(Condition.Poisoned));
            Assert.Equal(5, statue.Suffer(10, DamageType.Fire));
        }

        [Fact]
        public void UnconsciousComesWithProneAndProneStaysWhenItEnds()
        {
            Actor sleeper = Goblin();
            sleeper.Apply(Condition.Unconscious);

            Assert.True(sleeper.Has(Condition.Prone));

            sleeper.Remove(Condition.Unconscious);

            Assert.True(sleeper.Has(Condition.Prone));
        }

        [Fact]
        public void AStatblocksConditionImmunityKeepsTheConditionOff()
        {
            Actor skeleton = Goblin("skeleton");
            skeleton.MakeImmune(Condition.Poisoned);

            Assert.False(skeleton.Apply(Condition.Poisoned));
        }

        [Fact]
        public void IncapacitatedBreaksConcentration()
        {
            Caster wizard = Wizard(out Actor me);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            cast.Cast(wizard, Book.Find("bless"), Aim.At(me));
            Assert.True(me.IsConcentrating);

            me.Apply(Condition.Incapacitated);
            cast.Check(new[] { me });

            Assert.False(me.IsConcentrating);
        }


        // --- Hold Person, Hold Monster --------------------------------------------------------

        [Fact]
        public void HoldPersonParalyzesAHumanoidAndLeavesAnythingElseAlone()
        {
            Assert.True(Faithful("hold_person"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor bandit = Goblin("bandit", tags: "humanoid");
            Actor wolf = Goblin("wolf", tags: "beast");

            cast.Cast(wizard, Book.Find("hold_person"), Aim.At(bandit, wolf), castAt: 3);

            Assert.True(bandit.Has(Condition.Paralyzed));
            Assert.False(wolf.Has(Condition.Paralyzed));
        }

        [Fact]
        public void HoldMonsterIsSavedOffAtTheEndOfTheTargetsTurn()
        {
            Assert.True(Faithful("hold_monster"));

            // initiative 20 and 1; the goblin fails the first save and makes the second
            Encounter fight = Duel(Script(20, 1, 1, 20), out Caster wizard, out _, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("hold_monster"), Aim.At(goblin), turn: mine, fight: fight);
            Assert.True(goblin.Has(Condition.Paralyzed));

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            Assert.False(goblin.Has(Condition.Paralyzed));
        }


        // --- Sleep -------------------------------------------------------------------------------

        [Fact]
        public void SleepIncapacitatesThenPutsToSleepOnASecondFailure()
        {
            Assert.True(Faithful("sleep"));

            Encounter fight = Duel(Script(20, 1, 1, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting sleep = cast.Cast(wizard, Book.Find("sleep"), Aim.On(new Cell(6, 2)),
                                      turn: mine, fight: fight);

            Assert.True(sleep.Cast, sleep.Refusal);
            Assert.True(goblin.Has(Condition.Incapacitated));
            Assert.False(goblin.Has(Condition.Unconscious));

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            Assert.True(goblin.Has(Condition.Unconscious));
            Assert.True(goblin.Has(Condition.Prone));
            Assert.False(goblin.Has(Condition.Incapacitated));
        }

        [Fact]
        public void SleepEndsOnDamageAndHealingDoesNotEndIt()
        {
            Encounter fight = Duel(Script(20, 1, 1, 1), out Caster wizard, out Actor me,
                                   out Actor goblin, x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("sleep"), Aim.On(new Cell(6, 2)), turn: mine, fight: fight);

            goblin.Mend(5);
            Assert.True(goblin.Has(Condition.Incapacitated));

            goblin.Suffer(1, DamageType.Slashing);
            fight.Hurt(me, goblin, 1);

            Assert.False(goblin.Has(Condition.Incapacitated));
        }

        [Fact]
        public void ASleeperCanBeShakenAwakeByAnActionFromBesideIt()
        {
            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out _, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("sleep"), Aim.On(new Cell(3, 2)), turn: mine, fight: fight);

            Assert.True(goblin.Has(Condition.Incapacitated));
            Assert.True(cast.CanBeShaken(goblin));

            Assert.True(cast.Shake(fight, mine, goblin));
            Assert.False(goblin.Has(Condition.Incapacitated));
        }

        [Fact]
        public void SleepSparesTheCastersSideAndTheSleepless()
        {
            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            var friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(10));
            Actor elf = Goblin("elf", tags: "sleepless");
            Actor goblin = Goblin();

            cast.Cast(wizard, Book.Find("sleep"), Aim.At(friend, elf, goblin));

            Assert.False(friend.Has(Condition.Incapacitated));
            Assert.False(elf.Has(Condition.Incapacitated));
            Assert.True(goblin.Has(Condition.Incapacitated));
        }


        // --- Hideous Laughter, Hypnotic Pattern, Charm Person ----------------------------------

        [Fact]
        public void HideousLaughterPinsTheTargetProneAndDamageCallsASaveWithAdvantage()
        {
            Assert.True(Faithful("hideous_laughter"));

            // the first save fails; the damage-triggered one rolls twice with advantage: 20, 20
            Encounter fight = Duel(Script(20, 1, 1, 20, 20), out Caster wizard, out Actor me,
                                   out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("hideous_laughter"), Aim.At(goblin), turn: mine,
                      fight: fight);

            Assert.True(goblin.Has(Condition.Prone));
            Assert.True(goblin.Has(Condition.Incapacitated));
            Assert.False(new Turn(goblin, new ActionBudget(), 1).StandUp());

            goblin.Suffer(1, DamageType.Slashing);
            fight.Hurt(me, goblin, 1);

            Assert.False(goblin.Has(Condition.Incapacitated));
            Assert.False(goblin.Has(Condition.Prone));
        }

        [Fact]
        public void HypnoticPatternCharmsIncapacitatesAndStopsUntilHurt()
        {
            Assert.True(Faithful("hypnotic_pattern"));

            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out Actor me,
                                   out Actor goblin, x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("hypnotic_pattern"), Aim.On(new Cell(6, 2)), turn: mine,
                      fight: fight);

            Assert.True(goblin.Has(Condition.Charmed));
            Assert.True(goblin.Has(Condition.Incapacitated));
            Assert.Equal(0, goblin.Moves);

            goblin.Suffer(1, DamageType.Slashing);
            fight.Hurt(me, goblin, 1);

            Assert.False(goblin.Has(Condition.Charmed));
            Assert.False(goblin.Has(Condition.Incapacitated));
            Assert.True(goblin.Moves > 0);
        }

        [Fact]
        public void ACharmedCreatureCannotAttackItsCharmerAndTheCharmBreaksOnTheCastersDamage()
        {
            Assert.True(Faithful("charm_person"));

            // the goblin saves with advantage because it is being fought: two ones
            Encounter fight = Duel(Script(20, 1, 1, 1), out Caster wizard, out Actor me,
                                   out Actor goblin, tags: "humanoid");
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("charm_person"), Aim.At(goblin), turn: mine, fight: fight);

            Assert.True(goblin.HasFrom(Condition.Charmed, me));

            fight.EndTurn();
            Turn theirs = fight.Next();

            var claw = new Attack("claw", new DiceRoll(1, Die.D4), DamageType.Slashing);
            Assert.Null(fight.Hit(theirs, me, claw));

            // somebody on the goblin's own side hurting it changes nothing; the wizard does
            fight.Hurt(Goblin("other"), goblin, 1);
            Assert.True(goblin.Has(Condition.Charmed));

            fight.Hurt(me, goblin, 1);
            Assert.False(goblin.Has(Condition.Charmed));
        }


        // --- Flesh to Stone, Power Word Stun -----------------------------------------------------

        [Fact]
        public void FleshToStonePetrifiesOnTheThirdFailure()
        {
            Assert.True(Faithful("flesh_to_stone"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _,
                                   out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("flesh_to_stone"), Aim.At(goblin), turn: mine,
                      fight: fight);
            Assert.True(goblin.Has(Condition.Restrained));

            // the goblin's turn: its second failed save (1)
            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            Assert.True(goblin.Has(Condition.Restrained));

            // a round later: the third
            fight.Next();
            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            Assert.True(goblin.Has(Condition.Petrified));
            Assert.False(goblin.Has(Condition.Restrained));
        }

        [Fact]
        public void FleshToStoneOnASuccessOrAConstructOnlyStopsItMoving()
        {
            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor golem = Goblin("golem", tags: "construct");

            cast.Cast(wizard, Book.Find("flesh_to_stone"), Aim.At(golem));

            Assert.False(golem.Has(Condition.Restrained));
            Assert.Equal(0, golem.Moves);
        }

        [Fact]
        public void PowerWordStunStunsAtOneHundredFiftyAndSlowsAnythingTougher()
        {
            Assert.True(Faithful("power_word_stun"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor weak = Goblin("weak", hp: 150);
            Actor strong = Goblin("strong", hp: 151);

            cast.Cast(wizard, Book.Find("power_word_stun"), Aim.At(weak));
            cast.Cast(wizard, Book.Find("power_word_stun"), Aim.At(strong));

            Assert.True(weak.Has(Condition.Stunned));
            Assert.False(strong.Has(Condition.Stunned));
            Assert.Equal(0, strong.Moves);
        }


        // --- Aid, Death Ward, Raise Dead, True Seeing --------------------------------------------

        [Fact]
        public void AidRaisesTheMaximumAndTheCurrentAndLastsUntilALongRest()
        {
            Assert.True(Faithful("aid"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor friend = Goblin("friend", hp: 20);
            friend.Suffer(10, DamageType.Slashing);

            cast.Cast(wizard, Book.Find("aid"), Aim.At(friend), castAt: 3);

            Assert.Equal(30, friend.Health.Maximum);
            Assert.Equal(20, friend.Health.Current);

            friend.ShortRest();
            Assert.Equal(30, friend.Health.Maximum);

            friend.LongRest();
            Assert.Equal(20, friend.Health.Maximum);
            Assert.Equal(20, friend.Health.Current);
        }

        [Fact]
        public void DeathWardTurnsTheFirstDropToZeroIntoOneAndStopsAPowerWordKill()
        {
            Assert.True(Faithful("death_ward"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor friend = Goblin("friend", hp: 30);
            cast.Cast(wizard, Book.Find("death_ward"), Aim.At(friend));

            friend.Suffer(999, DamageType.Necrotic);
            Assert.Equal(1, friend.Health.Current);

            friend.Suffer(999, DamageType.Necrotic);
            Assert.True(friend.IsDown);

            Actor other = Goblin("other", hp: 50);
            cast.Cast(wizard, Book.Find("death_ward"), Aim.At(other));
            cast.Cast(wizard, Book.Find("power_word_kill"), Aim.At(other));

            Assert.Equal(50, other.Health.Current);
            Assert.Null(other.Boons.DeathWard);
        }

        [Fact]
        public void RaiseDeadBringsTheDeadBackWithAPenaltyThatEasesEachLongRest()
        {
            Assert.True(Faithful("raise_dead"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            var friend = new Actor("friend", 5, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(30));
            friend.Perish();

            cast.Cast(wizard, Book.Find("raise_dead"), Aim.At(friend));

            Assert.False(friend.IsDead);
            Assert.Equal(1, friend.Health.Current);
            Assert.Equal(-4, friend.Boons.FlatOnSave(Ability.Wisdom));

            friend.LongRest();
            Assert.Equal(-3, friend.Boons.FlatOnSave(Ability.Wisdom));

            friend.LongRest();
            friend.LongRest();
            friend.LongRest();
            Assert.Equal(0, friend.Boons.FlatOnSave(Ability.Wisdom));
        }

        [Fact]
        public void TrueSeeingSeesTheInvisible()
        {
            Assert.True(Faithful("true_seeing"));

            Caster wizard = Wizard(out Actor me);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor ghost = Goblin("ghost");
            ghost.Apply(Condition.Invisible);

            Assert.False(me.CanSee(ghost));

            cast.Cast(wizard, Book.Find("true_seeing"), Aim.At(me));

            Assert.True(me.CanSee(ghost));
        }


        // --- Sunbeam, Blindness/Deafness, Vitriolic Sphere, Black Tentacles ----------------------

        [Fact]
        public void SunbeamBlindsUntilTheStartOfTheCastersNextTurn()
        {
            Assert.True(Faithful("sunbeam"));

            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting beam = cast.Cast(wizard, Book.Find("sunbeam"), Aim.Toward(Facing.East),
                                     turn: mine, fight: fight);

            Assert.True(beam.Cast, beam.Refusal);
            Assert.True(goblin.Has(Condition.Blinded));

            fight.EndTurn();
            fight.Next();
            Assert.True(goblin.Has(Condition.Blinded));
            fight.EndTurn();

            fight.Next();
            Assert.False(goblin.Has(Condition.Blinded));
        }

        [Fact]
        public void BlindnessDeafnessAsksWhichAndBlindsOnBlindness()
        {
            Assert.True(Faithful("blindness_deafness"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));
            Actor goblin = Goblin();

            Casting refused = cast.Cast(wizard, Book.Find("blindness_deafness"), Aim.At(goblin));
            Assert.False(refused.Cast);

            cast.Cast(wizard, Book.Find("blindness_deafness"), Aim.At(goblin).Choosing("deafness"));
            Assert.True(goblin.Has(Condition.Deafened));
            Assert.False(goblin.Has(Condition.Blinded));

            Actor other = Goblin("other");
            cast.Cast(wizard, Book.Find("blindness_deafness"), Aim.At(other).Choosing("blindness"));
            Assert.True(other.Has(Condition.Blinded));
        }

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
        public void BlackTentaclesGrabWhoeverIsThereWhenTheyAppear()
        {
            Assert.True(Faithful("black_tentacles"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 7);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("black_tentacles"), Aim.On(new Cell(7, 2)), turn: mine,
                      fight: fight);

            Assert.True(goblin.Has(Condition.Restrained));
            Assert.Equal(97, goblin.Health.Current);
        }


        // --- the second batch: decoys, turn limits, rewrites, clouds, smites ---------------------

        [Fact]
        public void MirrorImageDuplicatesTakeHitsOnAThreeOrBetter()
        {
            Assert.True(Faithful("mirror_image"));

            // a hit (15) against the wizard rolls three d6 - a 3 among them
            Caster wizard = Wizard(out Actor me);
            var resolver = new StandardResolver(new ScriptedRng(15, 1, 1, 3));
            var cast = new Incantation(resolver);

            cast.Cast(wizard, Book.Find("mirror_image"), Aim.Nothing);
            Assert.Equal(3, me.Boons.Decoy.Decoys);

            Actor goblin = Goblin();
            var claw = new Attack("claw", new DiceRoll(1, Die.D4), DamageType.Slashing);

            Attempt hit = Strike.Roll(resolver, goblin, me, claw);
            Attempt landed = Strike.Decoyed(resolver, goblin, me, hit);

            Assert.True(hit.Succeeded);
            Assert.False(landed.Succeeded);
            Assert.Equal(2, me.Boons.Decoy.Decoys);
        }

        [Fact]
        public void HasteDoublesSpeedAddsANarrowActionAndLeavesLethargyWhenItEnds()
        {
            Assert.True(Faithful("haste"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 8);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("haste"), Aim.At(me), turn: mine, fight: fight);

            Assert.Equal(60, me.Moves);
            Assert.True(me.Boons.LimitedAction);

            // the next turn: two ordinary actions and the narrow one
            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            mine = fight.Next();

            Assert.Equal(1, mine.Limited);
            Assert.True(fight.Dash(mine));
            Assert.True(fight.Dash(mine));
            Assert.True(fight.Dash(mine));
            Assert.False(fight.Dash(mine));

            cast.Release(me);

            Assert.True(me.Has(Condition.Incapacitated));
            Assert.Equal(0, me.Moves);
        }

        [Fact]
        public void ShillelaghRewritesAClubToTheCastersAbilityAndDie()
        {
            Assert.True(Faithful("shillelagh"));

            var druid = new Actor("druid", 5, new AbilityScores(8, 10, 10, 10, 18, 10),
                                  Allegiance.Hero);
            druid.SetHealth(new Health(30));
            var caster = new Caster(druid, Ability.Wisdom,
                                    SpellSlots.For(CasterProgression.Full, 5));
            caster.Learn(Book.Find("shillelagh"));

            var club = new Attack("club", new DiceRoll(1, Die.D4), DamageType.Bludgeoning);
            Assert.Equal(Ability.Strength, club.AbilityFor(druid));

            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(caster, Book.Find("shillelagh"), Aim.Nothing);

            Assert.Equal(Ability.Wisdom, club.AbilityFor(druid));
            Assert.Equal(Die.D10, club.DamageFor(druid).Die);

            // a creature that resists bludgeoning takes the force instead
            Actor skeleton = Goblin("skeleton");
            skeleton.SetDefense(DamageType.Bludgeoning, Defense.Resistant);
            Assert.Equal(DamageType.Force, club.DamageTypeFor(druid, skeleton));
        }

        [Fact]
        public void EnlargeAndReduceAreModesAndOnlyTheUnwillingSave()
        {
            Assert.True(Faithful("enlarge_reduce"));

            Caster wizard = Wizard(out Actor me);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(20)));

            // willing: no save, even on a 20
            cast.Cast(wizard, Book.Find("enlarge_reduce"), Aim.At(me).Choosing("enlarge"));
            Assert.Equal(Size.Large, me.CurrentSize);
            Assert.Equal(Advantage.Advantage, me.SaveAdvantage(Ability.Strength));

            // unwilling: the goblin saves on the 20
            Actor goblin = Goblin();
            cast.Cast(wizard, Book.Find("enlarge_reduce"), Aim.At(goblin).Choosing("reduce"));
            Assert.Equal(Size.Medium, goblin.CurrentSize);
        }

        [Fact]
        public void SpareTheDyingStabilizesAndDamageUndoesIt()
        {
            Assert.True(Faithful("spare_the_dying"));
            Assert.Equal(3, Book.Find("spare_the_dying").RangeAt(1));
            Assert.Equal(24, Book.Find("spare_the_dying").RangeAt(17));

            Caster wizard = Wizard(out _);
            var friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(10));
            friend.Suffer(10, DamageType.Slashing);

            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(wizard, Book.Find("spare_the_dying"), Aim.At(friend));

            Assert.True(friend.Stable);

            friend.Suffer(1, DamageType.Slashing);
            Assert.False(friend.Stable);
        }

        [Fact]
        public void RemoveCurseEndsAHexWhateverItsLevel()
        {
            Assert.True(Faithful("remove_curse"));
            Assert.True(Book.Find("hex").Curse);

            Caster wizard = Wizard(out Actor me);
            Caster witch = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            cast.Cast(witch, Book.Find("hex"), Aim.At(me).Choosing(Ability.Strength), castAt: 5);
            Assert.True(me.Boons.Has("hex"));

            cast.Cast(wizard, Book.Find("remove_curse"), Aim.At(me));
            Assert.False(me.Boons.Has("hex"));
        }

        [Fact]
        public void GreaterRestorationEndsTheChosenOneAndRestoresScores()
        {
            Assert.True(Faithful("greater_restoration"));

            Caster wizard = Wizard(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            var friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(10));
            friend.Apply(Condition.Petrified);
            friend.Scores.ShiftUntilRest(Ability.Wisdom, -1);

            cast.Cast(wizard, Book.Find("greater_restoration"),
                      Aim.At(friend).Choosing("petrified"));
            Assert.False(friend.Has(Condition.Petrified));
            Assert.True(friend.Scores.AnyShifted);

            cast.Cast(wizard, Book.Find("greater_restoration"),
                      Aim.At(friend).Choosing("abilities"));
            Assert.False(friend.Scores.AnyShifted);
        }

        [Fact]
        public void TheGlobeStopsALowSpellCastFromOutside()
        {
            Assert.True(Faithful("globe_of_invulnerability"));

            Encounter fight = Duel(Script(20, 1, 20), out Caster wizard, out Actor me,
                                   out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("globe_of_invulnerability"), Aim.Nothing, turn: mine,
                      fight: fight);

            // an enemy mage outside the globe, which is 10 feet around (2,2)
            Caster enemy = Wizard(out Actor them);
            fight.Field.Place(them, new Cell(9, 2));

            Casting bolt = cast.Cast(enemy, Book.Find("fire_bolt"), Aim.At(me), fight: fight);
            Assert.True(bolt.Cast, bolt.Refusal);
            Assert.False(bolt.Landings.Single().Landed);
            Assert.Equal(80, me.Health.Current);
        }

        [Fact]
        public void DarknessBlocksSightAndYieldsOnlyToTruesight()
        {
            Assert.True(Faithful("fog_cloud"));
            Assert.True(Faithful("darkness"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 8);
            var cast = new Incantation(fight.Resolver);

            Assert.True(fight.Sees(me, goblin));

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("darkness"), Aim.On(new Cell(5, 2)), turn: mine,
                      fight: fight);

            // neither sees the other, so the two leans cancel: SRD's fog-fight
            Assert.False(fight.Sees(me, goblin));
            Assert.True(Strike.Lean(me, goblin, false, fight.Sees(me, goblin),
                                    fight.Sees(goblin, me)).IsFlat());

            me.Boons.Add(new Boon("true_seeing", duration: Duration.Rest) { Truesight = true });
            Assert.True(fight.Sees(me, goblin));
        }

        [Fact]
        public void SunburstBurnsAwayADarkness()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out _, x: 8);
            var cast = new Incantation(fight.Resolver);
            Caster enemy = Wizard(out Actor them);
            fight.Field.Place(them, new Cell(10, 4));

            cast.Cast(enemy, Book.Find("darkness"), Aim.On(new Cell(8, 2)), fight: fight);
            Assert.Single(fight.Zones);
            Assert.True(them.IsConcentrating);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("sunburst"), Aim.On(new Cell(8, 3)), turn: mine,
                      fight: fight);

            Assert.Empty(fight.Zones);
            Assert.False(them.IsConcentrating);
        }

        [Fact]
        public void StinkingCloudTakesTheActionsOfWhoeverStartsATurnInIt()
        {
            Assert.True(Faithful("stinking_cloud"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 8);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("stinking_cloud"), Aim.On(new Cell(9, 2)), turn: mine,
                      fight: fight);
            fight.EndTurn();

            Turn theirs = fight.Next();

            Assert.True(goblin.Has(Condition.Poisoned));
            Assert.False(theirs.Can(Spend.Action));
            Assert.False(theirs.Can(Spend.Bonus));

            fight.EndTurn();
            Assert.False(goblin.Has(Condition.Poisoned));
        }

        [Fact]
        public void SleetStormKnocksDownAndBreaksConcentration()
        {
            Assert.True(Faithful("sleet_storm"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 8);
            var cast = new Incantation(fight.Resolver);

            // the goblin is holding a Bless when the storm lands on it
            Caster enemy = Wizard(out Actor them);
            fight.Field.Remove(goblin);
            var enemyCaster = new Caster(goblin, Ability.Wisdom,
                                         SpellSlots.For(CasterProgression.Full, 5));
            enemyCaster.Learn(Book.Find("bless"));
            fight.Field.Place(goblin, new Cell(8, 2));
            cast.Cast(enemyCaster, Book.Find("bless"), Aim.At(goblin), fight: fight);
            Assert.True(goblin.IsConcentrating);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("sleet_storm"), Aim.On(new Cell(9, 2)), turn: mine,
                      fight: fight);
            fight.EndTurn();

            // the goblin starts its turn in it: a failed Dex save, prone and no concentration
            fight.Next();
            Assert.True(goblin.Has(Condition.Prone));
            Assert.False(goblin.IsConcentrating);
            Assert.True(fight.IsRough(new Cell(9, 2), goblin));
        }

        [Fact]
        public void CloudkillDriftsAwayFromItsCaster()
        {
            Assert.True(Faithful("cloudkill"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out _, x: 10);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("cloudkill"), Aim.On(new Cell(4, 2)), turn: mine,
                      fight: fight);

            SpellZone cloud = cast.ZonesOf(wizard.Actor).Single();
            Assert.Equal(new Cell(4, 2), cloud.Centre);

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            fight.Next();

            Assert.Equal(new Cell(6, 2), cloud.Centre);
        }

        sealed class Yes : IReactionChooser
        {
            public IReaction Choose(Encounter fight, Actor reactor, Moment moment,
                                    IReadOnlyList<IReaction> options) => options.FirstOrDefault();
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


        // --- the third batch: lasting repeats, a spell that swings, leaps, rams, a cloud ---------

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
        public void TrueStrikeSwingsTheWeaponWithTheCastersAbility()
        {
            Assert.True(Faithful("true_strike"));

            Encounter fight = Duel(Script(20, 1, 10, 4), out Caster wizard, out _, out Actor goblin);
            var cast = new Incantation(fight.Resolver);
            var dagger = new Attack("dagger", new DiceRoll(1, Die.D4), DamageType.Piercing,
                                    finesse: true);

            Turn mine = fight.Next();

            Casting refused = cast.Cast(wizard, Book.Find("true_strike"), Aim.At(goblin),
                                        turn: mine, fight: fight);
            Assert.False(refused.Cast);

            // a 10 plus Intelligence (+4) and proficiency (+6) hits AC 10; 4 on the d4, +4 Int,
            // and 3d6 radiant at level 17 (ones after the script runs out)
            Casting strike = cast.Cast(wizard, Book.Find("true_strike"),
                                       Aim.At(goblin).With(dagger), turn: mine, fight: fight);

            Assert.True(strike.Cast, strike.Refusal);
            Assert.Equal(100 - (4 + 4) - 3, goblin.Health.Current);
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
        public void FlamingSphereRollsIntoACreatureAndStops()
        {
            Assert.True(Faithful("flaming_sphere"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin,
                                   x: 7);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();

            Casting taken = cast.Cast(wizard, Book.Find("flaming_sphere"), Aim.On(new Cell(7, 2)),
                                      turn: mine, fight: fight);
            Assert.False(taken.Cast);

            cast.Cast(wizard, Book.Find("flaming_sphere"), Aim.On(new Cell(4, 2)), turn: mine,
                      fight: fight);
            Assert.Equal(100, goblin.Health.Current);

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            mine = fight.Next();

            // rolled at a square past the goblin: it stops at the goblin, which saves (a one)
            cast.Again(wizard, Book.Find("flaming_sphere"), Aim.On(new Cell(9, 2)), mine, fight);

            SpellZone sphere = cast.ZonesOf(wizard.Actor).Single();
            Assert.Equal(new Cell(6, 2), sphere.Centre);
            Assert.Equal(98, goblin.Health.Current);
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


        // --- the fourth batch: directed turns, dropping, forced moves, berries, an aura ----------

        [Fact]
        public void CommandGrovelProneAndTheTurnIsOver()
        {
            Assert.True(Faithful("command"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin, x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();

            Casting refused = cast.Cast(wizard, Book.Find("command"), Aim.At(goblin), turn: mine,
                                        fight: fight);
            Assert.False(refused.Cast);

            cast.Cast(wizard, Book.Find("command"), Aim.At(goblin).Choosing("grovel"), turn: mine,
                      fight: fight);
            fight.EndTurn();

            // the goblin's turn is spent before anyone can drive it, and the round comes back
            Turn next = fight.Next();

            Assert.True(goblin.Has(Condition.Prone));
            Assert.Same(wizard.Actor, next.Actor);
        }

        [Fact]
        public void CommandApproachWalksItToTheCasterAndHaltStopsItDead()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 7);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("command"), Aim.At(goblin).Choosing("approach"),
                      turn: mine, fight: fight);
            fight.EndTurn();
            fight.Next();

            Assert.Equal(1, fight.Field.Distance(me, goblin));
        }

        [Fact]
        public void CommandFleeRunsAndDropLeavesTheWeaponBehind()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("command"), Aim.At(goblin).Choosing("flee"), turn: mine,
                      fight: fight);
            fight.EndTurn();
            fight.Next();

            // 30 feet, and a Dash for 30 more: as far as the hall lets it
            Assert.True(fight.Field.Distance(me, goblin) >= 7);

            Actor other = Goblin("other");
            other.Disarm(new Cell(0, 0));
            var scimitar = new Attack("scimitar", new DiceRoll(1, Die.D6), DamageType.Slashing);
            var bite = new Attack("bite", new DiceRoll(1, Die.D4), DamageType.Piercing, hand: Hand.None);

            Assert.False(other.CanUse(scimitar));
            Assert.True(other.CanUse(bite));
        }

        [Fact]
        public void FearDropsTheWeaponAndDrivesTheFrightenedAway()
        {
            Assert.True(Faithful("fear"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("fear"), Aim.Toward(Facing.East), turn: mine, fight: fight);

            Assert.True(goblin.Has(Condition.Frightened));
            Assert.True(goblin.Disarmed);

            fight.EndTurn();
            fight.Next();

            Assert.True(fight.Field.Distance(me, goblin) > 1);
        }

        [Fact]
        public void TelekinesisMovesAndHoldsACreatureAndOneTargetAtATime()
        {
            Assert.True(Faithful("telekinesis"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out _, out Actor goblin, x: 6);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting grip = cast.Cast(wizard, Book.Find("telekinesis"),
                                     new Aim(new[] { goblin }, new Cell(9, 4)).Choosing("creature"),
                                     turn: mine, fight: fight);

            Assert.True(grip.Cast, grip.Refusal);
            Assert.Equal(new Cell(9, 4), fight.Field.Where(goblin));
            Assert.True(goblin.Has(Condition.Restrained));

            // a gargantuan creature is too big to move
            Actor titan = Goblin("titan");
            titan.Size = Size.Gargantuan;
            fight.Field.Place(titan, new Cell(5, 0));

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();
            mine = fight.Next();

            cast.Again(wizard, Book.Find("telekinesis"),
                       new Aim(new[] { titan }, new Cell(6, 0)).Choosing("creature"), mine, fight);

            Assert.Equal(new Cell(5, 0), fight.Field.Where(titan));
            Assert.False(goblin.Has(Condition.Restrained));
        }

        [Fact]
        public void GoodberryPutsTenBerriesInThePackThatHealAndVanish()
        {
            Assert.True(Faithful("goodberry"));

            Content.Schema.Library srd = Content.Schema.Library.Srd();
            var hero = new Content.Sheet.Hero("Wren", srd.Class("druid"), srd.Kind("human"),
                                              srd.Background("sage"),
                                              Content.Creation.Creation.Standard(srd.Class("druid")),
                                              3);
            hero.Build(spells: new[] { Book.Find("goodberry") });
            hero.Actor.Suffer(5, DamageType.Slashing);

            Casting berries = new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(hero.Caster, Book.Find("goodberry"), Aim.Nothing);

            hero.Receive(berries, srd.Items);
            Assert.Equal(10, hero.Pack.CountOf("goodberry"));

            int hp = hero.Actor.Health.Current;
            Assert.Equal(1, hero.Use("goodberry", new StandardResolver(new ScriptedRng(1))));
            Assert.Equal(hp + 1, hero.Actor.Health.Current);
            Assert.Equal(9, hero.Pack.CountOf("goodberry"));

            hero.LongRest();
            Assert.Equal(0, hero.Pack.CountOf("goodberry"));
        }

        [Fact]
        public void PassWithoutTraceIsOnWhoeverStandsInTheAura()
        {
            Assert.True(Faithful("pass_without_trace"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            var friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(10));
            fight.Field.Place(friend, new Cell(10, 4));

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("pass_without_trace"), Aim.Nothing, turn: mine,
                      fight: fight);

            Assert.Equal(10, me.Boons.FlatOnCheck(Skill.Stealth));
            Assert.Equal(0, goblin.Boons.FlatOnCheck(Skill.Stealth));

            // off the board it is simply on the caster
            Caster alone = Wizard(out Actor sneak);
            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(alone, Book.Find("pass_without_trace"), Aim.Nothing);
            Assert.Equal(10, sneak.Boons.FlatOnCheck(Skill.Stealth));
        }


        // --- the fifth batch: walls, cages, teleports and pushes ------------------------------

        [Fact]
        public void WallOfFireBurnsItsChosenSideAndNotTheOther()
        {
            Assert.True(Faithful("wall_of_fire"));

            // a wall running south from (5,0) for 12 squares (clipped to 5); the burning side is
            // left of south - east. a goblin two squares east at (7,2), another west at (3,2)
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 7);
            var cast = new Incantation(fight.Resolver);
            Actor west = Goblin("west");
            fight.Field.Place(west, new Cell(3, 1));

            Turn mine = fight.Next();
            Casting wall = cast.Cast(wizard, Book.Find("wall_of_fire"),
                                     new Aim(null, new Cell(5, 0), Facing.South).Choosing("line")
                                         .On(Side.Left),
                                     turn: mine, fight: fight);

            Assert.True(wall.Cast, wall.Refusal);

            // opaque: the wizard at (2,2) no longer sees the goblin at (7,2)
            Assert.False(fight.Sees(me, goblin));

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            // 5d8 of ones, no save, for ending its turn within 10 feet of the burning side
            Assert.Equal(95, goblin.Health.Current);
            Assert.Equal(100, west.Health.Current);
        }

        [Fact]
        public void BladeBarrierGivesCoverAndCutsWhoeverStandsInIt()
        {
            Assert.True(Faithful("blade_barrier"));

            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin,
                                   x: 5);
            var cast = new Incantation(fight.Resolver);
            Actor behind = Goblin("behind");
            fight.Field.Place(behind, new Cell(8, 2));

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("blade_barrier"),
                      new Aim(null, new Cell(5, 0), Facing.South).Choosing("line"), turn: mine,
                      fight: fight);

            // the goblin standing in it when it appeared failed its save: 6d10 of ones
            Assert.Equal(94, goblin.Health.Current);
            Assert.Equal(5, fight.Cover(me, behind));
            Assert.Equal(0, fight.Cover(me, goblin));
            Assert.True(fight.IsRough(new Cell(5, 3), behind));
        }

        [Fact]
        public void AForcecageTrapsWhoeverIsInsideAndMagicOutNeedsACharismaSave()
        {
            Assert.True(Faithful("forcecage"));

            Encounter fight = Duel(Script(20, 1, 1), out Caster wizard, out Actor me,
                                   out Actor goblin, x: 7);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Casting cage = cast.Cast(wizard, Book.Find("forcecage"),
                                     Aim.On(new Cell(7, 2)).Choosing("cage"), turn: mine,
                                     fight: fight);

            Assert.True(cage.Cast, cage.Refusal);

            // bars: seen through, never walked through
            Assert.True(fight.Sees(me, goblin));
            Assert.Null(fight.Field.RouteFor(goblin, new Cell(2, 4)));

            // a trapped caster's Misty Step out fails its Charisma save (a one)
            Caster trapped = Wizard(out Actor inside);
            fight.Field.Place(inside, new Cell(8, 3));

            Casting step = cast.Cast(trapped, Book.Find("misty_step"), Aim.On(new Cell(10, 3)),
                                     fight: fight);

            Assert.Equal(new Cell(8, 3), fight.Field.Where(inside));
            Assert.False(step.Landings.Single().Landed);
        }

        [Fact]
        public void MistyStepMovesTheCasterAndThunderwavePushes()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("thunderwave"), Aim.Toward(Facing.East), turn: mine,
                      fight: fight);

            // a failed save: pushed 10 feet east of (3,2)
            Assert.Equal(new Cell(5, 2), fight.Field.Where(goblin));

            cast.Cast(wizard, Book.Find("misty_step"), Aim.On(new Cell(2, 4)), turn: mine,
                      fight: fight);
            Assert.Equal(new Cell(2, 4), fight.Field.Where(me));
        }

        [Fact]
        public void ABurstDoesNotGoRoundAWall()
        {
            // a wall between (4,*) and (5,*) down the whole map
            const string walled = @"
+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . .|. . . . . .|
+ + + + + + + + + + + +
|. . . . .|. . . . . .|
+ + + + + + + + + + + +
|. . . . .|. . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+";

            Assert.True(MapReader.TryRead(walled, out MapLayout map, out string problem), problem);
            var field = new Battlefield(map);
            Actor near = Goblin("near");
            Actor beyond = Goblin("beyond");
            field.Place(near, new Cell(3, 1));
            field.Place(beyond, new Cell(5, 1));

            List<Actor> caught = field.Caught(new Cell(4, 1), 2).ToList();

            Assert.Contains(near, caught);
            Assert.DoesNotContain(beyond, caught);
        }


        // --- Dissonant Whispers and Dragon's Breath: in SRD 5.2.1 after all (2026-09-25) ---------

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
            Assert.Equal(CastingTime.BonusAction, breath.CastingTime);

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
