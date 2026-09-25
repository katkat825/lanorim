using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Tests
{
    // the SRD 5.2.1 check of 2026-09-25, the classes: what their features do now, each held to
    // its page
    public class SrdClassCheckTests
    {
        static readonly Library Srd = Library.Srd();

        static Hero Made(string cls, int level, Ability first = Ability.Strength, string species = "human",
                         string background = "soldier", int highestSpell = 0)
        {
            CharacterClass made = Srd.Class(cls);

            var hero = new Hero("Tess", made, Srd.Kind(species), Srd.Background(background),
                                Creation.Creation.Standard(made), level);

            hero.Build(new Dictionary<Ability, int> { [first] = 2, [Ability.Constitution] = 1 },
                       made.SkillChoices.Take(made.SkillPicks).ToList(), null, Srd.Items,
                       Srd.Spells.For(cls).Where(s => s.Level <= highestSpell).ToList());

            return hero;
        }

        static Feature Of(Hero hero, string id) => hero.Features.First(f => f.Id == id);

        const string Hall = @"
+-+-+-+-+-+-+-+
|@ . . . . . .|
+ + + + + + + +
|. . . . . . .|
+ + + + + + + +
|. . . . . . .|
+-+-+-+-+-+-+-+";

        static Encounter Room(Hero hero, out Actor goblin, params int[] rolls)
        {
            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);

            var fight = new Encounter(new StandardResolver(new ScriptedRng(rolls.Concat(Enumerable.Repeat(10, 200)).ToArray())),
                                      new Battlefield(map), new CombatLog());

            goblin = new Actor("goblin", 1, new AbilityScores());
            goblin.SetHealth(new Health(500));
            goblin.Armor = new ArmorProfile(ArmorWeight.Heavy, 5);

            fight.Enlist(hero.Actor, new Cell(1, 1), hero.Budget);
            fight.Enlist(goblin, new Cell(2, 1));

            return fight;
        }


        // --- Barbarian (SRD p.28-30) ---------------------------------------------------------------

        [Fact]
        public void RageResistsAndAdvantagesStrengthAndGrowsWithLevel()
        {
            Hero barbarian = Made("barbarian", 9);
            Feature rage = Of(barbarian, "rage");

            Assert.Equal(4, barbarian.UsesLeft(rage));
            Assert.True(barbarian.Invoke(rage));

            Assert.Equal(5, barbarian.Actor.Suffer(10, DamageType.Slashing));
            Assert.Equal(10, barbarian.Actor.Suffer(10, DamageType.Fire));
            Assert.Equal(Advantage.Advantage, barbarian.Actor.SaveAdvantage(Ability.Strength));
            Assert.Equal(Advantage.Advantage,
                         barbarian.Actor.CheckAdvantageFor(Ability.Strength, Skill.Athletics));

            // +3 at level 9
            Assert.Equal(3, barbarian.Actor.Boons.FlatOnDamageFor(Ability.Strength));
        }

        [Fact]
        public void AShortRestGivesOneRageBack()
        {
            Hero barbarian = Made("barbarian", 3);
            Feature rage = Of(barbarian, "rage");

            Assert.True(barbarian.Invoke(rage));
            barbarian.EndStance(rage);
            Assert.True(barbarian.Invoke(rage));
            barbarian.EndStance(rage);
            Assert.Equal(1, barbarian.UsesLeft(rage));

            barbarian.ShortRest(new StandardResolver(new ScriptedRng(1)));

            Assert.Equal(2, barbarian.UsesLeft(rage));
        }

        [Fact]
        public void RecklessAttackIsFreeAndCutsBothWays()
        {
            Hero barbarian = Made("barbarian", 2);
            Feature reckless = Of(barbarian, "reckless_attack");

            Assert.Equal(Spend.Free, reckless.Cost);
            Assert.True(barbarian.Invoke(reckless));

            Attack axe = Srd.Items.Find("greataxe").Attack;
            Attack bow = Srd.Items.Find("shortbow").Attack;

            Assert.True(barbarian.Actor.AttackLeansWith(axe).advantage);
            Assert.False(barbarian.Actor.AttackLeansWith(bow).advantage);
            Assert.True(barbarian.Actor.Boons.AnyAdvantageAgainst);
        }

        [Fact]
        public void DangerSenseIsAdvantageOnDexteritySavesWhileItCanAct()
        {
            Hero barbarian = Made("barbarian", 2);

            Assert.Equal(Advantage.Advantage, barbarian.Actor.SaveAdvantage(Ability.Dexterity));
            Assert.False(barbarian.Actor.SavesWith(Ability.Dexterity));

            barbarian.Actor.Apply(Condition.Incapacitated);
            Assert.Equal(Advantage.Flat, barbarian.Actor.SaveAdvantage(Ability.Dexterity));
        }

        [Fact]
        public void FrenzyRidesOnlyWhileRagingAndReckless()
        {
            Hero berserker = Made("barbarian", 3);
            Attack axe = Srd.Items.Find("greataxe").Attack;

            Assert.DoesNotContain(berserker.RidersFor(true, attack: axe), r => r.Id == "frenzy");

            berserker.Invoke(Of(berserker, "rage"));
            berserker.Invoke(Of(berserker, "reckless_attack"));

            Rider frenzy = berserker.RidersFor(false, attack: axe).Single(r => r.Id == "frenzy");
            Assert.Equal(new DiceRoll(2, Die.D6), frenzy.Damage);
        }

        [Fact]
        public void RelentlessRageIsAConstitutionSaveThatGetsHarder()
        {
            Hero barbarian = Made("barbarian", 11);
            barbarian.Invoke(Of(barbarian, "rage"));

            // a 20 on the first DC 10 save: back on twice the level
            barbarian.Actor.Suffer(9999, DamageType.Slashing);
            Assert.True(barbarian.Intercept(new StandardResolver(new ScriptedRng(20))));
            Assert.Equal(22, barbarian.Actor.Health.Current);

            // the second is DC 15, and a 1 fails it
            barbarian.Actor.Suffer(9999, DamageType.Slashing);
            Assert.False(barbarian.Intercept(new StandardResolver(new ScriptedRng(1))));
            Assert.True(barbarian.Actor.IsDown);
        }

        [Fact]
        public void RelentlessRageNeedsTheRage()
        {
            Hero barbarian = Made("barbarian", 11);
            barbarian.Actor.Suffer(9999, DamageType.Slashing);

            Assert.False(barbarian.Intercept(new StandardResolver(new ScriptedRng(20))));
        }


        // --- Fighter (p.47-49) ------------------------------------------------------------------------

        [Fact]
        public void TheChampionCritsOnANineteen()
        {
            Hero champion = Made("fighter", 3);
            Encounter fight = Room(champion, out Actor goblin, 19);

            Attempt roll = Strike.Roll(fight.Resolver, champion.Actor, goblin,
                                       champion.Attacks.First(), close: true);

            Assert.True(roll.IsCritical);
            Assert.True(roll.Succeeded);
        }

        [Fact]
        public void IndomitableRerollsAFailedSaveWithTheFightersLevel()
        {
            Hero fighter = Made("fighter", 9);
            Assert.Equal(1, fighter.Actor.SaveRerolls);

            // a 1, then a 10 + 9 + the save modifier
            Attempt save = Checks.Save(new StandardResolver(new ScriptedRng(1, 10)), fighter.Actor,
                                       Ability.Wisdom, 18);

            Assert.True(save.Succeeded);
            Assert.Equal(0, fighter.Actor.SaveRerolls);

            fighter.LongRest();
            Assert.Equal(1, fighter.Actor.SaveRerolls);
        }

        [Fact]
        public void TheDefenseStyleIsOneArmorClassInArmorOnly()
        {
            Hero fighter = Made("fighter", 1);

            Assert.Equal(16 + 1, fighter.Actor.ArmorClass);

            fighter.TakeOff(Content.Items.Slot.Body);
            Assert.Equal(10 + fighter.Actor.AbilityModifier(Ability.Dexterity), fighter.Actor.ArmorClass);
        }

        [Fact]
        public void SecondWindGrowsWithLevelAndComesBackOneAtATime()
        {
            Hero fighter = Made("fighter", 4);
            Feature wind = Of(fighter, "second_wind");

            Assert.Equal(3, fighter.UsesLeft(wind));
            Assert.Equal(new DiceRoll(1, Die.D10, 4), wind.AmountAt(4));
        }

        [Fact]
        public void RemarkableAthleteRollsInitiativeWithAdvantage()
        {
            Hero champion = Made("fighter", 3);

            Assert.True(champion.Actor.InitiativeAdvantage);
            Assert.Equal(Advantage.Advantage,
                         champion.Actor.CheckAdvantageFor(Ability.Strength, Skill.Athletics));
        }


        // --- Rogue (p.61-63) --------------------------------------------------------------------------

        [Fact]
        public void SneakAttackLandsOnceATurn()
        {
            Hero rogue = Made("rogue", 1, Ability.Dexterity);
            Encounter fight = Room(rogue, out Actor goblin, 20, 1);
            fight.Begin();

            Turn turn = fight.Next();
            Assert.Same(rogue.Actor, turn.Actor);

            // unseen by the goblin: advantage, so the setup is there on both swings
            goblin.Apply(Condition.Blinded);
            Attack dagger = Srd.Items.Find("dagger").Attack;

            Blow first = rogue.Hit(fight, turn, goblin, dagger);
            Blow second = rogue.Hit(fight, turn, goblin, dagger);

            Assert.Contains(first.Riders, r => r.Id == "sneak_attack");
            Assert.DoesNotContain(second.Riders, r => r.Id == "sneak_attack");
        }

        [Fact]
        public void UncannyDodgeHalvesAHit()
        {
            Hero rogue = Made("rogue", 5, Ability.Dexterity);
            Encounter fight = Room(rogue, out Actor goblin, 1, 20);
            rogue.ReadyFor(fight);
            fight.Begin();

            fight.Next();
            fight.EndTurn();
            Turn its = fight.Next();

            var club = new Attack("club", DiceRoll.Flat(10), DamageType.Bludgeoning);
            int was = rogue.Actor.Health.Current;

            Blow blow = fight.Hit(its, rogue.Actor, club);

            Assert.True(blow.Hit);
            Assert.Equal(was - 5, rogue.Actor.Health.Current);
        }

        [Fact]
        public void EvasionAndReliableTalent()
        {
            Hero rogue = Made("rogue", 7, Ability.Dexterity);

            Assert.True(rogue.Actor.Is("evasion"));

            // a 3 on a trained skill counts as a 10
            Skill trained = rogue.Class.SkillChoices.First(s => rogue.Actor.TrainingIn(s) != Training.Untrained);
            Attempt check = Checks.Check(new StandardResolver(new ScriptedRng(3)), rogue.Actor, trained, 5);

            Assert.Equal(10, check.Natural);
        }


        // --- Mage (p.77-82) ---------------------------------------------------------------------------

        [Fact]
        public void TheEvokersCantripsAndEvocationsHitHarder()
        {
            Hero evoker = Made("mage", 10, Ability.Intelligence);

            Assert.True(evoker.Actor.Is("potent_cantrip"));
            Assert.True(evoker.Actor.Is("empowered_evocation"));
            Assert.DoesNotContain(evoker.Features, f => f.Id == "overchannel" && f.Trait != Trait.Narrate);
        }


        // --- Cleric (p.36-40) ---------------------------------------------------------------------------

        [Fact]
        public void TheLifeDomainsSpellsAreAlwaysPreparedAndHealMore()
        {
            Hero cleric = Made("cleric", 3, Ability.Wisdom, highestSpell: 1);

            Assert.True(cleric.Caster.IsPrepared("bless"));
            Assert.True(cleric.Caster.IsPrepared("aid"));
            Assert.DoesNotContain(cleric.Caster.Picked, s => s.Id == "aid");

            // Cure Wounds at 1st: 2d8 of ones, + Wisdom, + Disciple of Life's 2 + 1
            cleric.Actor.Suffer(15, DamageType.Slashing);
            int was = cleric.Actor.Health.Current;

            new Incantation(new StandardResolver(new ScriptedRng(1)))
                .Cast(cleric.Caster, Srd.Spells.Find("cure_wounds"), Aim.At(cleric.Actor));

            Assert.Equal(was + 2 + cleric.Actor.AbilityModifier(Ability.Wisdom) + 3,
                         cleric.Actor.Health.Current);
        }

        [Fact]
        public void PreserveLifeSpendsChannelDivinityAndStopsAtHalf()
        {
            Hero cleric = Made("cleric", 3, Ability.Wisdom);
            Feature preserve = Of(cleric, "preserve_life");
            Feature channel = Of(cleric, "channel_divinity");

            Assert.Equal(Spend.Action, preserve.Cost);
            Assert.Equal(2, cleric.UsesLeft(channel));

            // not bloodied: nothing to do
            Assert.False(cleric.Invoke(preserve, new StandardResolver(new ScriptedRng(1))));

            cleric.Actor.Suffer(cleric.Actor.Health.Maximum - 1, DamageType.Slashing);
            Assert.True(cleric.Invoke(preserve, new StandardResolver(new ScriptedRng(1))));

            Assert.Equal(cleric.Actor.Health.Maximum / 2, cleric.Actor.Health.Current);
            Assert.Equal(1, cleric.UsesLeft(channel));
        }


        // --- Paladin (p.53-57) ----------------------------------------------------------------------------

        [Fact]
        public void APaladinCastsFromLevelOneAndSmitesFreeOnceADay()
        {
            Hero first = Made("paladin", 1, Ability.Strength);
            Assert.NotNull(first.Caster);
            Assert.Equal(2, ((SpellSlots)first.Caster.Resource).Maximum(1));

            Hero paladin = Made("paladin", 2, Ability.Strength);
            Assert.True(paladin.Caster.IsPrepared("divine_smite"));
            Assert.Equal(1, paladin.Caster.FreeLeft("divine_smite"));

            int slots = ((SpellSlots)paladin.Caster.Resource).Remaining(1);
            Assert.True(paladin.Caster.Pay(Srd.Spells.Find("divine_smite"), 1));
            Assert.Equal(slots, ((SpellSlots)paladin.Caster.Resource).Remaining(1));
            Assert.Equal(0, paladin.Caster.FreeLeft("divine_smite"));

            paladin.LongRest();
            Assert.Equal(1, paladin.Caster.FreeLeft("divine_smite"));
        }

        [Fact]
        public void TheAurasAreCharismaOnSavesAndNoFear()
        {
            Hero paladin = Made("paladin", 10, Ability.Strength);

            int with = paladin.Actor.SaveModifier(Ability.Wisdom);
            paladin.Actor.AuraAbility = null;
            int without = paladin.Actor.SaveModifier(Ability.Wisdom);
            paladin.Actor.AuraAbility = Ability.Charisma;

            Assert.Equal(System.Math.Max(1, paladin.Actor.AbilityModifier(Ability.Charisma)), with - without);

            Assert.False(paladin.Actor.Apply(Condition.Frightened));
        }

        [Fact]
        public void SacredWeaponSpendsTheOathsChannelDivinity()
        {
            Hero paladin = Made("paladin", 3, Ability.Strength);
            Feature weapon = Of(paladin, "sacred_weapon");
            Feature channel = Of(paladin, "paladin_channel_divinity");

            Assert.True(paladin.Invoke(weapon));
            Assert.Equal(1, paladin.UsesLeft(channel));
            Assert.Equal(System.Math.Max(1, paladin.Actor.AbilityModifier(Ability.Charisma)),
                         paladin.Actor.Boons.FlatOnAttacks);
        }


        // --- Druid (p.41-46) -------------------------------------------------------------------------------

        [Fact]
        public void TheDruidsLandAndLanguageSpellsAreAlwaysPrepared()
        {
            Hero druid = Made("druid", 3, Ability.Wisdom);

            Assert.True(druid.Caster.IsPrepared("speak_with_animals"));
            Assert.True(druid.Caster.IsPrepared("web"));
            Assert.DoesNotContain(druid.Features, f => f.Id == "lands_stride");
            Assert.DoesNotContain(druid.Class.ArmorTraining, w => w == ArmorWeight.Medium);
        }


        // --- creation's numbers (SRD class tables) -------------------------------------------------------

        [Fact]
        public void CreationUsesTheClassTables()
        {
            var making = new Creation.Creation(Srd, Srd.Backgrounds);

            making.Pick(Srd.Class("mage"));
            Assert.Equal(3, making.CantripPicks);
            Assert.Equal(4, making.SpellPicks);

            making.Pick(Srd.Class("paladin"));
            making.StartAt(5);
            Assert.Equal(2, making.HighestSpellLevel);
            Assert.Equal(6, making.SpellPicks);

            making.Pick(Srd.Class("barbarian"));
            making.StartAt(3);
            Assert.Equal(3, making.SkillPicks);
        }
    }
}
