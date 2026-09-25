using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Sheet;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Tests
{
    // the 2026-09-23 decisions, held to the SRD spell files: line and cone shapes, a real reaction
    // window, Extra Attack ported literally, and the rule that an approximation never wears an
    // SRD name (_design_docs/cc_task_shapes-reactions-actions.md)
    public class ShapesAndReactionsTests
    {
        static readonly SpellBook Book = SpellBook.Srd();

        // eleven squares by three
        const string Hall = @"
+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+";

        static Encounter Field(IRng rng)
        {
            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);

            return new Encounter(new StandardResolver(rng), new Battlefield(map), new CombatLog());
        }

        // a level 5 mage with slots: four 1st, three 2nd, two 3rd
        static Caster Mage(out Actor actor, string id = "mage", int level = 5,
                           Allegiance side = Allegiance.Hero)
        {
            actor = new Actor(id, level, new AbilityScores(8, 14, 14, 18, 10, 10), side);
            actor.SetHealth(new Health(40, Die.D6, level));

            var caster = new Caster(actor, Ability.Intelligence,
                                    SpellSlots.For(CasterProgression.Full, level));

            foreach (Spell spell in Book.All) caster.Learn(spell);

            return caster;
        }

        static int Slots(Caster caster, int level) => ((SpellSlots)caster.Resource).Remaining(level);

        static Actor Dummy(string id, int hp = 100, int ac = 10,
                           Allegiance side = Allegiance.Enemy)
        {
            var actor = new Actor(id, 1, new AbilityScores(), side);
            actor.SetHealth(new Health(hp));
            actor.Armor = new ArmorProfile(ArmorWeight.Heavy, ac);
            return actor;
        }


        // --- the shapes load, and land where they should ------------------------------------------

        [Fact]
        public void LightningBoltLoadsAsAHundredFootLine()
        {
            Spell bolt = Book.Find("lightning_bolt");
            SpellEffect effect = bolt.Effects.Single();

            Assert.False(bolt.Approximated);
            Assert.Equal(Reach.Line, effect.Reach);
            Assert.Equal(20, effect.Length);
            Assert.Equal(1, effect.Width);
            Assert.True(bolt.NeedsADirection);
        }

        [Fact]
        public void ConeOfColdLoadsAsASixtyFootCone()
        {
            Spell cone = Book.Find("cone_of_cold");
            SpellEffect effect = cone.Effects.Single();

            Assert.False(cone.Approximated);
            Assert.Equal(Reach.Cone, effect.Reach);
            Assert.Equal(12, effect.Length);
            Assert.Equal(DiceRoll.Parse("8d8"), effect.Amount);
        }

        [Fact]
        public void BurningHandsAndThunderwaveAreTheirRealShapes()
        {
            Assert.Equal(Reach.Cone, Book.Find("burning_hands").Effects[0].Reach);
            Assert.Equal(3, Book.Find("burning_hands").Effects[0].Length);

            Assert.Equal(Reach.Cube, Book.Find("thunderwave").Effects[0].Reach);
            Assert.Equal(3, Book.Find("thunderwave").Effects[0].Length);
        }

        [Fact]
        public void ALightningBoltHitsEverythingInItsRowAndNothingBeside()
        {
            Encounter fight = Field(new ScriptedRng(20, 1, 1, 1));
            Caster mage = Mage(out Actor me);

            Actor near = Dummy("near");
            Actor far = Dummy("far");
            Actor aside = Dummy("aside");

            fight.Enlist(me, new Cell(0, 1));
            fight.Enlist(near, new Cell(3, 1));
            fight.Enlist(far, new Cell(10, 1));
            fight.Enlist(aside, new Cell(5, 0));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);

            Casting result = cast.Cast(mage, Book.Find("lightning_bolt"), Aim.Toward(Facing.East),
                                       fight: fight);

            Assert.True(result.Cast, result.Refusal);
            Assert.Contains(near, result.Touched);
            Assert.Contains(far, result.Touched);
            Assert.DoesNotContain(aside, result.Touched);
            Assert.DoesNotContain(me, result.Touched);

            // it shows the whole line, not just who was in it
            Assert.Equal(10, result.Covered.Count);
        }

        [Fact]
        public void AConeAimedAtASquareIsThrownTowardIt()
        {
            Encounter fight = Field(new ScriptedRng(20, 1, 1));
            Caster mage = Mage(out Actor me);

            Actor caught = Dummy("caught");
            Actor behind = Dummy("behind");

            // a 15-foot cone east of (5, 1) is (6, 1), (7, 1), then (8, 0) to (8, 2)
            fight.Enlist(me, new Cell(5, 1));
            fight.Enlist(caught, new Cell(8, 2));
            fight.Enlist(behind, new Cell(3, 1));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);

            // clicking a square to the east throws it east
            Casting result = cast.Cast(mage, Book.Find("burning_hands"), Aim.On(new Cell(9, 1)),
                                       fight: fight);

            Assert.True(result.Cast, result.Refusal);
            Assert.Contains(caught, result.Touched);
            Assert.DoesNotContain(behind, result.Touched);
        }

        [Fact]
        public void ALineWithNothingToPointAtIsRefusedOnTheBoard()
        {
            Encounter fight = Field(new ScriptedRng(20, 1));
            Caster mage = Mage(out Actor me);

            fight.Enlist(me, new Cell(0, 1));
            fight.Enlist(Dummy("someone"), new Cell(5, 1));
            fight.Begin();

            Casting result = new Incantation(fight.Resolver)
                .Cast(mage, Book.Find("lightning_bolt"), Aim.Nothing, fight: fight);

            Assert.False(result.Cast);
            Assert.Contains("pointed", result.Refusal);
            Assert.Equal(2, Slots(mage, 3));
        }

        [Fact]
        public void OnTheBoardATouchSpellHasToTouch()
        {
            Encounter fight = Field(new ScriptedRng(20, 1));
            Caster mage = Mage(out Actor me);

            Actor friend = Dummy("friend", side: Allegiance.Hero);

            fight.Enlist(me, new Cell(0, 1));
            fight.Enlist(friend, new Cell(3, 1));
            fight.Begin();

            Casting result = new Incantation(fight.Resolver)
                .Cast(mage, Book.Find("cure_wounds"), Aim.At(friend), fight: fight);

            Assert.False(result.Cast);
            Assert.Contains("out of range", result.Refusal);
            Assert.Equal(4, Slots(mage, 1));
        }

        [Fact]
        public void TheReaderWantsALengthForALineAndAWidthToo()
        {
            SpellReader.TryRead(@"{""spells"":[{""id"":""bad"",""level"":1,""effects"":[
              {""primitive"":""damage"",""reach"":""line"",""amount"":""1d6"",
               ""damage_type"":""fire""}]}]}", out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.Contains("'length'"));
            Assert.Contains(problems, p => p.Contains("'width'"));
        }

        [Fact]
        public void TheReaderRefusesAConeThatBorrowsARadius()
        {
            SpellReader.TryRead(@"{""spells"":[{""id"":""bad"",""level"":1,""effects"":[
              {""primitive"":""damage"",""reach"":""cone"",""length"":3,""radius"":3,
               ""amount"":""1d6"",""damage_type"":""fire""}]}]}", out _,
                                out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.Contains("'radius'"));
        }


        // --- Shield ---------------------------------------------------------------------------------

        [Fact]
        public void ShieldIsAReactionToBeingHitAndNotAnApproximation()
        {
            Spell shield = Book.Find("shield");

            Assert.False(shield.Approximated);
            Assert.Equal(CastingTime.Reaction, shield.CastingTime);
            Assert.Equal(Trigger.Hit, shield.Trigger);
            Assert.Equal(Duration.NextTurn, shield.Effects.Single().Duration);
        }

        [Fact]
        public void AShieldCannotBeCastOnYourOwnTurn()
        {
            Caster mage = Mage(out _);

            Casting result = new Incantation(new StandardResolver(new ScriptedRng(10)))
                .Cast(mage, Book.Find("shield"), Aim.Nothing);

            Assert.False(result.Cast);
            Assert.Contains("reaction", result.Refusal);
            Assert.Equal(4, Slots(mage, 1));
        }

        // the goblin goes first: initiative 1 for the mage, 20 for the goblin
        static Encounter GoblinSwingsAtTheMage(out Caster mage, out Actor me, out Actor goblin,
                                               out Incantation cast, params int[] rolls)
        {
            Encounter fight = Field(new ScriptedRng(rolls));
            cast = new Incantation(fight.Resolver);

            mage = Mage(out me);
            me.Armor = new ArmorProfile(ArmorWeight.Light, 12); // armor class 14 with Dex

            goblin = Dummy("goblin");
            goblin.Scores.Raise(Ability.Dexterity, 4); // +2, and +2 proficiency

            fight.Enlist(me, new Cell(1, 1));
            fight.Enlist(goblin, new Cell(2, 1));

            var shield = new SpellReaction(mage, Book.Find("shield"), cast);
            fight.Arm(me, shield);
            fight.ChooseReactionsWith(me, ReactionChoosers.WhenItHelps);

            fight.Begin();

            return fight;
        }

        static readonly Attack Scimitar = new Attack("scimitar", DiceRoll.Parse("1d6"),
                                                     DamageType.Slashing, Ability.Dexterity,
                                                     finesse: true);

        [Fact]
        public void AShieldTurnsAWouldBeHitIntoAMiss()
        {
            // 12 + 4 = 16 hits armor class 14, and does not hit 19
            Encounter fight = GoblinSwingsAtTheMage(out Caster mage, out Actor me,
                                                    out Actor goblin, out _, 1, 20, 12, 5);

            Assert.Equal(14, me.ArmorClass);

            Blow blow = fight.Hit(fight.Next(), me, Scimitar);

            Assert.False(blow.Hit);
            Assert.Equal(16, blow.Attempt.Total);
            Assert.Equal(19, me.ArmorClass);
            Assert.Equal(me.Health.Maximum, me.Health.Current);

            // a 1st-level slot, and the reaction
            Assert.Equal(3, Slots(mage, 1));
            Assert.Equal(0, fight.ReactionsLeft(me));
        }

        [Fact]
        public void AShieldLastsUntilTheStartOfTheCastersNextTurn()
        {
            Encounter fight = GoblinSwingsAtTheMage(out _, out Actor me, out _, out _,
                                                    1, 20, 12, 5);

            fight.Hit(fight.Next(), me, Scimitar);
            fight.EndTurn();

            Assert.Equal(19, me.ArmorClass);

            Turn mine = fight.Next();

            Assert.Same(me, mine.Actor);
            Assert.Equal(14, me.ArmorClass);
        }

        [Fact]
        public void AHitAShieldCouldNotTurnSpendsNothing()
        {
            // 17 + 4 = 21 beats even the shielded 19
            Encounter fight = GoblinSwingsAtTheMage(out Caster mage, out Actor me, out _, out _,
                                                    1, 20, 17, 5);

            Blow blow = fight.Hit(fight.Next(), me, Scimitar);

            Assert.True(blow.Hit);
            Assert.Equal(4, Slots(mage, 1));
            Assert.Equal(1, fight.ReactionsLeft(me));
        }

        [Fact]
        public void AShieldAnswersASpellAttackToo()
        {
            // initiative 1 and 20, then the enemy mage's fire bolt: 10 + 7 = 17 hits armor class
            // 14 and misses the shielded 19 (meeting the number is a hit, so 19 would not do)
            Encounter fight = Field(new ScriptedRng(1, 20, 10, 5));
            var cast = new Incantation(fight.Resolver);

            Caster mage = Mage(out Actor me);
            me.Armor = new ArmorProfile(ArmorWeight.Light, 12);

            Caster enemy = Mage(out Actor them, "enemy", side: Allegiance.Enemy);

            fight.Enlist(me, new Cell(1, 1));
            fight.Enlist(them, new Cell(6, 1));
            fight.Arm(me, new SpellReaction(mage, Book.Find("shield"), cast));
            fight.ChooseReactionsWith(me, ReactionChoosers.WhenItHelps);
            fight.Begin();

            Turn turn = fight.Next();
            Casting bolt = cast.Cast(enemy, Book.Find("fire_bolt"), Aim.At(me), turn: turn,
                                     fight: fight);

            Assert.True(bolt.Cast, bolt.Refusal);
            Assert.Equal(0, bolt.TotalDamage);
            Assert.Equal(3, Slots(mage, 1));
        }


        // --- Counterspell ---------------------------------------------------------------------------

        static Encounter Duel(out Caster hero, out Actor me, out Caster enemy, out Actor them,
                              out Incantation cast, int distance, params int[] rolls)
        {
            Encounter fight = Field(new ScriptedRng(rolls));
            cast = new Incantation(fight.Resolver);

            hero = Mage(out me);
            enemy = Mage(out them, "enemy", side: Allegiance.Enemy);

            fight.Enlist(me, new Cell(0, 1));
            fight.Enlist(them, new Cell(distance, 1));
            fight.Arm(me, new SpellReaction(hero, Book.Find("counterspell"), cast));
            fight.Begin();

            return fight;
        }

        [Fact]
        public void CounterspellStopsTheCastAndTheSlotIsNotSpent()
        {
            // initiative 1 and 20: the enemy first. then its Magic Missile is countered - the
            // enemy's Constitution save is a 3
            Encounter fight = Duel(out Caster hero, out _, out Caster enemy, out Actor them,
                                   out Incantation cast, 6, 1, 20, 3);

            Turn turn = fight.Next();
            Assert.Same(them, turn.Actor);

            Casting missile = cast.Cast(enemy, Book.Find("magic_missile"), Aim.At(hero.Actor),
                                        turn: turn, fight: fight);

            Assert.False(missile.Cast);
            Assert.True(missile.Countered);

            // SRD 5.2.1: the action is wasted, and the slot is not expended
            Assert.Equal(1, turn.Actions);
            Assert.Equal(4, Slots(enemy, 1));

            // and the counterspeller paid a 3rd-level slot and their reaction
            Assert.Equal(1, Slots(hero, 3));
            Assert.Equal(0, fight.ReactionsLeft(hero.Actor));
            Assert.Equal(hero.Actor.Health.Maximum, hero.Actor.Health.Current);
        }

        [Fact]
        public void ACounterspellTheCasterSavesAgainstLetsTheSpellThrough()
        {
            // the enemy's save is a 19; then magic missile's three darts
            Encounter fight = Duel(out Caster hero, out _, out Caster enemy, out Actor them,
                                   out Incantation cast, 6, 1, 20, 19, 4);

            Casting missile = cast.Cast(enemy, Book.Find("magic_missile"), Aim.At(hero.Actor),
                                        turn: fight.Next(), fight: fight);

            Assert.True(missile.Cast);
            Assert.False(missile.Countered);
            Assert.Equal(3, Slots(enemy, 1));
            Assert.Equal(1, Slots(hero, 3));
        }

        [Fact]
        public void ACounterspellOnlyReachesAsFarAsItsRange()
        {
            Encounter fight = Duel(out Caster hero, out Actor me, out _, out Actor them,
                                   out Incantation cast, 10, 1, 20);

            // ten squares away, and Counterspell reaches twelve
            var counter = new SpellReaction(hero, Book.Find("counterspell"), cast);

            Assert.True(counter.CanAnswer(fight, me, Moment.Cast(them, "fireball", 3)));

            // the same reaction with a one-square reach does not get offered at ten
            var shortReach = new Spell("short_counter", 3, School.Abjuration,
                                       new[] { new SpellEffect(Primitive.Counter, Reach.Creature,
                                                               save: Ability.Constitution,
                                                               onSave: OnSave.Negates) },
                                       range: 1, castingTime: CastingTime.Reaction,
                                       trigger: Trigger.Cast);

            hero.Learn(shortReach);

            Assert.False(new SpellReaction(hero, shortReach, cast)
                             .CanAnswer(fight, me, Moment.Cast(them, "fireball", 3)));
        }

        [Fact]
        public void CounterspellIsNotAnApproximationAnyMore()
        {
            Spell counter = Book.Find("counterspell");

            Assert.False(counter.Approximated);
            Assert.Equal(Trigger.Cast, counter.Trigger);
            Assert.Equal(Primitive.Counter, counter.Effects.Single().Kind);
            Assert.Equal(Ability.Constitution, counter.Effects.Single().Save);
        }

        [Fact]
        public void TheReaderRefusesACounterThatIsNotAReactionToACast()
        {
            SpellReader.TryRead(@"{""spells"":[{""id"":""bad"",""level"":3,""effects"":[
              {""primitive"":""counter"",""reach"":""creature""}]}]}", out _,
                                out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.Contains("counter"));
        }

        [Fact]
        public void TheReaderWantsAReactionAndItsTriggerTogether()
        {
            SpellReader.TryRead(@"{""spells"":[{""id"":""bad"",""level"":1,
              ""casting_time"":""reaction"",""effects"":[
              {""primitive"":""sway"",""reach"":""caster"",""sway"":5,""touches"":""armor_class""}]},
              {""id"":""worse"",""level"":1,""trigger"":""hit"",""effects"":[
              {""primitive"":""sway"",""reach"":""caster"",""sway"":5,""touches"":""armor_class""}]}]}",
                                out _, out IReadOnlyList<string> problems);

            Assert.Contains(problems, p => p.StartsWith("bad:") && p.Contains("trigger"));
            Assert.Contains(problems, p => p.StartsWith("worse:") && p.Contains("trigger"));
        }


        // --- what else the audit made faithful --------------------------------------------------------

        [Fact]
        public void CureWoundsAddsTheSpellcastingModifier()
        {
            Caster mage = Mage(out _);
            Actor hurt = Dummy("hurt", hp: 40);
            hurt.Suffer(30, DamageType.Slashing);

            // 2d8 of 3s, and Intelligence 18 is +4
            Casting result = new Incantation(new StandardResolver(new ScriptedRng(3)))
                .Cast(mage, Book.Find("cure_wounds"), Aim.At(hurt));

            Assert.Equal(10, result.TotalHealing);
        }

        [Fact]
        public void GuidingBoltsGlimmerComesWithAHitAndIsSpentByTheNextAttack()
        {
            Caster mage = Mage(out Actor me);
            Actor target = Dummy("target", ac: 10);

            new Incantation(new StandardResolver(new ScriptedRng(15, 1)))
                .Cast(mage, Book.Find("guiding_bolt"), Aim.At(target));

            Assert.Equal(Advantage.Advantage, target.AdvantageAgainstMe);

            Strike.Roll(new StandardResolver(new ScriptedRng(10, 10)), me, target, Scimitar);

            Assert.Equal(Advantage.Flat, target.AdvantageAgainstMe);
        }

        [Fact]
        public void AGuidingBoltThatMissesLeavesNoGlimmer()
        {
            Caster mage = Mage(out _);
            Actor target = Dummy("target", ac: 30);

            new Incantation(new StandardResolver(new ScriptedRng(2)))
                .Cast(mage, Book.Find("guiding_bolt"), Aim.At(target));

            Assert.Equal(Advantage.Flat, target.AdvantageAgainstMe);
        }

        [Fact]
        public void ViciousMockeryIsOneSaveForBothHalves()
        {
            Caster mage = Mage(out _);
            Actor saves = Dummy("saves");
            Actor fails = Dummy("fails");

            // a 20 saves: no damage and no stumble. then a 1 fails both, and 6 psychic
            new Incantation(new StandardResolver(new ScriptedRng(20)))
                .Cast(mage, Book.Find("vicious_mockery"), Aim.At(saves));

            Assert.Equal(Advantage.Flat, saves.AttackAdvantage);

            new Incantation(new StandardResolver(new ScriptedRng(1, 6)))
                .Cast(mage, Book.Find("vicious_mockery"), Aim.At(fails));

            Assert.Equal(Advantage.Disadvantage, fails.AttackAdvantage);
        }

        [Fact]
        public void HexMarksTheTargetForTheCastersHitsAndAChosenAbility()
        {
            Caster mage = Mage(out Actor me);
            Actor target = Dummy("target");

            var cast = new Incantation(new StandardResolver(new ScriptedRng(10)));

            Casting refused = cast.Cast(mage, Book.Find("hex"), Aim.At(target));
            Assert.False(refused.Cast);
            Assert.Contains("ability", refused.Refusal);

            Casting hexed = cast.Cast(mage, Book.Find("hex"),
                                      Aim.At(target).Choosing(Ability.Strength));

            Assert.True(hexed.Cast, hexed.Refusal);
            Assert.Single(target.Boons.MarksFrom(me));
            Assert.Equal(Advantage.Disadvantage,
                         target.CheckAdvantageFor(Ability.Strength, Skill.Athletics));
            Assert.Equal(Advantage.Flat,
                         target.CheckAdvantageFor(Ability.Dexterity, Skill.Stealth));
        }

        [Fact]
        public void GuidanceIsForTheSkillTheCasterChose()
        {
            Caster mage = Mage(out _);
            Actor ally = Dummy("ally", side: Allegiance.Hero);

            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Assert.False(cast.Cast(mage, Book.Find("guidance"), Aim.At(ally)).Cast);

            cast.Cast(mage, Book.Find("guidance"), Aim.At(ally).Choosing(Skill.Stealth));

            Assert.Single(ally.Boons.DiceOnCheck(Skill.Stealth));
            Assert.Empty(ally.Boons.DiceOnCheck(Skill.Athletics));
        }

        [Fact]
        public void MageArmorIsThirteenPlusDexterityWithNoArmorOn()
        {
            Caster mage = Mage(out Actor me);

            // Dex 14: 10 + 2 unarmored
            Assert.Equal(12, me.ArmorClass);

            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(mage, Book.Find("mage_armor"), Aim.At(me));

            Assert.Equal(15, me.ArmorClass);

            // and in armor it does nothing, which is SRD's rule
            me.Armor = new ArmorProfile(ArmorWeight.Heavy, 16);
            Assert.Equal(16, me.ArmorClass);
        }

        [Fact]
        public void PowerWordKillKillsAtAHundredAndHurtsAboveIt()
        {
            Caster mage = Mage(out _, level: 17);

            Actor weak = Dummy("weak", hp: 100);
            Actor strong = Dummy("strong", hp: 200);

            var cast = new Incantation(new StandardResolver(new ScriptedRng(6)));

            cast.Cast(mage, Book.Find("power_word_kill"), Aim.At(weak));
            Assert.True(weak.IsDown);

            mage.Rested(Rest.Long);
            cast.Cast(mage, Book.Find("power_word_kill"), Aim.At(strong));

            // 12d12 of 6s
            Assert.Equal(200 - 72, strong.Health.Current);
        }

        [Fact]
        public void DispelMagicEndsLowSpellsAndHasToBeatTenPlusTheLevelForHighOnes()
        {
            // two enemy casters, because both spells are held and one caster holds one thing
            Caster blesser = Mage(out _, "blesser", level: 17, side: Allegiance.Enemy);
            Caster holder = Mage(out _, "holder", level: 17, side: Allegiance.Enemy);
            Caster mage = Mage(out _, level: 5);

            Actor ally = Dummy("ally", side: Allegiance.Hero);
            Actor target = Dummy("target");

            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            // a 1st-level bless on one creature and a 5th-level hold on another
            cast.Cast(blesser, Book.Find("bless"), Aim.At(target));
            cast.Cast(holder, Book.Find("hold_monster"), Aim.At(ally));

            Assert.True(target.Boons.Has("bless"));
            Assert.True(ally.Has(Condition.Paralyzed));

            // the dispel's ability check rolls a 1: plus 4 is 5, which does not beat 15
            Casting first = cast.Cast(mage, Book.Find("dispel_magic"), Aim.At(target).Choosing("creature"));
            Casting second = cast.Cast(mage, Book.Find("dispel_magic"), Aim.At(ally).Choosing("creature"));

            Assert.False(target.Boons.Has("bless"));
            Assert.Equal(1, first.Landings.Single().Amount);

            Assert.True(ally.Has(Condition.Paralyzed));
            Assert.False(second.Landings.Single().Landed);
        }

        [Fact]
        public void SpiritualWeaponSwingsAgainOnALaterTurnForABonusActionAndNothingElse()
        {
            Encounter fight = Field(new ScriptedRng(20, 1, 15, 4, 15, 4));
            Caster cleric = Mage(out Actor me);

            Actor target = Dummy("target");

            fight.Enlist(me, new Cell(0, 1));
            fight.Enlist(target, new Cell(3, 1));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);

            Turn first = fight.Next();
            // SRD p.165: the force appears in a space within range - beside the target, at (2,1) -
            // and attacks a creature within 5 feet of it
            Casting summoned = cast.Cast(cleric, Book.Find("spiritual_weapon"),
                                         new Aim(new[] { target }, new Cell(2, 1)),
                                         turn: first, fight: fight);

            Assert.True(summoned.Cast, summoned.Refusal);
            Assert.Contains(summoned.Landings, l => l.Effect.Kind == Primitive.Damage && ReferenceEquals(l.Target, target));
            Assert.Equal(0, first.BonusActions);
            Assert.Equal(2, first.Actions);
            Assert.Equal(2, Slots(cleric, 2));

            fight.EndTurn();
            fight.Next();
            fight.EndTurn();

            Turn later = fight.Next();
            Casting again = cast.Again(cleric, Book.Find("spiritual_weapon"),
                                       new Aim(new[] { target }, new Cell(2, 1)), later, fight);

            Assert.True(again.Cast, again.Refusal);
            Assert.Single(again.Landings);
            Assert.Equal(Primitive.Damage, again.Landings[0].Effect.Kind);

            // no slot, one bonus action
            Assert.Equal(2, Slots(cleric, 2));
            Assert.Equal(0, later.BonusActions);
        }

        [Fact]
        public void MeteorSwarmIsOneSaveAndACreatureInTwoBurstsIsCaughtOnce()
        {
            Caster mage = Mage(out _, level: 17);

            Encounter fight = Field(new ScriptedRng(20, 1, 1));
            Actor target = Dummy("target", hp: 1000);

            fight.Enlist(mage.Actor, new Cell(0, 0));
            fight.Enlist(target, new Cell(5, 1));
            fight.Begin();

            var resolver = new LoggingResolver(new StandardResolver(new ScriptedRng(1, 1)));
            var cast = new Incantation(resolver);

            Casting result = cast.Cast(mage, Book.Find("meteor_swarm"),
                                       Aim.OnMany(new Cell(5, 1), new Cell(6, 1)), fight: fight);

            Assert.True(result.Cast, result.Refusal);

            // two landings - fire and bludgeoning - on each creature caught, and one save each.
            // the caster is five squares from a 40-foot burst and is caught too, as in the SRD
            Assert.Equal(2, result.Landings.Count(l => ReferenceEquals(l.Target, target)));
            Assert.Equal(2, result.Landings.Count(l => ReferenceEquals(l.Target, mage.Actor)));
            Assert.Equal(2, resolver.Attempts.Count(a => a.Kind == RollKind.Save));
        }

        [Fact]
        public void EldritchBlastGrowsByBeams()
        {
            SpellEffect blast = Book.Find("eldritch_blast").Effects.Single();

            Assert.Equal(1, blast.TargetsAt(0, 0, 4));
            Assert.Equal(2, blast.TargetsAt(0, 0, 5));
            Assert.Equal(4, blast.TargetsAt(0, 0, 17));

            // and each beam is one d10, not more dice on one beam
            Assert.Equal(DiceRoll.Parse("1d10"), blast.AmountAt(0, 0, 17));
        }

        [Fact]
        public void TheBonusActionSpellsSpendTheBonusAction()
        {
            foreach (string id in new[] { "hex", "hunters_mark", "misty_step", "spiritual_weapon",
                                          "lesser_restoration" })
                Assert.Equal(CastingTime.BonusAction, Book.Find(id).CastingTime);
        }


        // --- the hard rule --------------------------------------------------------------------------

        [Fact]
        public void TheSrdNameListIsThereAndKnowsTheObviousOnes()
        {
            Assert.True(SrdSpellNames.All.Count > 300, SrdSpellNames.All.Count.ToString());

            Assert.True(SrdSpellNames.IsSrd("Fireball"));
            Assert.True(SrdSpellNames.IsSrd("hunter's mark"));
            Assert.False(SrdSpellNames.IsSrd("Drowse"));
        }

        // THE EXCEPTIONS TO THE HARD RULE (decisions_checklist.md section 1): approximations that
        // show their SRD name anyway, each approved by Kathleen 2026-09-25. their descriptions keep
        // "(v1 ships a bounded version of this spell.)" - CC-BY asks us to say what changed
        public static readonly IReadOnlyDictionary<string, string> KeptUnderTheirSrdNames =
            new Dictionary<string, string>
            {
                ["find_familiar"] = "fixed familiar archetypes and a scouting menu, not any creature - close enough; approved by Kathleen 2026-09-25",
                ["dominate_monster"] = "a short command set, not full obedience - close enough; approved by Kathleen 2026-09-25",
                ["wish"] = "duplicates a spell or picks from an authored menu, no free-text wish - close enough; approved by Kathleen 2026-09-25",
                ["polymorph"] = "curated form cards, not any beast's statblock - close enough; approved by Kathleen 2026-09-25",
                ["shapechange"] = "curated high-level form cards, not any creature - close enough; approved by Kathleen 2026-09-25",
                ["fly"] = "no altitude, and no extra creature for a higher slot - approved by Kathleen 2026-09-25",
                ["gaseous_form"] = "can't share another creature's square (one piece to a square) - approved by Kathleen 2026-09-25",
                ["slow"] = "no 25 percent failure for spells with a Somatic component (v1 doesn't track components) - approved by Kathleen 2026-09-25",
            };

        [Fact]
        public void TheAllowListIsTheApprovedEightAndEachSaysWhatChanged()
        {
            IReadOnlyDictionary<string, string> english =
                Locale.Read(System.IO.File.ReadAllText("game.csv"));

            Assert.Equal(8, KeptUnderTheirSrdNames.Count);

            foreach (string id in KeptUnderTheirSrdNames.Keys)
            {
                Spell spell = Book.Find(id);

                Assert.True(spell != null && spell.Approximated, id);
                Assert.True(SrdSpellNames.IsSrd(english[spell.NameKey]), id);
                Assert.Contains("(v1 ships a bounded version of this spell.)",
                                english[KeyConventions.Key(KeyConventions.SpellNs, id, "description")]);
            }
        }

        [Fact]
        public void NoApproximationIsShownUnderAnSrdName()
        {
            // decisions_checklist.md section 1: a 5e spell name is a promise. a spell that does
            // not do what the SRD spell does ships under a new name, and this is the check that
            // makes that mechanical rather than a thing somebody has to remember
            IReadOnlyDictionary<string, string> english =
                Locale.Read(System.IO.File.ReadAllText("game.csv"));

            foreach (Spell spell in Book.Renamed.Where(s => !KeptUnderTheirSrdNames.ContainsKey(s.Id)))
            {
                Assert.True(english.TryGetValue(spell.NameKey, out string name),
                            $"{spell.Id} has no English name");

                Assert.False(SrdSpellNames.IsSrd(name),
                             $"{spell.Id} is an approximation (or not in the SRD) and is shown as " +
                             $"'{name}', which is an SRD spell's name. rename it in game/locale/game.csv");
            }
        }

        [Fact]
        public void EveryFaithfulSpellIsShownUnderItsSrdName()
        {
            // the other half of the rule (1b of the 2026-09-24 run): a spell that claims to be the
            // SRD spell wears a name the SRD list has. a name missing from spell_names.json fails
            // here - which is either a renamed spell that forgot its flag, or a gap in the list
            IReadOnlyDictionary<string, string> english =
                Locale.Read(System.IO.File.ReadAllText("game.csv"));

            foreach (Spell spell in Book.All.Where(s => !s.Renamed))
                Assert.True(SrdSpellNames.IsSrd(english[spell.NameKey]),
                            $"{spell.Id} is shown as '{english[spell.NameKey]}', which is not in " +
                            "content/srd/reference/spell_names.json");
        }

        [Fact]
        public void AFaithfulSpellKeepsItsSrdName()
        {
            IReadOnlyDictionary<string, string> english =
                Locale.Read(System.IO.File.ReadAllText("game.csv"));

            foreach (string id in new[] { "shield", "counterspell", "lightning_bolt", "cone_of_cold",
                                          "fireball", "power_word_kill" })
                Assert.True(SrdSpellNames.IsSrd(english[KeyConventions.SpellName(id)]), id);
        }


        // --- Extra Attack, Action Surge, Cunning Action ------------------------------------------------

        static readonly Library Srd = Library.Srd();

        static Hero Built(string cls, string species, string background, int level,
                          params Skill[] skills)
        {
            var hero = new Hero("Tess", Srd.Class(cls), Srd.Kind(species), Srd.Background(background),
                                Creation.Creation.Standard(Srd.Class(cls)), level);

            hero.Build(new Dictionary<Ability, int>
                       {
                           [Srd.Class(cls).Priority[0]] = 2,
                           [Ability.Constitution] = 1,
                       },
                       skills, null, Srd.Items);

            return hero;
        }

        [Fact]
        public void AFighterHasThreeActionsFromLevelFiveAndAnActionSurge()
        {
            Hero four = Built("fighter", "human", "soldier", 4, Skill.Athletics, Skill.Perception);
            Hero five = Built("fighter", "human", "soldier", 5, Skill.Athletics, Skill.Perception);

            Assert.Equal(2, four.Budget.ActionsFor(3));
            Assert.Equal(3, five.Budget.ActionsFor(3));

            var turn = new Turn(five.Actor, five.Budget, 3);

            Assert.True(turn.Surge());
            Assert.Equal(4, turn.Actions);
        }

        [Fact]
        public void BarbarianAndPaladinGetExtraAttackAtFiveAndTheCastersDoNot()
        {
            Assert.Equal(3, Built("barbarian", "orc", "guard", 5, Skill.Athletics, Skill.Survival)
                                .Budget.ActionsFor(2));

            Assert.Equal(3, Built("paladin", "human", "acolyte", 5, Skill.Athletics, Skill.Insight)
                                .Budget.ActionsFor(2));

            foreach (CharacterClass cls in Srd.Classes.Where(c => c.Casts && c.Id != "paladin"))
                Assert.DoesNotContain(cls.Features, f => f.Trait == Trait.ActionGrant &&
                                                         f.Uses == 0 && f.Grants == Grants.Action);
        }

        [Fact]
        public void CunningActionIsDashDisengageAndHideOnTheBonusActionNotAnExtraOne()
        {
            Hero rogue = Built("rogue", "halfling", "criminal", 2, Skill.Stealth, Skill.Acrobatics,
                               Skill.Perception, Skill.Investigation);

            Assert.Equal(1, rogue.Budget.BonusActionsFor(2));
            Assert.Equal(2, rogue.Budget.ActionsFor(2));
            Assert.Equal(Manoeuvre.Dash | Manoeuvre.Disengage | Manoeuvre.Hide,
                         rogue.Actor.QuickOnBonus);
        }

        [Fact]
        public void AClassMayGrantAnActionEveryRoundButNotMoreThanATurnHolds()
        {
            // the old rule refused any standing extra action; that was the wrong reading
            ClassReader.TryRead(@"{""classes"":[{""id"":""test"",""hit_die"":""d10"",
              ""companion"":""x"",""subclass"":""y"",""features"":[
              {""id"":""extra"",""trait"":""action_grant"",""level"":5,""count"":1}]}]}",
                                out _, out IReadOnlyList<string> fine);

            Assert.DoesNotContain(fine, p => p.Contains("extra"));

            ClassReader.TryRead(@"{""classes"":[{""id"":""test"",""hit_die"":""d10"",
              ""companion"":""x"",""subclass"":""y"",""features"":[
              {""id"":""greedy"",""trait"":""action_grant"",""level"":5,""count"":3}]}]}",
                                out _, out IReadOnlyList<string> greedy);

            Assert.Contains(greedy, p => p.Contains("greedy"));
        }

        [Fact]
        public void AHeroReadiedForAFightHasItsShieldAndItsSwordArmed()
        {
            Hero wizard = Built("mage", "human", "sage", 3, Skill.Arcana, Skill.History);
            wizard.Caster.Learn(Book.Find("shield"));

            Encounter fight = Field(new ScriptedRng(10));
            fight.Enlist(wizard.Actor, new Cell(0, 1));

            wizard.ReadyFor(fight, new Incantation(fight.Resolver));

            Assert.Contains(fight.ReactionsOf(wizard.Actor), r => r.Id == "shield");
            Assert.Contains(fight.ReactionsOf(wizard.Actor), r => r is OpportunityAttack);
        }
    }
}
