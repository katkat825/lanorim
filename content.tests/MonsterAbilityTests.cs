using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Monsters;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Tests
{
    // decisions_checklist.md section 6's four monster primitives - multiattack, save-or-condition,
    // recharge, resistance - plus spellcasting through the ordinary spell engine, and the tactics
    // that pick between a swing and a spell
    public class MonsterAbilityTests
    {
        static readonly Library Srd = Library.Srd();

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
            new ScriptedRng(rolls.Concat(Enumerable.Repeat(10, 200)).ToArray());

        static Encounter Field(IRng rng)
        {
            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);

            return new Encounter(new StandardResolver(rng), new Battlefield(map), new CombatLog());
        }

        static Actor Hero(int hp = 60)
        {
            var hero = new Actor("hero", 5, new AbilityScores(), Allegiance.Hero);
            hero.SetHealth(new Health(hp));
            hero.Armor = new ArmorProfile(ArmorWeight.Heavy, 10);
            hero.Tag("humanoid");
            return hero;
        }

        [Fact]
        public void TheNewStatblocksLoadWithTheirSpecials()
        {
            Assert.True(Srd.Bestiary.Sound, string.Join("\n", Srd.Bestiary.Problems));

            foreach (string id in new[] { "giant_rat", "kobold_warrior", "guard", "ghoul", "boar",
                                          "brown_bear", "owlbear", "priest", "mage" })
                Assert.True(Srd.Bestiary.Has(id), id);

            Monster spider = Srd.Bestiary.Find("giant_spider");
            Assert.Single(spider.Actions);
            Assert.Equal(5, spider.Actions[0].Recharge);

            Assert.Contains("fireball", Srd.Bestiary.Find("mage").Spellcasting.SpellIds);
        }

        [Fact]
        public void AWolfsBiteKnocksAMediumCreatureProneAndNotAHugeOne()
        {
            Monster wolf = Srd.Bestiary.Find("wolf");
            Actor biter = wolf.Spawn();
            Attack bite = wolf.Attacks[0];

            // a hit (15) and its damage
            var resolver = new StandardResolver(new ScriptedRng(15, 3, 3));

            Actor medium = Hero();
            Strike.Make(resolver, biter, medium, bite, riders: bite.OnHit);
            Assert.True(medium.Has(Condition.Prone));

            Actor huge = Hero();
            huge.Size = Size.Huge;
            Strike.Make(resolver, biter, huge, bite, riders: bite.OnHit);
            Assert.False(huge.Has(Condition.Prone));
        }

        [Fact]
        public void AGhoulsClawParalyzesOnAFailedSave()
        {
            Monster ghoul = Srd.Bestiary.Find("ghoul");
            Actor claws = ghoul.Spawn();
            Attack claw = ghoul.Attacks.Single(a => a.Id == "ghoul_claw");

            // hit 15, damage 2, the hero's Con save 1 against DC 10
            Encounter fight = Field(Script(20, 1, 15, 2, 1));
            Actor hero = Hero();
            fight.Enlist(claws, new Cell(2, 2));
            fight.Enlist(hero, new Cell(3, 2));
            fight.Begin();

            Turn turn = fight.Next();
            Assert.Same(claws, turn.Actor);

            fight.Hit(turn, hero, claw);

            Assert.True(hero.Has(Condition.Paralyzed));
        }

        [Fact]
        public void AGiantSpiderWebsOnceAndMustRechargeBeforeItWebsAgain()
        {
            Monster spider = Srd.Bestiary.Find("giant_spider");
            Actor me = spider.Spawn();
            Caster webs = spider.CasterFor(me, Srd.Spells);
            Spell web = webs.Known.Single();

            var cast = new Incantation(new StandardResolver(new ScriptedRng(1)));
            Actor hero = Hero();

            Assert.True(webs.CanCast(web, 0));
            Assert.True(cast.Cast(webs, web, Aim.At(hero)).Cast);
            Assert.True(hero.Has(Condition.Restrained));
            Assert.Equal(13, webs.SaveDc);

            Assert.False(webs.CanCast(web, 0));

            // a 4 on the recharge d6 is not enough; a 5 is
            webs.Recharge(new StandardResolver(new ScriptedRng(4)));
            Assert.False(webs.CanCast(web, 0));

            webs.Recharge(new StandardResolver(new ScriptedRng(5)));
            Assert.True(webs.CanCast(web, 0));
        }

        [Fact]
        public void AMageCastsItsDailySpellsAndRunsOutOfThem()
        {
            Monster mage = Srd.Bestiary.Find("mage");
            Actor me = mage.Spawn();
            Caster caster = mage.CasterFor(me, Srd.Spells);

            Spell fireball = caster.Find("fireball");
            Assert.NotNull(fireball);
            Assert.Equal(14, caster.SaveDc);

            Assert.True(caster.Pay(fireball, 3));
            Assert.True(caster.Pay(fireball, 3));
            Assert.False(caster.CanCast(fireball, 3));

            // fire bolt is at will
            Spell bolt = caster.Find("fire_bolt");
            for (int i = 0; i < 5; i++) Assert.True(caster.Pay(bolt, 0));

            caster.Rested(Rest.Long);
            Assert.True(caster.CanCast(fireball, 3));
        }

        [Fact]
        public void TheMageThrowsAnAreaSpellIntoAClusterRatherThanStab()
        {
            Monster mage = Srd.Bestiary.Find("mage");

            Encounter fight = Field(Script(20, 1, 1, 1));
            Actor me = mage.Spawn();
            Actor a = Hero();
            Actor b = new Actor("friend", 3, new AbilityScores(), Allegiance.Hero);
            b.SetHealth(new Health(60));

            fight.Enlist(me, new Cell(1, 2));
            fight.Enlist(a, new Cell(8, 2));
            fight.Enlist(b, new Cell(8, 3));
            fight.Begin();

            var cast = new Incantation(fight.Resolver);
            var brain = (MonsterTactics)mage.Brain(mage.CasterFor(me, Srd.Spells), cast);

            Turn turn = fight.Next();
            Assert.Same(me, turn.Actor);

            brain.Take(fight, turn);

            // the best of its areas for two heroes side by side (Cone of Cold, as it happens)
            Assert.Contains(brain.Cast, c => c.Touched.Contains(a) && c.Touched.Contains(b));
            Assert.True(a.Health.Current < 60 && b.Health.Current < 60);
        }

        [Fact]
        public void ACravenKoboldRunsWhenItIsBloodied()
        {
            Monster kobold = Srd.Bestiary.Find("kobold_warrior");

            Encounter fight = Field(Script(20, 1));
            Actor me = kobold.Spawn();
            Actor hero = Hero();

            fight.Enlist(me, new Cell(3, 2));
            fight.Enlist(hero, new Cell(4, 2));
            fight.Begin();

            me.Suffer(4, DamageType.Slashing);
            Assert.True(me.Health.IsBloodied);

            Turn turn = fight.Next();
            kobold.Brain(null, new Incantation(fight.Resolver)).Take(fight, turn);

            Assert.True(fight.Field.Distance(me, hero) > 3);
            Assert.Equal(60, hero.Health.Current);
        }

        [Fact]
        public void TheSameSeedPlaysTheSameMonsterTurn()
        {
            int Play(int seed)
            {
                Monster mage = Srd.Bestiary.Find("mage");
                Encounter fight = Field(new SeededRng(seed));
                Actor me = mage.Spawn();
                Actor hero = Hero(100);

                fight.Enlist(me, new Cell(1, 1));
                fight.Enlist(hero, new Cell(7, 3));
                fight.Begin();

                var cast = new Incantation(fight.Resolver);
                ITactics brain = mage.Brain(mage.CasterFor(me, Srd.Spells), cast);

                for (int i = 0; i < 6 && !fight.Over; i++)
                {
                    Turn turn = fight.Next();
                    if (turn == null) break;

                    if (turn.Actor == me) brain.Take(fight, turn);
                    else fight.EndTurn();
                }

                return hero.Health.Current;
            }

            Assert.Equal(Play(7), Play(7));
        }
    }
}
