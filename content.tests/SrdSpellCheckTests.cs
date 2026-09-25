using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Schema;
using Content.Sheet;
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
    // the SRD 5.2.1 check of 2026-09-25 (_design_docs/SRD_CHECK_2026-09-25.md): what the spells did
    // differently from the SRD text, now done the SRD's way - each held to its page
    public class SrdSpellCheckTests
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

        static IRng Script(params int[] rolls) =>
            new ScriptedRng(rolls.Concat(Enumerable.Repeat(1, 400)).ToArray());

        static Encounter Field(IRng rng)
        {
            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);

            return new Encounter(new StandardResolver(rng), new Battlefield(map), new CombatLog());
        }

        static Caster Wizard(out Actor actor, string id = "wizard", int level = 17,
                             Allegiance side = Allegiance.Hero)
        {
            actor = new Actor(id, level, new AbilityScores(10, 10, 14, 18, 10, 10), side);
            actor.SetHealth(new Health(80, Die.D6, level));
            actor.Armor = new ArmorProfile(ArmorWeight.Heavy, 10);

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
            goblin.Speed = 30;

            foreach (string tag in tags) goblin.Tag(tag);

            return goblin;
        }

        // the wizard at (2,2) going first, the goblin at (x,2) going second
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

        static readonly Attack Dagger = new Attack("dagger", DiceRoll.Parse("1d4"), DamageType.Piercing);


        // --- Invisibility, Greater Invisibility (SRD p.143, p.137) -------------------------------

        [Fact]
        public void InvisibilityIsTheInvisibleConditionAndEndsOnAnAttackRoll()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Assert.True(cast.Cast(wizard, Book.Find("invisibility"), Aim.At(me), fight: fight).Cast);
            Assert.True(me.Has(Condition.Invisible));
            Assert.False(fight.Sees(goblin, me));

            fight.Swing(me, goblin, Dagger);

            Assert.False(me.Has(Condition.Invisible));
        }

        [Fact]
        public void InvisibilityEndsOnCastingASpellAndOnDealingDamage()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            cast.Cast(wizard, Book.Find("invisibility"), Aim.At(me), fight: fight);
            Assert.True(cast.Cast(wizard, Book.Find("light"), Aim.On(new Cell(2, 2)), fight: fight).Cast);
            Assert.False(me.Has(Condition.Invisible));

            cast.Cast(wizard, Book.Find("invisibility"), Aim.At(me), fight: fight);
            fight.Hurt(me, goblin, goblin.Suffer(3, DamageType.Fire));
            Assert.False(me.Has(Condition.Invisible));
        }

        [Fact]
        public void GreaterInvisibilityOutlastsAttacking()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            cast.Cast(wizard, Book.Find("greater_invisibility"), Aim.At(me), fight: fight);
            fight.Swing(me, goblin, Dagger);

            Assert.True(me.Has(Condition.Invisible));
        }


        // --- Shield and Magic Missile (SRD p.161-162) -----------------------------------------------

        [Fact]
        public void ShieldAnswersMagicMissileAndTakesNoDamageFromIt()
        {
            Encounter fight = Field(Script(20, 1));
            Caster wizard = Wizard(out Actor me);
            Caster foe = Wizard(out Actor enemy, "enemy", side: Allegiance.Enemy);

            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(enemy, new Cell(6, 2));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            fight.Arm(me, new SpellReaction(wizard, Book.Find("shield"), cast));
            fight.ChooseReactionsWith(me, ReactionChoosers.WhenItHelps);

            int slots = ((SpellSlots)wizard.Resource).Remaining(1);

            Casting darts = cast.Cast(foe, Book.Find("magic_missile"), Aim.At(me, me, me), fight: fight);

            Assert.True(darts.Cast, darts.Refusal);
            Assert.Equal(80, me.Health.Current);
            Assert.Equal(slots - 1, ((SpellSlots)wizard.Resource).Remaining(1));
            Assert.Equal(15, me.ArmorClass);
        }


        // --- Hex, Hunter's Mark (SRD p.140, p.141) ---------------------------------------------------

        [Fact]
        public void AMarkMovesOnABonusActionOnlyOnceItsCreatureIsDown()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor first);
            Actor second = Goblin("second");
            fight.Field.Place(second, new Cell(5, 2));

            var cast = new Incantation(fight.Resolver);
            Spell mark = Book.Find("hunters_mark");

            Assert.True(cast.Cast(wizard, mark, Aim.At(first), fight: fight).Cast);
            Assert.NotEmpty(first.Boons.MarksFrom(me));

            Assert.False(cast.CanRepeat(wizard, mark));
            Assert.False(cast.Again(wizard, mark, Aim.At(second), fight: fight).Cast);

            first.Suffer(1000, DamageType.Force);

            Assert.True(cast.CanRepeat(wizard, mark));
            Assert.True(cast.Again(wizard, mark, Aim.At(second), fight: fight).Cast);

            Assert.NotEmpty(second.Boons.MarksFrom(me));
            Assert.Empty(first.Boons.MarksFrom(me));
            Assert.Equal("hunters_mark", me.Concentrating);
        }


        // --- Entangle (SRD p.128) -----------------------------------------------------------------------

        [Fact]
        public void EntangleCatchesEveryoneInTheSquareButItsCaster()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Casting plants = cast.Cast(wizard, Book.Find("entangle"), Aim.On(new Cell(1, 1)), fight: fight);

            Assert.True(plants.Cast, plants.Refusal);
            Assert.True(goblin.Has(Condition.Restrained));
            Assert.False(me.Has(Condition.Restrained));
        }


        // --- a minute or an hour to cast -------------------------------------------------------------------

        [Fact]
        public void IdentifyRaiseDeadAndForesightAreNotCastInAFight()
        {
            // Identify 1 minute (SRD p.142), Raise Dead 1 hour (p.157), Foresight 1 minute (p.134)
            foreach (string id in new[] { "identify", "raise_dead", "foresight", "find_familiar" })
            {
                Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out _);
                var cast = new Incantation(fight.Resolver);

                Assert.True(Book.Find(id).OutOfCombat, id);
                Assert.False(cast.Cast(wizard, Book.Find(id), Aim.At(me), fight: fight).Cast, id);
            }

            Caster free = Wizard(out Actor alone);
            Assert.True(new Incantation(new StandardResolver(Script(1)))
                            .Cast(free, Book.Find("foresight"), Aim.At(alone)).Cast);
        }


        // --- Faerie Fire, Blur (SRD p.129, p.114) ----------------------------------------------------------

        [Fact]
        public void FaerieFireHelpsOnlyAnAttackerThatCanSeeTheTarget()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            cast.Cast(wizard, Book.Find("faerie_fire"), Aim.On(new Cell(3, 1)), fight: fight);
            Assert.True(goblin.Boons.AnyAdvantageAgainst);

            Assert.Equal(Advantage.Advantage, Strike.Lean(me, goblin, true, attackerSees: true, targetSees: true));

            // unseen, the outline gives nothing and not seeing is disadvantage
            Assert.Equal(Advantage.Disadvantage, Strike.Lean(me, goblin, true, attackerSees: false, targetSees: true));
        }

        [Fact]
        public void BlurDoesNotFoolAnAttackerWithTruesight()
        {
            Caster wizard = Wizard(out Actor me);
            var cast = new Incantation(new StandardResolver(Script(1)));
            cast.Cast(wizard, Book.Find("blur"), Aim.At(me));

            Actor plain = Goblin("plain");
            Actor seer = Goblin("seer");
            seer.Boons.Add(new Boon("true_seeing", "true_seeing", Duration.Encounter) { Truesight = true });

            Assert.Equal(Advantage.Disadvantage, Strike.Lean(plain, me, true, true, true));
            Assert.Equal(Advantage.Flat, Strike.Lean(seer, me, true, true, true));
        }


        // --- Spiritual Weapon (SRD p.165) ------------------------------------------------------------------

        [Fact]
        public void SpiritualWeaponStrikesFromItsForceAndMovesTwentyFeetAtMost()
        {
            Encounter fight = Duel(Script(20, 1, 15), out Caster wizard, out Actor me, out Actor goblin, x: 9);
            var cast = new Incantation(fight.Resolver);
            Spell weapon = Book.Find("spiritual_weapon");

            // the force appears beside the goblin, 35 feet from the caster, and attacks it - no
            // creature named, so the one beside it
            Casting summoned = cast.Cast(wizard, weapon, Aim.On(new Cell(8, 2)), fight: fight);

            Assert.True(summoned.Cast, summoned.Refusal);
            Assert.Contains(summoned.Landings, l => l.Effect.Kind == Primitive.Damage &&
                                                    ReferenceEquals(l.Target, goblin));

            // twenty feet is four squares: (8,2) to (2,2) is six
            Assert.False(cast.Again(wizard, weapon, Aim.On(new Cell(2, 2)), fight: fight).Cast);
        }


        // --- Dispel Magic on an effect, Disintegrate (SRD p.124) -----------------------------------------------

        [Fact]
        public void DispelMagicEndsAWebOnTheBoard()
        {
            Encounter fight = Field(Script(20, 1));
            Caster wizard = Wizard(out Actor me);
            Caster foe = Wizard(out Actor enemy, "enemy", side: Allegiance.Enemy);

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(enemy, new Cell(10, 4));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            cast.Cast(foe, Book.Find("web"), Aim.On(new Cell(4, 1)), fight: fight);
            Assert.Single(fight.Zones);

            Casting dispel = cast.Cast(wizard, Book.Find("dispel_magic"),
                                       Aim.On(new Cell(5, 2)).Choosing("effect"), fight: fight);

            Assert.True(dispel.Cast, dispel.Refusal);
            Assert.Empty(fight.Zones);
            Assert.False(enemy.IsConcentrating);
        }

        [Fact]
        public void DisintegrateLeavesDustThatRaiseDeadCannotBringBack()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            goblin.Suffer(60, DamageType.Slashing);
            cast.Cast(wizard, Book.Find("disintegrate"), Aim.At(goblin).Choosing("creature"), fight: fight);

            Assert.True(goblin.IsDown);
            Assert.True(goblin.Dust);

            Casting raised = new Incantation(new StandardResolver(Script(1)))
                .Cast(wizard, Book.Find("raise_dead"), Aim.At(goblin));

            Assert.False(raised.Landings.Any(l => l.Landed && l.Effect.Revives));
            Assert.True(goblin.IsDown);
        }

        [Fact]
        public void DisintegrateDestroysACubeOfForce()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin, x: 7);
            var cast = new Incantation(fight.Resolver);

            Caster other = Wizard(out Actor friend, "friend");
            fight.Field.Place(friend, new Cell(0, 4));

            Assert.True(cast.Cast(other, Book.Find("wall_of_force"), Aim.On(new Cell(6, 1)), fight: fight).Cast);
            Assert.Single(fight.Zones);

            Casting ray = cast.Cast(wizard, Book.Find("disintegrate"),
                                    Aim.On(new Cell(7, 2)).Choosing("force"), fight: fight);

            Assert.True(ray.Cast, ray.Refusal);
            Assert.Empty(fight.Zones);
        }


        // --- Major Image (SRD p.147) -----------------------------------------------------------------------

        [Fact]
        public void MajorImageAtLevelFourNeedsNoConcentration()
        {
            Caster wizard = Wizard(out Actor me);
            var cast = new Incantation(new StandardResolver(Script(1)));

            cast.Cast(wizard, Book.Find("major_image"), Aim.On(new Cell(1, 1)), castAt: 3);
            Assert.Equal("major_image", me.Concentrating);

            cast.Release(me);
            cast.Cast(wizard, Book.Find("major_image"), Aim.On(new Cell(1, 1)), castAt: 4);
            Assert.False(me.IsConcentrating);
        }


        // --- Moonbeam (SRD p.151) --------------------------------------------------------------------------

        [Fact]
        public void MoonbeamTurnsAShapeShifterBackUntilItLeavesTheBeam()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin, x: 6);
            var cast = new Incantation(fight.Resolver);

            goblin.Assume(new Shape("bear", "wild_shape", 16, 10, 14, 11, 40));
            Assert.True(goblin.IsShifted);

            Spell beam = Book.Find("moonbeam");
            Assert.True(cast.Cast(wizard, beam, Aim.On(new Cell(6, 2)), fight: fight).Cast);

            Assert.False(goblin.IsShifted);
            Assert.True(goblin.Boons.NoShifting);

            // the beam moved off it: free to shift again
            Assert.True(cast.Again(wizard, beam, Aim.On(new Cell(9, 4)), fight: fight).Cast);
            Assert.False(goblin.Boons.NoShifting);
        }


        // --- saves by creature type: Shatter, Blight (SRD p.161, p.113) ------------------------------------

        [Fact]
        public void AConstructSavesAgainstShatterWithDisadvantage()
        {
            // the two dice are a 20 and a 1; with disadvantage the 1 counts
            Caster wizard = Wizard(out _);
            Actor golem = Goblin("golem", tags: "construct");
            Actor plain = Goblin("plain");

            Casting onGolem = new Incantation(new StandardResolver(Script(20, 1)))
                .Cast(wizard, Book.Find("shatter"), Aim.At(golem));
            Casting onPlain = new Incantation(new StandardResolver(Script(20, 1)))
                .Cast(wizard, Book.Find("shatter"), Aim.At(plain));

            Assert.True(onGolem.Landings.Single().Attempt.Failed);
            Assert.True(onPlain.Landings.Single().Attempt.Succeeded);
        }

        [Fact]
        public void APlantFailsItsSaveAgainstBlight()
        {
            Caster wizard = Wizard(out _);
            Actor shrub = Goblin("shrub", tags: "plant");

            Casting blight = new Incantation(new StandardResolver(Script(20)))
                .Cast(wizard, Book.Find("blight"), Aim.At(shrub));

            Assert.True(blight.Landings.Single().Attempt.Failed);
        }


        // --- Counterspell needs sight (SRD p.120), Hypnotic Pattern needs sight of the pattern (p.141) -------

        [Fact]
        public void ABlindedCasterCannotCounterspell()
        {
            Encounter fight = Field(Script(20, 1));
            Caster wizard = Wizard(out Actor me);
            Caster foe = Wizard(out Actor enemy, "enemy", side: Allegiance.Enemy);

            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(enemy, new Cell(6, 2));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            var counter = new SpellReaction(wizard, Book.Find("counterspell"), cast);

            me.Apply(Condition.Blinded);
            Assert.False(counter.CanAnswer(fight, me, Moment.Cast(enemy, "fire_bolt", 0)));

            me.Remove(Condition.Blinded);
            Assert.True(counter.CanAnswer(fight, me, Moment.Cast(enemy, "fire_bolt", 0)));
        }

        const string Wide = @"
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . . . . . . . . . . . . . . . . . .|
+ + + + + + + + + + + + + + + + + + + + + +
|. . . . . . . . . . . . . . . . . . . . .|
+ + + + + + + + + + + + + + + + + + + + + +
|. . . . . . . . . . . . . . . . . . . . .|
+ + + + + + + + + + + + + + + + + + + + + +
|. . . . . . . . . . . . . . . . . . . . .|
+ + + + + + + + + + + + + + + + + + + + + +
|. . . . . . . . . . . . . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+";

        [Fact]
        public void HypnoticPatternMissesACreatureThatCannotSeeIt()
        {
            Assert.True(MapReader.TryRead(Wide, out MapLayout wide, out string problem), problem);
            var fight = new Encounter(new StandardResolver(Script(20, 1)), new Battlefield(wide), new CombatLog());
            Caster wizard = Wizard(out Actor me);
            Caster foe = Wizard(out Actor enemy, "enemy", side: Allegiance.Enemy);
            Actor inFog = Goblin("in_fog");
            Actor clear = Goblin("clear");

            fight.Enlist(me, new Cell(0, 4));
            fight.Enlist(enemy, new Cell(0, 0));
            fight.Enlist(inFog, new Cell(14, 2));
            fight.Enlist(clear, new Cell(18, 2));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);

            // the enemy's fog, 20 feet around (10,2), takes in the one goblin: from inside a heavily
            // obscured square nothing is seen
            Assert.True(cast.Cast(foe, Book.Find("fog_cloud"), Aim.On(new Cell(10, 2)), fight: fight).Cast);

            // a 30-foot cube centred on (16,2) covers both goblins
            Assert.True(cast.Cast(wizard, Book.Find("hypnotic_pattern"), Aim.On(new Cell(16, 2)), fight: fight).Cast);

            Assert.True(clear.Has(Condition.Charmed));
            Assert.False(inFog.Has(Condition.Charmed));
        }


        // SRD 5.2.1 Charmed (p.178): nothing hostile at the charmer - a curse with no save included,
        // while a blessing still may
        [Fact]
        public void ACharmedCasterCannotCurseTheCharmerButMayBlessIt()
        {
            Encounter fight = Field(Script());
            Caster wizard = Wizard(out Actor me);
            Actor charmer = Goblin("charmer");

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(charmer, new Cell(1, 0));
            fight.Begin();

            me.Apply(Condition.Charmed, charmer);

            var cast = new Incantation(fight.Resolver);

            Assert.False(cast.Cast(wizard, Book.Find("hex"), Aim.At(charmer), fight: fight).Cast);
            Assert.True(cast.Cast(wizard, Book.Find("bless"), Aim.At(charmer), fight: fight).Cast);
        }


        // --- Enhance Ability (SRD p.127) -------------------------------------------------------------------

        [Fact]
        public void EnhanceAbilityCannotChooseConstitution()
        {
            Caster wizard = Wizard(out Actor me);
            var cast = new Incantation(new StandardResolver(Script(1)));

            Assert.False(cast.Cast(wizard, Book.Find("enhance_ability"), Aim.At(me).Choosing(Ability.Constitution)).Cast);
            Assert.True(cast.Cast(wizard, Book.Find("enhance_ability"), Aim.At(me).Choosing(Ability.Strength)).Cast);
        }


        // --- Web (SRD p.174) ----------------------------------------------------------------------------------

        [Fact]
        public void WebsHoldOnlyWhileTheCreatureIsInThem()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin, x: 9);
            var cast = new Incantation(fight.Resolver);

            Assert.True(cast.Cast(wizard, Book.Find("web"), Aim.On(new Cell(4, 1)), fight: fight).Cast);
            IZone web = fight.Zones.Single();
            Assert.Equal(Obscurement.Light, web.Obscures);

            // it has to walk in (or start its turn there) to be caught: pushed into the webs
            fight.Shove(goblin, new Cell(5, 3));
            Assert.True(goblin.Has(Condition.Restrained));

            // and pushed out of them, it is free
            fight.Shove(goblin, new Cell(9, 3));
            Assert.False(goblin.Has(Condition.Restrained));
        }


        // --- Chain Lightning (SRD p.114) ---------------------------------------------------------------------

        [Fact]
        public void ChainLightningLeapsOnlyWithinThirtyFeetOfItsFirstTargetAndOnlyOnceEach()
        {
            Encounter fight = Field(Script(20, 1, 1, 1));
            Caster wizard = Wizard(out Actor me);
            Actor first = Goblin("first");
            Actor near = Goblin("near");
            Actor far = Goblin("far");

            fight.Enlist(me, new Cell(0, 0));
            fight.Enlist(first, new Cell(2, 2));
            fight.Enlist(near, new Cell(6, 2));
            fight.Enlist(far, new Cell(10, 4));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            Spell bolt = Book.Find("chain_lightning");

            Assert.False(cast.Cast(wizard, bolt, Aim.At(first, far), fight: fight).Cast);
            Assert.False(cast.Cast(wizard, bolt, Aim.At(first, near, near), fight: fight).Cast);
            Assert.True(cast.Cast(wizard, bolt, Aim.At(first, near), fight: fight).Cast);
        }


        // --- Finger of Death (SRD p.131) ---------------------------------------------------------------------

        [Fact]
        public void AHumanoidKilledByFingerOfDeathRisesAtTheStartOfTheCastersNextTurn()
        {
            Encounter fight = Field(Script(20, 1, 1));
            Caster wizard = Wizard(out Actor me);
            Actor bandit = Goblin("bandit", hp: 30, tags: "humanoid");
            Actor other = Goblin("other");

            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(bandit, new Cell(4, 2));
            fight.Enlist(other, new Cell(10, 4));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);

            var risen = new List<(Actor Corpse, string Statblock, Actor Master)>();
            fight.Rising += (corpse, statblock, master) => risen.Add((corpse, statblock, master));

            Turn mine = fight.Next();
            Assert.Same(me, mine.Actor);
            cast.Cast(wizard, Book.Find("finger_of_death"), Aim.At(bandit), turn: mine, fight: fight);
            Assert.True(bandit.IsDown);
            Assert.Empty(risen);

            Turn turn;

            do
            {
                fight.EndTurn();
                turn = fight.Next();
            }
            while (turn != null && !ReferenceEquals(turn.Actor, me));

            Assert.Single(risen);
            Assert.Same(bandit, risen[0].Corpse);
            Assert.Equal("zombie", risen[0].Statblock);
            Assert.Same(me, risen[0].Master);
        }

        [Fact]
        public void TheRisenZombieJoinsTheHerosSide()
        {
            Library srd = Library.Srd();
            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);

            Content.Classes.CharacterClass mage = srd.Class("mage");
            var hero = new Hero("Tess", mage, srd.Kind("human"), srd.Background("soldier"),
                                Creation.Creation.Standard(mage), 13);
            hero.Build(null, mage.SkillChoices.Take(mage.SkillPicks).ToList(), null, srd.Items);

            Battle battle = Battle.Set(srd, map, hero,
                                       new[]
                                       {
                                           new Battle.Foe(srd.Bestiary.Find("bandit"), new Cell(4, 2)),
                                           new Battle.Foe(srd.Bestiary.Find("bandit"), new Cell(9, 4)),
                                       },
                                       new StandardResolver(Script(20, 1, 1)));

            Actor corpse = battle.Monsters.First(a => battle.Fight.Field.Where(a) == new Cell(4, 2));
            corpse.Suffer(1000, DamageType.Necrotic);

            battle.Fight.Raise(corpse, "zombie", hero.Actor);

            Actor zombie = battle.Allies.Single();
            Assert.Equal(Allegiance.Hero, zombie.Side);
            Assert.Equal(new Cell(4, 2), battle.Fight.Field.Where(zombie));
            Assert.NotNull(battle.BrainOf(zombie));
            Assert.DoesNotContain(zombie, battle.Monsters);
            Assert.Contains(battle.Fight.Actors, a => ReferenceEquals(a, zombie));
        }


        // --- Dimension Door (SRD p.123-124) ------------------------------------------------------------------

        [Fact]
        public void DimensionDoorTakesACreatureFromBesideYouAlong()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin, x: 9);
            var friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            friend.SetHealth(new Health(20));
            fight.Field.Place(friend, new Cell(2, 3));

            var cast = new Incantation(fight.Resolver);
            Casting door = cast.Cast(wizard, Book.Find("dimension_door"),
                                     new Aim(new[] { friend }, new Cell(6, 0)), fight: fight);

            Assert.True(door.Cast, door.Refusal);
            Assert.Equal(new Cell(6, 0), fight.Field.Where(me));
            Assert.Equal(1, Battlefield.Distance(new Cell(6, 0), fight.Field.Where(friend).Value));
        }

        [Fact]
        public void DimensionDoorIntoAnOccupiedSpaceHurtsAndFails()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin, x: 9);
            var cast = new Incantation(fight.Resolver);

            cast.Cast(wizard, Book.Find("dimension_door"), Aim.On(new Cell(9, 2)), fight: fight);

            Assert.Equal(new Cell(2, 2), fight.Field.Where(me));
            Assert.Equal(80 - 4, me.Health.Current);
        }


        // --- Wall of Fire's burns (SRD p.172) ----------------------------------------------------------------

        [Fact]
        public void WallOfFireBurnsOnTheFirstEntryOfATurnAndOnEndingItThere()
        {
            // a wall running south from (5,0); the goblin at (7,2) walks into it, out, and back in,
            // and ends its turn in it: one burn for entering and one for ending there - 5d8 of ones
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin, x: 7);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            Assert.True(cast.Cast(wizard, Book.Find("wall_of_fire"),
                                  new Aim(null, new Cell(5, 0), Facing.South).Choosing("line").On(Side.Right),
                                  turn: mine, fight: fight).Cast);
            fight.EndTurn();

            Turn theirs = fight.Next();
            fight.Walk(theirs, new Cell(5, 2));
            fight.Walk(theirs, new Cell(4, 2));
            fight.Walk(theirs, new Cell(5, 2));

            Assert.Equal(95, goblin.Health.Current);

            fight.EndTurn();

            Assert.Equal(90, goblin.Health.Current);
        }


        // --- Flesh to Stone's full minute (SRD p.133) -----------------------------------------------------------

        [Fact]
        public void FleshToStoneHeldTheFullMinuteLeavesTheStone()
        {
            Encounter fight = Duel(Script(20, 1), out Caster wizard, out Actor me, out Actor goblin);
            var cast = new Incantation(fight.Resolver);

            Turn mine = fight.Next();
            cast.Cast(wizard, Book.Find("flesh_to_stone"), Aim.At(goblin), turn: mine, fight: fight);

            Turn turn = mine;

            while (turn != null && !(ReferenceEquals(turn.Actor, me) && fight.Round >= 11))
            {
                fight.EndTurn();
                turn = fight.Next();
            }

            Assert.True(goblin.Has(Condition.Petrified));
            Assert.False(me.IsConcentrating);

            cast.Release(me);
            Assert.True(goblin.Has(Condition.Petrified));
        }


        // --- Foresight (SRD p.134) ---------------------------------------------------------------------------

        [Fact]
        public void CastingForesightAgainEndsTheFirst()
        {
            // at will, so the one 9th-level slot isn't what stops the second cast
            var me = new Actor("wizard", 17, new AbilityScores(10, 10, 14, 18, 10, 10), Allegiance.Hero);
            var wizard = new Caster(me, Ability.Intelligence, new AtWill());
            wizard.Learn(Book.Find("foresight"));
            Actor friend = new Actor("friend", 1, new AbilityScores(), Allegiance.Hero);
            var cast = new Incantation(new StandardResolver(Script(1)));

            cast.Cast(wizard, Book.Find("foresight"), Aim.At(me));
            Assert.True(me.Boons.Has("foresight"));

            cast.Cast(wizard, Book.Find("foresight"), Aim.At(friend));
            Assert.False(me.Boons.Has("foresight"));
            Assert.True(friend.Boons.Has("foresight"));
        }
    }
}
