using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Resolution;
using Core.Space;

namespace Content.Tests
{
    // THE COMBAT COMMAND API (Tier 2.9 of the 2026-09-24 run): what the combat UX calls - the
    // action bar, movement, targeting with previews, confirm and cancel, reaction settings with an
    // "ask" that stops and waits, and the player's dice going to the tray
    public class CombatSessionTests
    {
        static readonly Library Srd = Library.Srd();

        // the hero at the '@' (0,0); an open gap on the east edge at (10,2)
        const string Hall = @"
+-+-+-+-+-+-+-+-+-+-+-+
|@ . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+ + + + + + + + + + + +
|. . . . . . . . . . .
+ + + + + + + + + + + +
|. . . . . . . . . . .|
+-+-+-+-+-+-+-+-+-+-+-+";

        static MapLayout Map()
        {
            Assert.True(MapReader.TryRead(Hall, out MapLayout map, out string problem), problem);
            return map;
        }

        static Hero Made(string cls, int level = 3, params string[] spells)
        {
            Content.Classes.CharacterClass made = Srd.Class(cls);

            var hero = new Hero("Tess", made, Srd.Kind("human"), Srd.Background("soldier"),
                                Creation.Creation.Standard(made), level);

            hero.Build(null, made.SkillChoices.Take(made.SkillPicks).ToList(), null, Srd.Items,
                       spells.Select(s => Srd.Spells.Find(s)).ToList());

            return hero;
        }

        // the hero always first: the dice source throws 20s for the hero, the GM rolls 1s
        static CombatSession Session(Hero hero, IEnumerable<Battle.Foe> foes, IDiceSource dice = null,
                                     IReactionAsker asker = null, ReactionSettings settings = null,
                                     IRng gm = null)
        {
            var resolver = new TableResolver(gm ?? new ScriptedRng(1), dice ?? new Loaded(20),
                                             a => ReferenceEquals(a, hero.Actor));

            Battle battle = Battle.Set(Srd, Map(), hero, foes, resolver,
                                       new PolicyChooser(settings ?? new ReactionSettings(),
                                                         asker ?? new AnswerAlways(false)));

            var session = new CombatSession(battle, Srd.Items, Srd.Forms);
            session.Start();
            return session;
        }

        static Battle.Foe Goblin(int x, int y) => new Battle.Foe(Srd.Bestiary.Find("goblin"), new Cell(x, y));

        // a dice source that always throws the same face, and counts what it was asked for
        sealed class Loaded : IDiceSource
        {
            readonly int _face;

            public Loaded(int face) => _face = face;

            public List<Die> Asked { get; } = new List<Die>();

            public IReadOnlyList<int> Throw(IReadOnlyList<Die> dice)
            {
                Asked.AddRange(dice);
                return dice.Select(d => System.Math.Min(_face, d.Sides())).ToList();
            }
        }

        [Fact]
        public void TheSessionStopsOnTheHerosTurnWithAnActionBar()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(8, 1) });

            Assert.Equal(SessionPhase.Choosing, session.Phase);
            Assert.Same(session.Hero.Actor, session.Turn.Actor);

            IReadOnlyList<ActionOption> bar = session.Options();

            Assert.Contains(bar, o => o.Kind == OptionKind.Attack);
            Assert.Contains(bar, o => o.Id == "dash" && o.Enabled);
            Assert.Contains(bar, o => o.Kind == OptionKind.EndTurn);

            // the goblin is far away: the sword says why it is greyed
            ActionOption sword = bar.First(o => o.Kind == OptionKind.Attack && !o.Attack.IsRanged);
            Assert.False(sword.Enabled);
            Assert.Equal(CombatSession.Why("out_of_range"), sword.WhyNotKey);

            // one to nine, in order, end turn left for the button and Space
            Assert.Equal(Enumerable.Range(1, bar.Count(o => o.Hotkey > 0)),
                         bar.Where(o => o.Hotkey > 0).Select(o => o.Hotkey));
        }

        [Fact]
        public void WalkingUpSpendsMovementAndThenTheAttackPreviewsItsChance()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(4, 1) });
            Actor goblin = session.Battle.Monsters.Single();

            Assert.True(session.Reachable().ContainsKey(new Cell(3, 1)));
            Assert.True(session.PathTo(new Cell(3, 1)).Count > 1);

            ActionResult walk = session.Move(new Cell(3, 1));
            Assert.True(walk.Done);
            Assert.True(session.Turn.SquaresLeft < 6);

            ActionOption sword = session.Options().First(o => o.Kind == OptionKind.Attack && o.Enabled);
            Assert.True(session.Select(sword));
            Assert.Equal(SessionPhase.Targeting, session.Phase);
            Assert.Contains(goblin, session.LegalTargets());

            Preview preview = session.Preview(goblin);
            Assert.True(preview.Legal);

            double expected = CombatSession.AttackChance(sword.Attack.Modifier(session.Hero.Actor),
                                                          goblin.ArmorClass, Advantage.Flat);
            Assert.Equal(expected, preview.HitChance, 3);
            Assert.True(preview.ExpectedDamage > 0);

            // the dice source throws a 20: a critical, and the goblin's 7 hit points are gone
            ActionResult swing = session.Confirm(goblin);
            Assert.True(swing.Done);
            Assert.True(swing.Blow.Critical);
            Assert.Equal(SessionPhase.Over, session.Phase);
            Assert.Equal(Outcome.HeroesWon, session.Outcome);
        }

        [Fact]
        public void CancelPutsTheBarBackAndSpendsNothing()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(1, 1) });

            ActionOption sword = session.Options().First(o => o.Kind == OptionKind.Attack && o.Enabled);
            session.Select(sword);
            session.Cancel();

            Assert.Equal(SessionPhase.Choosing, session.Phase);
            Assert.Null(session.Selected);
            Assert.Equal(2, session.Turn.Actions);
        }

        [Fact]
        public void AnAreaSpellPreviewsItsTemplateAndTurnsWithQAndE()
        {
            CombatSession session = Session(Made("mage", 3, "burning_hands", "fire_bolt"),
                                            new[] { Goblin(2, 0) });

            ActionOption hands = session.Options().First(o => o.Id == "spell:burning_hands");
            Assert.Equal(Targeting.Direction, hands.Targeting);

            session.Select(hands);
            session.Point(Facing.South);
            Preview south = session.Preview(null, null);
            Assert.DoesNotContain(new Cell(2, 0), south.Template);

            // a cone into empty air promises nothing (it used to promise the full average)
            Assert.Empty(south.Targets);
            Assert.Equal(0, south.ExpectedDamage);

            Assert.Equal(Facing.East, session.Rotate(-1));
            Preview east = session.Preview(null, null);
            Assert.Contains(new Cell(2, 0), east.Template);
            Assert.NotNull(east.SaveDc);
            Assert.True(east.HalfOnSave);
            Assert.Single(east.Targets);

            ActionResult burn = session.Confirm();
            Assert.True(burn.Done);
            Assert.NotNull(burn.Casting);
        }

        [Fact]
        public void EndingTheTurnPlaysTheGoblinAndComesBack()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(8, 3) });
            Turn first = session.Turn;
            var log = new FightLog();
            ((Observers)session.Fight.Observer).Add(log);

            session.EndTurn();

            Assert.Equal(SessionPhase.Choosing, session.Phase);
            Assert.Same(session.Hero.Actor, session.Turn.Actor);
            Assert.NotSame(first, session.Turn);
            Assert.Equal(2, session.Fight.Round);

            // the goblin had its turn in between (it shot, being a goblin with a bow)
            Assert.Contains(log.Lines, l => l.Key.Contains("turn") &&
                                            l.Args.FirstOrDefault() is Actor a && a.Id.StartsWith("goblin"));
        }

        [Fact]
        public void TheHerosDiceComeFromTheTrayAndTheGoblinsDoNot()
        {
            var dice = new Loaded(20);
            CombatSession session = Session(Made("fighter"), new[] { Goblin(1, 1) }, dice);

            // initiative was the hero's first throw
            Assert.Contains(Die.D20, dice.Asked);
            int before = dice.Asked.Count;

            // the goblin swings on its turn - behind the screen, not on the felt
            session.EndTurn();
            Assert.Equal(before, dice.Asked.Count);

            ActionOption sword = session.Options().First(o => o.Kind == OptionKind.Attack && o.Enabled);
            session.Select(sword);
            session.Confirm(session.LegalTargets().First());

            Assert.True(dice.Asked.Count > before);
        }

        [Fact]
        public void AnAskReactionStopsAndTheAnswerDecides()
        {
            // a mage with Shield, set to "ask"; the goblin's scimitar hits on the GM's 15
            var settings = new ReactionSettings();
            settings.Set("shield", ReactionPolicy.Ask);

            var yes = new Counting(true);
            Hero mage = Made("mage", 3, "shield");
            CombatSession session = Session(mage, new[] { Goblin(1, 1) }, asker: yes,
                                            settings: settings, gm: new ScriptedRng(15));

            var slots = (SpellSlots)mage.Caster.Resource;
            int first = slots.Remaining(1);

            session.EndTurn();

            // asked, said yes, and a first-level slot went on the Shield
            Assert.True(yes.Asked > 0);
            Assert.Equal(first - 1, slots.Remaining(1));
        }

        [Fact]
        public void NeverMeansNoOpportunityAttackAndAutoMeansOne()
        {
            Hero fighter = Made("fighter");
            CombatSession session = Session(fighter, new[] { Goblin(1, 0) });
            Actor goblin = session.Battle.Monsters.Single();

            var swing = new OpportunityAttack(fighter.Attacks.First(a => !a.IsRanged));
            Moment leaving = Moment.Leaving(goblin, fighter.Actor, new Cell(1, 0), new Cell(2, 0));

            var never = new ReactionSettings();
            never.Set("opportunity_attack", ReactionPolicy.Never);

            Assert.Null(new PolicyChooser(never, new AnswerAlways(true))
                            .Choose(session.Fight, fighter.Actor, leaving, new[] { swing }));

            Assert.Same(swing, new PolicyChooser(new ReactionSettings(), new AnswerAlways(false))
                                   .Choose(session.Fight, fighter.Actor, leaving, new[] { swing }));
        }

        [Fact]
        public void AnOpenEdgeOffersTheWayOut()
        {
            CombatSession session = Session(Made("fighter"), new[] { Goblin(1, 3) });

            Assert.DoesNotContain(session.Options(), o => o.Kind == OptionKind.Flee);

            session.Fight.Field.Remove(session.Hero.Actor);
            session.Fight.Field.Place(session.Hero.Actor, new Cell(10, 2));

            ActionOption leave = session.Options().Single(o => o.Kind == OptionKind.Flee);
            Assert.True(session.Take(leave).Done);
            Assert.Equal(Outcome.Fled, session.Outcome);
        }

        sealed class Counting : IReactionAsker
        {
            readonly bool _answer;

            public Counting(bool answer) => _answer = answer;

            public int Asked { get; private set; }

            public bool Ask(ReactionQuestion question)
            {
                Asked++;
                return _answer;
            }
        }
    }
}
