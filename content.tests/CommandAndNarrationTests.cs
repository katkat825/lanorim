using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Magic;
using Core.Space;
using static Content.Tests.Fights;

namespace Content.Tests
{
    // COMMAND SAID AND SEEN, AND THE SPELLS THE NARRATOR TELLS (cc_task_m3 Part 2): "Druidcraft and Command don't seem to
    // do anything". Command's rules were right (FaithfulStrikeAndDirectTests); its word was never offered and two of its
    // turns passed in silence. A narrate spell did nothing anyone could see or read
    public class CommandAndNarrationTests
    {
        // the wizard at (2,2) going first, the goblin at (x,2) second, the fight's log listening. the goblin's save is the
        // third roll
        static Encounter Duel(int save, out FightLog log, out Caster wizard, out Actor me, out Actor goblin, int x = 3)
        {
            log = new FightLog();
            Encounter fight = Field(Script(20, 1, save), observer: log);
            wizard = Wizard(out me);
            goblin = Goblin();

            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(goblin, new Cell(x, 2));
            fight.Begin();

            return fight;
        }

        static string Obeys(string word) => KeyConventions.Key(KeyConventions.UiNs, "log", "obeys_" + word, "name");

        // cast the word on the goblin and play to the wizard's next turn
        static (Encounter Fight, FightLog Log, Actor Me, Actor Goblin, Turn Next) Commanded(string word, int save, int x = 3)
        {
            Encounter fight = Duel(save, out FightLog log, out Caster wizard, out Actor me, out Actor goblin, x);
            Turn mine = fight.Next();

            Casting cast = new Incantation(fight.Resolver).Cast(wizard, Book.Find("command"), Aim.At(goblin).Choosing(word),
                                                                 turn: mine, fight: fight);
            Assert.True(cast.Cast, cast.Refusal);

            fight.EndTurn();
            Turn next = fight.Next();

            return (fight, log, me, goblin, next);
        }

        static List<LogLine> Said(FightLog log, string key, Actor about) =>
            log.Lines.Where(l => l.Key == key && l.Args.Count > 0 && ReferenceEquals(l.Args[0], about)).ToList();

        // each word on a failed save: said once in the log, the turn spent as the word says, and the round back to the
        // wizard (the goblin starts 6 squares along the wizard's row)
        [Theory]
        [InlineData("approach")]
        [InlineData("drop")]
        [InlineData("flee")]
        [InlineData("grovel")]
        [InlineData("halt")]
        public void AFailedSaveAndTheTurnIsTheWordsAndSaidSo(string word)
        {
            (Encounter fight, FightLog log, Actor me, Actor goblin, Turn next) = Commanded(word, save: 1, x: 6);

            Assert.Single(Said(log, Obeys(word), goblin));
            Assert.Same(me, next.Actor);

            int now = fight.Field.Distance(me, goblin);

            switch (word)
            {
                case "approach": Assert.Equal(1, now); break;
                case "flee": Assert.True(now > 4, $"fled only to {now}"); break;
                default: Assert.Equal(4, now); break;
            }

            Assert.Equal(word == "grovel", goblin.Has(Condition.Prone));
            Assert.Equal(word == "drop", goblin.Disarmed);

            // the word is said before the turn it decides moves anything
            int obeyed = log.Lines.ToList().FindIndex(l => l.Key == Obeys(word));
            int moved = log.Lines.ToList().FindLastIndex(l => l.Key == KeyConventions.Key(KeyConventions.UiNs, "log", "moved", "name"));
            Assert.True(moved < 0 || moved > obeyed);
        }

        [Theory]
        [InlineData("grovel")]
        [InlineData("halt")]
        public void APassedSaveAndTheTurnIsItsOwnWithNothingSaid(string word)
        {
            Encounter fight = Duel(20, out FightLog log, out Caster wizard, out Actor me, out Actor goblin);
            Turn mine = fight.Next();

            new Incantation(fight.Resolver).Cast(wizard, Book.Find("command"), Aim.At(goblin).Choosing(word),
                                                 turn: mine, fight: fight);
            fight.EndTurn();

            // its own turn: nobody decided it, so it comes back to be played
            Turn next = fight.Next();

            Assert.Same(goblin, next.Actor);
            Assert.Empty(Said(log, Obeys(word), goblin));
            Assert.False(goblin.Has(Condition.Prone));
            Assert.Contains(log.Lines, l => l.Key == KeyConventions.Key(KeyConventions.UiNs, "log", "save_made", "name"));
        }

        // --- the narrator's spells ----------------------------------------------------------------------------------

        static readonly string[] Narrators =
        {
            "druidcraft", "mage_hand", "minor_illusion", "prestidigitation", "disguise_self", "wish", "thaumaturgy",
            "silent_image", "speak_with_animals", "darkvision", "major_image", "speak_with_dead",
        };

        [Fact]
        public void EveryNarratedSpellHasALineOfItsOwn()
        {
            List<Spell> narrated = Schema.Library.Srd().Spells.All.Where(s => s.Narrates).ToList();

            Assert.Equal(Narrators.OrderBy(s => s), narrated.Select(s => s.Id).OrderBy(s => s));

            foreach (Spell spell in narrated)
            {
                Assert.Equal($"spell.{spell.Id}.narration", spell.NarrationKey);
                Assert.Contains(spell.NarrationKey, spell.Keys());
            }

            // and a spell with something mechanical in it has none
            Assert.DoesNotContain(Book.Find("fire_bolt").Keys(), k => k.EndsWith(".narration"));
        }

        sealed class Heard : CombatObserver
        {
            public readonly List<(string Spell, Cell? At)> Told = new List<(string, Cell?)>();

            public override void Narrated(Actor caster, string spellId, Cell? at) => Told.Add((spellId, at));
        }

        [Fact]
        public void DruidcraftCastInAFightSaysItsLineAfterTheCastAtTheSquareItWasAimedAt()
        {
            var log = new FightLog();
            var heard = new Heard();
            Encounter fight = Field(Script(20, 1), observer: new Observers(log, heard));
            Caster wizard = Wizard(out Actor me);
            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(Goblin(), new Cell(8, 2));
            fight.Begin();

            Turn mine = fight.Next();
            Casting cast = new Incantation(fight.Resolver).Cast(wizard, Book.Find("druidcraft"), Aim.On(new Cell(3, 3)),
                                                                 turn: mine, fight: fight);
            Assert.True(cast.Cast, cast.Refusal);

            Assert.Equal(new[] { ("druidcraft", (Cell?)new Cell(3, 3)) }, heard.Told);

            int castLine = log.Lines.ToList().FindIndex(l => l.Key == KeyConventions.Key(KeyConventions.UiNs, "log", "cast", "name"));
            int told = log.Lines.ToList().FindIndex(l => l.Key == "spell.druidcraft.narration");

            Assert.True(castLine >= 0 && told > castLine, string.Join(" / ", log.Lines));
            Assert.Same(me, log.Lines[told].Args[0]);
        }

        [Fact]
        public void ASpellCastOnACreatureIsToldAtWhereItStands()
        {
            var heard = new Heard();
            Encounter fight = Field(Script(20, 1), observer: heard);
            Caster wizard = Wizard(out Actor me);
            fight.Enlist(me, new Cell(2, 2));
            fight.Enlist(Goblin(), new Cell(8, 2));
            fight.Begin();

            Turn mine = fight.Next();
            new Incantation(fight.Resolver).Cast(wizard, Book.Find("darkvision"), Aim.At(me), turn: mine, fight: fight);

            Assert.Equal(new[] { ("darkvision", (Cell?)new Cell(2, 2)) }, heard.Told);
        }
    }
}
