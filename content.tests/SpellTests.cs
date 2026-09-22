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
        public void OnlyTheFlaggedSpellsAreApproximations()
        {
            // v1_spell_list.md budgeted about eight approximations across the whole 131. the 62
            // MUST spells alone need 17, because v1 keeps one area shape and one reaction: every
            // line and cone becomes a burst and every interrupt becomes a cast. that is a real
            // divergence from the doc and this number is the tripwire for it - if it moves,
            // somebody has changed how faithful v1 is and the doc needs the same edit.
            Assert.Equal(17, Book.Approximations.Count());
        }

        [Fact]
        public void ACantripCostsNothingAndALeveledSpellCostsItsLevel()
        {
            Assert.Equal(0, Book.Find("fire_bolt").Cost);
            Assert.Equal(3, Book.Find("fireball").Cost);
            Assert.Equal(5, Book.Find("fireball").CostAt(5));
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
                    Assert.True(effect.DamageType != DamageType.None, spell.Id);
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
        public void RefusesAConditionTheEngineOwns()
        {
            IReadOnlyList<string> problems = Problems(@"
            {""spells"":[{""id"":""bad"",""level"":1,""effects"":[
              {""primitive"":""afflict"",""condition"":""unconscious""}]}]}");

            Assert.Contains(problems, p => p.Contains("engine's"));
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

        static Caster Mage(out Actor actor, int level = 5, int mana = 20)
        {
            actor = new Actor("mage", level, new AbilityScores(8, 14, 14, 18, 10, 10),
                              Allegiance.Hero);

            actor.SetHealth(new Health(30, Die.D6, level));
            actor.ManaMax = mana;
            actor.FillMana();

            var caster = new Caster(actor, Ability.Intelligence);

            foreach (Spell spell in Book.All) caster.Learn(spell);

            return caster;
        }

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
            Assert.Equal(20, actor.Mana);
        }

        [Fact]
        public void ACantripGrowsWithTheCastersLevelAndNotWithMana()
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

            Assert.Equal(19, actor.Mana);

            cast.Cast(mage, Book.Find("magic_missile"), Aim.At(Dummy()), 4);

            Assert.Equal(15, actor.Mana);
        }

        [Fact]
        public void UpcastingAddsTheDeclaredDicePerLevel()
        {
            SpellEffect burn = Book.Find("burning_hands").Effects[0];

            Assert.Equal(new DiceRoll(3, Die.D6), burn.AmountAt(1, 1, 5));
            Assert.Equal(new DiceRoll(5, Die.D6), burn.AmountAt(1, 3, 5));
        }

        [Fact]
        public void ASpellWithoutTheManaIsRefusedAndNothingIsSpent()
        {
            Caster mage = Mage(out Actor actor, mana: 1);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(10)));

            Casting result = cast.Cast(mage, Book.Find("fireball"), Aim.On(new Cell(3, 1)));

            Assert.False(result.Cast);
            Assert.Contains("mana", result.Refusal);
            Assert.Equal(1, actor.Mana);
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

            cast.Cast(mage, Book.Find("guidance"), Aim.At(ally));

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

            cast.Cast(mage, Book.Find("hold_person"), Aim.At(victim));

            Assert.True(victim.Has(Condition.Stunned));

            cast.Release(actor);

            Assert.False(victim.Has(Condition.Stunned));
            Assert.False(actor.IsConcentrating);
        }

        [Fact]
        public void GoingDownDropsWhateverTheCasterWasHolding()
        {
            Caster mage = Mage(out Actor actor);
            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));

            Actor victim = Dummy("victim");

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
            actor.ManaMax = 10;
            actor.FillMana();

            var empty = new Caster(actor, Ability.Intelligence);
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
