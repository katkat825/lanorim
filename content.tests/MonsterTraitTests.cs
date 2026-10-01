using System.Collections.Generic;
using System.Linq;
using Content.Monsters;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // A STATBLOCK'S NAMED TRAITS (cc_task_open-questions-answers.md 2.3, Core.Characters.Knack): each at the moment
    // SRD 5.2.1 gives it, read from the statblock's "traits"
    public class MonsterTraitTests
    {
        static readonly Library Srd = Library.Srd();

        // what every d20 leaned on, by kind: the fight's own resolver, watched
        sealed class Watched : IResolver
        {
            readonly StandardResolver _inner;

            public Watched(IRng rng) => _inner = new StandardResolver(rng);

            public List<(RollKind Kind, Advantage Lean)> Leans { get; } = new();

            public Attempt Resolve(RollKind kind, int modifier, int against, Advantage advantage = Advantage.Flat)
            {
                Leans.Add((kind, advantage));
                return _inner.Resolve(kind, modifier, against, advantage);
            }

            public int Roll(DiceRoll dice) => _inner.Roll(dice);
        }

        static Encounter Hall(Watched dice)
        {
            Assert.True(MapReader.TryRead(Fights.Hall, out MapLayout map, out string problem), problem);
            return new Encounter(dice, new Battlefield(map));
        }

        static Actor Spawned(string monster) => Srd.Bestiary.Find(monster).Spawn();

        static Actor Hero()
        {
            var hero = new Actor("hero", 1, new AbilityScores(), Allegiance.Hero);
            hero.SetHealth(new Health(200));
            return hero;
        }

        static Advantage FirstAttackLean(Watched dice) => dice.Leans.First(l => l.Kind == RollKind.Attack).Lean;

        [Fact]
        public void TheStatblocksSayTheirTraits()
        {
            Assert.True(Spawned("wolf").Has(Knack.PackTactics));
            Assert.True(Spawned("giant_rat").Has(Knack.PackTactics));
            Assert.True(Spawned("kobold_warrior").Has(Knack.SunlightSensitivity));
            Assert.True(Spawned("zombie").Has(Knack.UndeadFortitude));
            Assert.True(Spawned("imp").Has(Knack.MagicResistance));
            Assert.True(Spawned("boar").Has(Knack.BloodiedFury));
            Assert.False(Spawned("goblin").Has(Knack.PackTactics));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PackTacticsWantsAnAllyBesideTheTarget(bool allyBeside)
        {
            var dice = new Watched(Script(20, 1));
            Encounter fight = Hall(dice);
            Actor hero = Hero();
            Actor rat = Spawned("giant_rat");
            Actor other = Spawned("giant_rat");

            fight.Enlist(hero, new Cell(5, 2));
            fight.Enlist(rat, new Cell(4, 2));
            fight.Enlist(other, allyBeside ? new Cell(6, 2) : new Cell(9, 4));

            fight.Swing(rat, hero, Srd.Bestiary.Find("giant_rat").Attacks.First());

            Assert.Equal(allyBeside ? Advantage.Advantage : Advantage.Flat, FirstAttackLean(dice));
        }

        [Fact]
        public void AnIncapacitatedAllyIsNoPack()
        {
            var dice = new Watched(Script(20, 1));
            Encounter fight = Hall(dice);
            Actor hero = Hero();
            Actor wolf = Spawned("wolf");
            Actor other = Spawned("wolf");

            fight.Enlist(hero, new Cell(5, 2));
            fight.Enlist(wolf, new Cell(4, 2));
            fight.Enlist(other, new Cell(6, 2));
            other.Apply(Condition.Incapacitated);

            fight.Swing(wolf, hero, Srd.Bestiary.Find("wolf").Attacks.First());

            Assert.Equal(Advantage.Flat, FirstAttackLean(dice));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BloodiedFuryIsAdvantageOnlyWhileBloodied(bool bloodied)
        {
            var dice = new Watched(Script(20, 1));
            Encounter fight = Hall(dice);
            Actor hero = Hero();
            Actor boar = Spawned("boar");

            fight.Enlist(hero, new Cell(5, 2));
            fight.Enlist(boar, new Cell(4, 2));
            if (bloodied) boar.Health.Take(boar.Health.Current - boar.Health.Maximum / 2);

            fight.Swing(boar, hero, Srd.Bestiary.Find("boar").Attacks.First());

            Assert.Equal(bloodied ? Advantage.Advantage : Advantage.Flat, FirstAttackLean(dice));
        }

        [Fact]
        public void SunlightSensitivityIsDisadvantageInAFightSetInSunlight()
        {
            var dice = new Watched(Script(20, 1));
            Encounter fight = Hall(dice);
            fight.Setting.Add(Encounter.Sunlight);
            Actor hero = Hero();
            Actor kobold = Spawned("kobold_warrior");

            fight.Enlist(hero, new Cell(5, 2));
            fight.Enlist(kobold, new Cell(4, 2));

            fight.Swing(kobold, hero, Srd.Bestiary.Find("kobold_warrior").Attacks.First(a => !a.IsRanged));

            Assert.Equal(Advantage.Disadvantage, FirstAttackLean(dice));
        }

        [Fact]
        public void MagicResistanceIsAdvantageOnASaveAgainstASpell()
        {
            var dice = new Watched(Script(20, 1, 5));
            Encounter fight = Hall(dice);
            Caster wizard = Wizard(out Actor me);
            Actor imp = Spawned("imp");

            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(imp, new Cell(3, 2));
            fight.Begin();

            new Incantation(fight.Resolver).Cast(wizard, Book.Find("hold_monster"), Aim.At(imp), fight: fight);

            Assert.Contains(dice.Leans, l => l.Kind == RollKind.Save && l.Lean == Advantage.Advantage);
        }

        [Theory]
        [InlineData(DamageType.Slashing, false, true)]
        [InlineData(DamageType.Radiant, false, false)]
        [InlineData(DamageType.Slashing, true, false)]
        public void UndeadFortitudeKeepsAZombieUpOnASave(DamageType type, bool critical, bool staysUp)
        {
            // the zombie's Constitution save: a 20 makes any DC
            var dice = new Watched(Script(20));
            Encounter fight = Hall(dice);
            var log = new List<Actor>();
            Actor hero = Hero();
            Actor zombie = Spawned("zombie");

            fight.Enlist(hero, new Cell(5, 2));
            fight.Enlist(zombie, new Cell(4, 2));

            int dealt = zombie.Health.Current;
            int suffered = zombie.Suffer(dealt, type);
            fight.Hurt(hero, zombie, suffered, type, critical, dealt);

            Assert.Equal(staysUp, !zombie.IsDown);
            if (staysUp)
            {
                Assert.Equal(1, zombie.Health.Current);
                Assert.False(zombie.Has(Condition.Prone));
                Assert.False(zombie.Has(Condition.Unconscious));
            }
        }

        // EVERY FALL IS TOLD (found 2026-10-03 on the way): a monster a spell killed was never said to go down, so
        // it didn't topple on the board or go down in the log. Hurt tells it, after whatever keeps a creature up
        [Fact]
        public void AMonsterASpellKillsIsToldDown()
        {
            var dice = new Watched(Script(20, 1));
            var log = new CombatLog();
            Assert.True(MapReader.TryRead(Fights.Hall, out MapLayout map, out _));
            var fight = new Encounter(dice, new Battlefield(map), log);
            Caster wizard = Wizard(out Actor me);
            Actor goblin = Goblin("goblin", hp: 1);

            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(goblin, new Cell(4, 2));
            fight.Begin();

            new Incantation(fight.Resolver).Cast(wizard, Book.Find("magic_missile"), Aim.At(goblin), fight: fight);

            Assert.True(goblin.IsDown);
            Assert.Contains("goblin goes down", log.ToString());
        }
    }
}
