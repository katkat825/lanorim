using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Combat;
using Core.Dice;

namespace Content.Tests
{
    // WHAT THE LOG SAYS ABOUT AN ATTACK, AND WHEN (cc_task_table-ui-minis-zoom-damage.md): hit, miss or
    // critical with the armor class the moment the roll is settled, then the damage throw, then the
    // damage line - for a weapon and a spell alike - and a fight's log that stops hearing the
    // campaign's rolls when its fight is over.
    public partial class CombatSessionTests
    {
        static FightLog Heard(CombatSession session)
        {
            var log = new FightLog();
            log.Listen((TableResolver)session.Fight.Resolver);
            ((Observers)session.Fight.Observer).Add(log);
            return log;
        }

        // the line's name; "damage_lessened" (the goblin had fewer hit points than the roll) is a damage line
        static string Say(LogLine line) => line.Key.Split('.')[2].Replace("damage_lessened", "damage");

        static List<string> Said(FightLog log, int from) => log.Lines.Skip(from).Select(Say).ToList();

        [Fact]
        public void AHitIsSaidBeforeTheDamageIsThrownAndTheDamageNamesTheWeapon()
        {
            // a 15 on the d20, the highest face on anything else: +5 against the goblin's 15 hits
            var dice = new Loaded(15);
            CombatSession session = Session(Made("fighter"), new[] { Goblin(1, 1) }, dice);
            FightLog log = Heard(session);
            int lines = log.Lines.Count;

            ActionOption weapon = session.Options().First(o => o.Kind == OptionKind.Attack && o.Enabled);
            session.Select(weapon);
            int asked = dice.Asked.Count;
            session.Confirm(session.LegalTargets().First());

            Assert.Equal(new[] { "roll_attack", "hit", "roll_table", "damage" }, Said(log, lines).Take(4));

            // the tray was asked for the d20 and then for the weapon's own damage dice
            DiceRoll damage = weapon.Attack.DamageFor(session.Hero.Actor);
            Assert.Equal(new[] { Die.D20 }.Concat(Enumerable.Repeat(damage.Die, damage.Count)),
                         dice.Asked.Skip(asked));

            LogLine dealt = log.Lines.Skip(lines).First(l => Say(l) == "damage");
            Assert.Equal(weapon.Attack.NameKey, Assert.IsType<Named>(dealt.Args[0]).Key);
            Assert.Equal(damage.ToString(), dealt.Args[2]);
        }

        [Fact]
        public void AMissSaysMissAndThrowsNoDamage()
        {
            var dice = new Loaded(2);
            CombatSession session = Session(Made("fighter"), new[] { Goblin(1, 1) }, dice);
            FightLog log = Heard(session);
            int lines = log.Lines.Count;

            session.Select(session.Options().First(o => o.Kind == OptionKind.Attack && o.Enabled));
            int asked = dice.Asked.Count;
            session.Confirm(session.LegalTargets().First());

            Assert.Equal(new[] { "roll_attack", "miss" }, Said(log, lines));
            Assert.Equal(new[] { Die.D20 }, dice.Asked.Skip(asked));

            // the miss says the number it had to beat
            LogLine miss = log.Lines.Last();
            Assert.Equal(session.Battle.Monsters.Single().ArmorClass, miss.Args[3]);
        }

        [Fact]
        public void ACriticalSaysSoAndThrowsTheDamageDiceTwice()
        {
            var dice = new Loaded(20);
            CombatSession session = Session(Made("fighter"), new[] { Goblin(1, 1) }, dice);
            FightLog log = Heard(session);
            int lines = log.Lines.Count;

            ActionOption weapon = session.Options().First(o => o.Kind == OptionKind.Attack && o.Enabled);
            session.Select(weapon);
            int asked = dice.Asked.Count;
            session.Confirm(session.LegalTargets().First());

            Assert.Equal(new[] { "roll_attack", "critical", "roll_table", "damage" }, Said(log, lines).Take(4));

            DiceRoll damage = weapon.Attack.DamageFor(session.Hero.Actor);
            Assert.Equal(1 + 2 * damage.Count, dice.Asked.Count - asked);
        }

        [Fact]
        public void AFireBoltSaysHitAndItsDamageIsThrownOnTheTray()
        {
            var dice = new Loaded(15);
            CombatSession session = Session(Made("mage", 3, "fire_bolt"), new[] { Goblin(4, 0) }, dice);
            FightLog log = Heard(session);
            int lines = log.Lines.Count;

            session.Select(session.Options().First(o => o.Id == "spell:fire_bolt"));
            int asked = dice.Asked.Count;
            session.Confirm(session.LegalTargets().First());

            List<string> said = Said(log, lines);
            Assert.Equal(new[] { "cast_on", "roll_attack", "hit", "roll_table", "damage" }, said.Take(5));
            Assert.Equal(new[] { Die.D20, Die.D10 }, dice.Asked.Skip(asked));

            LogLine dealt = log.Lines.Skip(lines).First(l => Say(l) == "damage");
            Assert.Equal("spell.fire_bolt.name", ((Named)dealt.Args[0]).Key);
        }

        [Fact]
        public void ASaveSpellSaysTheSaveAndTheCasterThrowsTheDamage()
        {
            // the goblin rolls its own save behind the screen (the GM's 1s fail it); the damage is
            // the caster's, so it is thrown on the tray (combat_ux.md: "attacks, damage, saves...")
            var dice = new Loaded(15);
            CombatSession session = Session(Made("mage", 3, "vicious_mockery"), new[] { Goblin(4, 0) }, dice);
            FightLog log = Heard(session);
            int lines = log.Lines.Count;

            session.Select(session.Options().First(o => o.Id == "spell:vicious_mockery"));
            int asked = dice.Asked.Count;
            session.Confirm(session.LegalTargets().First());

            List<string> said = Said(log, lines);
            Assert.Equal(new[] { "cast_on", "save_failed" }, said.Take(2));
            Assert.Contains("damage", said);
            Assert.True(said.IndexOf("save_failed") < said.IndexOf("damage"));
            Assert.DoesNotContain(Die.D20, dice.Asked.Skip(asked));
            Assert.NotEmpty(dice.Asked.Skip(asked));
        }

        [Fact]
        public void AFightsLogStopsHearingRollsWhenTheFightEnds()
        {
            var dice = new Loaded(15);
            CombatSession session = Session(Made("fighter"), new[] { Goblin(1, 1) }, dice);
            var resolver = (TableResolver)session.Fight.Resolver;

            var first = new FightLog();
            first.Listen(resolver);
            first.Ended(Outcome.HeroesWon);
            int heard = first.Lines.Count;

            var second = new FightLog();
            second.Listen(resolver);

            resolver.Roll(new DiceRoll(1, Die.D20, 2), session.Hero.Actor);

            // the next fight's roll is in the next fight's log, once, and not in the last one's
            Assert.Equal(heard, first.Lines.Count);
            Assert.Single(second.Lines);
        }
    }
}
