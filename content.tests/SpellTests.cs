using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;

namespace Content.Tests
{
    public class SpellFileTests
    {
        static readonly SpellBook Book = SpellBook.Srd();

        [Fact]
        public void EverySrdSpellFileLoadsWithoutAProblem() =>
            Assert.True(Book.Sound, string.Join("\n", Book.Problems));

        [Fact]
        public void TheMustListIsAllThere()
        {
            // the 62 MUST spells of v1_spell_list.md. this is the list, and the file is the
            // implementation - if they disagree, one of them is wrong and the test says which
            string[] must =
            {
                "eldritch_blast", "fire_bolt", "guidance", "light", "mage_hand", "minor_illusion",
                "prestidigitation", "sacred_flame", "vicious_mockery",

                "bless", "burning_hands", "charm_person", "cure_wounds", "detect_magic",
                "disguise_self", "entangle", "find_familiar", "guiding_bolt", "hex",
                "hunters_mark", "mage_armor", "magic_missile", "shield", "sleep", "thunderwave",

                "hold_person", "invisibility", "lesser_restoration", "mirror_image", "misty_step",
                "scorching_ray", "shatter", "spiritual_weapon", "web",

                "counterspell", "dispel_magic", "fireball", "fly", "haste", "hypnotic_pattern",
                "lightning_bolt", "slow", "spirit_guardians",

                "banishment", "dimension_door", "greater_invisibility", "wall_of_fire",

                "cone_of_cold", "greater_restoration", "hold_monster", "wall_of_force",

                "chain_lightning", "disintegrate", "heal", "sunbeam",

                "finger_of_death", "teleport",

                "dominate_monster", "sunburst",

                "meteor_swarm", "power_word_kill", "wish",
            };

            Assert.Equal(62, must.Length);

            string[] missing = must.Where(id => !Book.Has(id)).ToArray();

            Assert.True(missing.Length == 0, "not in the spell files: " + string.Join(", ", missing));
        }

        [Fact]
        public void EverySpellHasANameAndADescriptionKeyThatFitTheGrammar()
        {
            foreach (string key in Book.Keys())
                Assert.True(Core.Localization.KeyConventions.IsWellFormed(key),
                            Core.Localization.KeyConventions.Explain(key));
        }

        [Fact]
        public void TheShouldListIsThereWithTheTwoTheSrdDoesNotHaveUnderNewNames()
        {
            // the 69 SHOULD spells of v1_spell_list.md. Dissonant Whispers and Dragon's Breath are
            // not in SRD 5.2.1; decisions_checklist.md section 1 (2026-09-24) keeps them as working
            // spells under original names (Murmur of Dread, Wyrmbreath Boon) rather than dropping
            // them
            string[] should =
            {
                "produce_flame", "ray_of_frost", "shillelagh", "shocking_grasp",
                "spare_the_dying", "thaumaturgy", "true_strike",

                "chromatic_orb", "command", "divine_smite", "faerie_fire", "feather_fall",
                "fog_cloud", "goodberry", "healing_word", "hellish_rebuke", "hideous_laughter",
                "identify", "searing_smite", "silent_image", "speak_with_animals",

                "aid", "blindness_deafness", "blur", "darkness", "darkvision", "enhance_ability",
                "enlarge_reduce", "flaming_sphere", "heat_metal", "moonbeam",
                "pass_without_trace", "spike_growth", "suggestion",

                "call_lightning", "fear", "gaseous_form", "major_image", "remove_curse",
                "sleet_storm", "speak_with_dead", "stinking_cloud",

                "black_tentacles", "blight", "death_ward", "ice_storm", "polymorph", "stoneskin",
                "vitriolic_sphere",

                "cloudkill", "flame_strike", "raise_dead", "telekinesis",

                "blade_barrier", "flesh_to_stone", "globe_of_invulnerability", "true_seeing",

                "forcecage", "plane_shift", "reverse_gravity",

                "antimagic_field", "earthquake", "maze", "power_word_stun",

                "foresight", "shapechange", "true_polymorph",
            };

            Assert.Equal(67, should.Length);

            string[] missing = should.Where(id => !Book.Has(id)).ToArray();

            Assert.True(missing.Length == 0, "not in the spell files: " + string.Join(", ", missing));

            Assert.Equal(62 + 69, Book.Count);
            Assert.True(Book.Find("dissonant_whispers").NotInSrd);
            Assert.True(Book.Find("dragons_breath").NotInSrd);
        }

        [Fact]
        public void OnlyTheFlaggedSpellsAreApproximations()
        {
            // THE TRIPWIRE ON HOW FAITHFUL V1 IS. it used to say 17, and 17 was an undercount: it
            // flagged the spells that were wrong because v1 had one shape and one reaction, and
            // missed a dozen that were wrong for other reasons (a missing modifier, a missing
            // choice, a condition v1 does not have). the 2026-09-23 audit checked every spell
            // against SRD 5.2.1. lines, cones, cubes, a real reaction window, the new sway fields
            // and zones that act made Lightning Bolt, Cone of Cold, Shield, Counterspell, Guiding
            // Bolt, Hex, Invisibility, Spirit Guardians, Web and Entangle faithful. what is left is
            // below, and every one of them ships under a new name
            // (NoApproximationIsShownUnderAnSrdName). if this list moves, somebody changed how
            // faithful v1 is and the build notes need the same edit.
            //
            // 2026-09-24 (the unattended run): new conditions, damage-triggered endings, escalating
            // saves, hit-point and creature-type gates, modes, delayed damage and the rest made
            // 17 more faithful - Hold Person, Hold Monster, Sunbeam, Sunburst, Charm Person, Sleep,
            // Hypnotic Pattern, Hideous Laughter, Blindness/Deafness, Flesh to Stone, Power Word
            // Stun, Aid, Death Ward, Raise Dead, True Seeing, Black Tentacles, Vitriolic Sphere;
            // then obscuring zones, decoys, turn limits, weapon rewrites, the after-your-hit bonus
            // action and the rest made 15 more - Mirror Image, Haste, Greater Restoration,
            // Shillelagh, Spare the Dying, Divine Smite, Searing Smite, Fog Cloud, Darkness,
            // Enlarge/Reduce, Remove Curse, Sleet Storm, Stinking Cloud, Cloudkill, Globe of
            // Invulnerability
            string[] must =
            {
                "find_familiar", "fly", "slow", "banishment", "wall_of_force", "teleport",
                "dominate_monster", "wish",
            };

            string[] should =
            {
                "feather_fall", "heat_metal", "suggestion", "gaseous_form", "polymorph",
                "plane_shift", "reverse_gravity", "antimagic_field", "earthquake", "maze",
                "shapechange", "true_polymorph",
            };

            Assert.Equal(8, must.Length);
            Assert.Equal(12, should.Length);

            Assert.Equal(must.Concat(should).OrderBy(id => id),
                         Book.Approximations.Select(s => s.Id).OrderBy(id => id));
        }

        // WHAT A SPELL COSTS IS NO LONGER A PROPERTY OF THE SPELL. It used to be its level, in
        // mana, because there was one pool and one price. Now the price depends on the mode the
        // player chose, so the spell carries its LEVEL and the resource decides what that costs -
        // which is why SpellResourceTests owns the prices and this file no longer mentions them.
        [Fact]
        public void ACantripIsLevelZeroAndALeveledSpellCarriesItsLevel()
        {
            Assert.Equal(0, Book.Find("fire_bolt").Level);
            Assert.True(Book.Find("fire_bolt").IsCantrip);

            Assert.Equal(3, Book.Find("fireball").Level);
            Assert.False(Book.Find("fireball").IsCantrip);
        }

        [Fact]
        public void ConcentrationIsDeclaredOnTheSpellAndOnItsEffects()
        {
            foreach (Spell spell in Book.All)
            {
                bool holds = spell.Effects.Any(e => e.Duration == Duration.Concentration);

                if (holds)
                    Assert.True(spell.Concentration,
                                $"{spell.Id} holds an effect but is not marked concentration");
            }
        }

        [Fact]
        public void EveryDamagingSpellSaysWhatTypeOfDamageItDeals()
        {
            foreach (Spell spell in Book.All)
                foreach (SpellEffect effect in spell.Effects.Where(e => e.Kind == Primitive.Damage))
                    Assert.True(effect.DamageType != DamageType.None || effect.ChosenDamageType,
                                spell.Id);
        }

        [Fact]
        public void NoSpellBothRollsToHitAndCallsForASave()
        {
            foreach (Spell spell in Book.All)
                foreach (SpellEffect effect in spell.Effects)
                    Assert.False(effect.AttackRoll && effect.Save.HasValue, spell.Id);
        }
    }

    public class SpellReaderTests
    {
        static IReadOnlyList<string> Problems(string json)
        {
            SpellReader.TryRead(json, out _, out IReadOnlyList<string> problems);
            return problems;
        }

        [Fact]
        public void RefusesDamageWithNoType()
        {
            IReadOnlyList<string> problems = Problems(@"
            {""spells"":[{""id"":""bad"",""level"":1,""school"":""evocation"",
              ""effects"":[{""primitive"":""damage"",""amount"":""2d6""}]}]}");

            Assert.Contains(problems, p => p.Contains("damage_type"));
        }

        [Fact]
        public void RefusesAPrimitiveThatIsNotOnTheList()
        {
            IReadOnlyList<string> problems = Problems(@"
            {""spells"":[{""id"":""bad"",""level"":1,
              ""effects"":[{""primitive"":""mind_control""}]}]}");

            Assert.Contains(problems, p => p.Contains("not a primitive"));
        }

        [Fact]
        public void RefusesAnIdThatWouldMakeABadKey()
        {
            Assert.Contains(Problems(@"{""spells"":[{""id"":""Fire Bolt"",""level"":0}]}"),
                            p => p.Contains("not a spell id"));
        }

        [Fact]
        public void RefusesCantripScalingOnALeveledSpell()
        {
            IReadOnlyList<string> problems = Problems(@"
            {""spells"":[{""id"":""bad"",""level"":3,""effects"":[
              {""primitive"":""damage"",""amount"":""8d6"",""damage_type"":""fire"",
               ""cantrip_scaling"":true}]}]}");

            Assert.Contains(problems, p => p.Contains("cantrip_scaling"));
        }

        [Fact]
        public void UnconsciousMayBeNamedNowThatSleepPutsCreaturesThere()
        {
            // it used to be refused as the engine's own. SRD 5.2.1 Sleep puts a creature
            // Unconscious without it dropping to 0, so content may name it (2026-09-24)
            IReadOnlyList<string> problems = Problems(@"
            {""spells"":[{""id"":""fine"",""level"":1,""effects"":[
              {""primitive"":""afflict"",""condition"":""unconscious""}]}]}");

            Assert.DoesNotContain(problems, p => p.Contains("condition"));
        }

        [Fact]
        public void RefusesAConditionThatIsNotOne()
        {
            IReadOnlyList<string> problems = Problems(@"
            {""spells"":[{""id"":""bad"",""level"":1,""effects"":[
              {""primitive"":""afflict"",""condition"":""exhausted""}]}]}");

            Assert.Contains(problems, p => p.Contains("not a v1 condition"));
        }

        [Fact]
        public void RefusesASwayThatTouchesNothing()
        {
            IReadOnlyList<string> problems = Problems(@"
            {""spells"":[{""id"":""bad"",""level"":1,""effects"":[
              {""primitive"":""sway"",""sway"":2}]}]}");

            Assert.Contains(problems, p => p.Contains("touches nothing"));
        }

        [Fact]
        public void RefusesAnEffectThatBothAttacksAndSaves()
        {
            IReadOnlyList<string> problems = Problems(@"
            {""spells"":[{""id"":""bad"",""level"":1,""effects"":[
              {""primitive"":""damage"",""amount"":""1d6"",""damage_type"":""fire"",
               ""attack_roll"":true,""save"":""dex"",""on_save"":""half""}]}]}");

            Assert.Contains(problems, p => p.Contains("never both"));
        }
    }

    public class CastingTests
    {
        // eleven squares across, so a fireball centred in the middle can miss both ends
        const string Room = @"
+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+";

        static readonly SpellBook Book = SpellBook.Srd();

        // POINTS, NOT SLOTS, and on purpose: these tests are about what a spell DOES, and a
        // pool with a number in it makes "this cast cost more than that one" a readable assertion.
        // SpellResourceTests is where each mode is held to its own rules.
        static Caster Mage(out Actor actor, int level = 5, int points = 20)
        {
            actor = new Actor("mage", level, new AbilityScores(8, 14, 14, 18, 10, 10),
                              Allegiance.Hero);

            actor.SetHealth(new Health(30, Die.D6, level));

            var caster = new Caster(actor, Ability.Intelligence, new SpellPoints(points));

            foreach (Spell spell in Book.All) caster.Learn(spell);

            return caster;
        }

        static int Pool(Caster caster) => ((SpellPoints)caster.Resource).Remaining;

        static Actor Dummy(string id = "dummy", int hp = 100, int ac = 10)
        {
            var actor = new Actor(id);
            actor.SetHealth(new Health(hp));
            actor.Armor = new ArmorProfile(ArmorWeight.Heavy, ac);
            return actor;
        }

        static Encounter Field(out Battlefield field, IRng rng)
        {
            Assert.True(MapReader.TryRead(Room, out MapLayout map, out string problem), problem);

            field = new Battlefield(map);

            return new Encounter(new StandardResolver(rng), field);
        }

        [Fact]
        public void TheSaveDcIsEightPlusProficiencyPlusTheCastingAbility()
        {
            Caster mage = Mage(out _);

            // level 5 is +3 proficiency, int 18 is +4
            Assert.Equal(15, mage.SaveDc);
            Assert.Equal(7, mage.AttackModifier);
        }

        [Fact]
        public void ACantripSpendsNoMana()
        {
            Caster mage = Mage(out Actor actor);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(18, 6)));

            Casting result = cast.Cast(mage, Book.Find("fire_bolt"), Aim.At(Dummy()));

            Assert.True(result.Cast);
            Assert.Equal(20, Pool(mage));
        }

        [Fact]
        public void ACantripGrowsWithTheCastersLevelAndNotWithTheResource()
        {
            SpellEffect bolt = Book.Find("fire_bolt").Effects[0];

            Assert.Equal(new DiceRoll(1, Die.D10), bolt.AmountAt(0, 0, 1));
            Assert.Equal(new DiceRoll(2, Die.D10), bolt.AmountAt(0, 0, 5));
            Assert.Equal(new DiceRoll(3, Die.D10), bolt.AmountAt(0, 0, 11));
            Assert.Equal(new DiceRoll(4, Die.D10), bolt.AmountAt(0, 0, 17));
        }

        [Fact]
        public void ALeveledSpellSpendsItsLevelAndUpcastingSpendsMore()
        {
            Caster mage = Mage(out Actor actor);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(3)));

            cast.Cast(mage, Book.Find("magic_missile"), Aim.At(Dummy()));

            // a 1st-level spell costs 2
            Assert.Equal(18, Pool(mage));

            // and the same spell thrown at 4th costs 6
            cast.Cast(mage, Book.Find("magic_missile"), Aim.At(Dummy()), 4);

            Assert.Equal(12, Pool(mage));
        }

        [Fact]
        public void UpcastingAddsTheDeclaredDicePerLevel()
        {
            SpellEffect burn = Book.Find("burning_hands").Effects[0];

            Assert.Equal(new DiceRoll(3, Die.D6), burn.AmountAt(1, 1, 5));
            Assert.Equal(new DiceRoll(5, Die.D6), burn.AmountAt(1, 3, 5));
        }

        [Fact]
        public void ASpellTheResourceCannotPayForIsRefusedAndNothingIsSpent()
        {
            Caster mage = Mage(out Actor actor, points: 1);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(10)));

            Casting result = cast.Cast(mage, Book.Find("fireball"), Aim.On(new Cell(3, 1)));

            Assert.False(result.Cast);
            Assert.Contains("nothing left", result.Refusal);
            Assert.Equal(1, Pool(mage));
        }

        [Fact]
        public void AFireballCatchesEverythingInItsBurstAndHalvesOnASave()
        {
            Encounter fight = Field(out Battlefield field, new ScriptedRng(20, 1, 1));
            Caster mage = Mage(out Actor actor);

            Actor caught = Dummy("caught");
            Actor safe = Dummy("safe");

            fight.Enlist(actor, new Cell(0, 0));
            fight.Enlist(caught, new Cell(5, 1));
            fight.Enlist(safe, new Cell(10, 1));
            fight.Begin();

            // the save fails (1), then 8d6 of 4s
            var rolls = new List<int> { 1 };
            for (int i = 0; i < 8; i++) rolls.Add(4);

            var cast = new Incantation(new StandardResolver(new ScriptedRng(rolls.ToArray())));

            Casting result = cast.Cast(mage, Book.Find("fireball"), Aim.On(new Cell(5, 1)),
                                       fight: fight);

            Assert.True(result.Cast);
            Assert.Contains(caught, result.Touched);

            // five squares from the burst, and a fireball reaches four
            Assert.DoesNotContain(safe, result.Touched);

            // SRD does not spare the caster either, and five squares is five squares
            Assert.DoesNotContain(actor, result.Touched);

            Assert.Equal(32, caught.Health.Maximum - caught.Health.Current);
        }

        [Fact]
        public void ASavedFireballStillBurnsForHalf()
        {
            Encounter fight = Field(out Battlefield field, new ScriptedRng(20, 1));
            Caster mage = Mage(out Actor actor);

            Actor caught = Dummy("caught");

            fight.Enlist(actor, new Cell(0, 0));
            fight.Enlist(caught, new Cell(5, 1));
            fight.Begin();

            var rolls = new List<int> { 20 };
            for (int i = 0; i < 8; i++) rolls.Add(4);

            var cast = new Incantation(new StandardResolver(new ScriptedRng(rolls.ToArray())));

            cast.Cast(mage, Book.Find("fireball"), Aim.On(new Cell(5, 1)), fight: fight);

            Assert.Equal(16, caught.Health.Maximum - caught.Health.Current);
        }

        [Fact]
        public void ABlessAddsARolledDieToEveryTargetsAttacks()
        {
            Caster mage = Mage(out Actor actor);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor ally = Dummy("ally");

            cast.Cast(mage, Book.Find("bless"), Aim.At(ally));

            Assert.True(ally.Boons.Has("bless"));

            var resolver = new StandardResolver(new ScriptedRng(4, 10));

            // the 4 is the bless die, the 10 the d20
            Attempt attempt = Core.Rules.Checks.Save(resolver, ally, Ability.Wisdom, 10);

            Assert.Equal(14, attempt.Total);
        }

        [Fact]
        public void CastingASecondConcentrationSpellDropsTheFirstAndItsBoons()
        {
            Caster mage = Mage(out Actor actor);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor ally = Dummy("ally");

            cast.Cast(mage, Book.Find("bless"), Aim.At(ally));
            Assert.True(ally.Boons.Has("bless"));
            Assert.Equal("bless", actor.Concentrating);

            cast.Cast(mage, Book.Find("guidance"), Aim.At(ally).Choosing(Skill.Perception));

            Assert.False(ally.Boons.Has("bless"));
            Assert.True(ally.Boons.Has("guidance"));
            Assert.Equal("guidance", actor.Concentrating);
        }

        [Fact]
        public void LettingGoOfAConcentrationLiftsTheConditionItHeld()
        {
            Caster mage = Mage(out Actor actor);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor victim = Dummy("victim");

            victim.Tag("humanoid");

            cast.Cast(mage, Book.Find("hold_person"), Aim.At(victim));

            Assert.True(victim.Has(Condition.Paralyzed));

            cast.Release(actor);

            Assert.False(victim.Has(Condition.Paralyzed));
            Assert.False(actor.IsConcentrating);
        }

        [Fact]
        public void GoingDownDropsWhateverTheCasterWasHolding()
        {
            Caster mage = Mage(out Actor actor);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor victim = Dummy("victim");
            victim.Tag("humanoid");

            cast.Cast(mage, Book.Find("hold_person"), Aim.At(victim));

            actor.Suffer(999, DamageType.Slashing);
            cast.Check(new[] { actor });

            Assert.False(actor.IsConcentrating);
            Assert.False(victim.Has(Condition.Stunned));
        }

        [Fact]
        public void CureWoundsHealsAndNeverPastTheMaximum()
        {
            Caster mage = Mage(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(8, 8)));

            Actor hurt = Dummy("hurt", hp: 40);
            hurt.Suffer(5, DamageType.Slashing);

            Casting result = cast.Cast(mage, Book.Find("cure_wounds"), Aim.At(hurt));

            Assert.Equal(5, result.TotalHealing);
            Assert.Equal(40, hurt.Health.Current);
        }

        [Fact]
        public void MagicMissileNeverMissesAndSplitsBetweenTargets()
        {
            Caster mage = Mage(out _);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(4)));

            Actor a = Dummy("a", ac: 30);
            Actor b = Dummy("b", ac: 30);
            Actor c = Dummy("c", ac: 30);

            Casting result = cast.Cast(mage, Book.Find("magic_missile"), Aim.At(a, b, c));

            Assert.Equal(3, result.Landings.Count);
            Assert.Equal(15, result.TotalDamage); // three darts of 4 + 1
        }

        [Fact]
        public void ASpellNotOnTheSheetIsRefused()
        {
            var actor = new Actor("novice", 1, new AbilityScores(), Allegiance.Hero);

            var empty = new Caster(actor, Ability.Intelligence, new SpellPoints(10));
            var cast = new Incantation(new StandardResolver(new ScriptedRng(10)));

            Casting result = cast.Cast(empty, Book.Find("fireball"), Aim.On(new Cell(1, 1)));

            Assert.False(result.Cast);
            Assert.Contains("sheet", result.Refusal);
        }

        [Fact]
        public void ACastCostsAnActionWhenItIsTakenOnATurn()
        {
            Encounter fight = Field(out _, new ScriptedRng(20, 1));
            Caster mage = Mage(out Actor actor);

            Actor target = Dummy();

            fight.Enlist(actor, new Cell(0, 0));
            fight.Enlist(target, new Cell(3, 1));
            fight.Begin();

            Turn turn = fight.Next();
            var cast = new Incantation(new StandardResolver(new ScriptedRng(18, 6)));

            cast.Cast(mage, Book.Find("fire_bolt"), Aim.At(target), turn: turn, fight: fight);

            Assert.Equal(1, turn.Actions);
        }
    }
}
