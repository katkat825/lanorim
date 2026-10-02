using System.Linq;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Space;
using Core.Dice;
using Core.Resolution;
using Godot;

namespace Game.Table
{
    public partial class Table
    {
        void Demonstrate()
        {
            Library srd = Library.Srd();

            var making = new Content.Creation.Creation(srd, srd.Backgrounds);

            making.Pick(srd.Class("rogue"));
            making.Pick(srd.Kind("halfling"));
            making.Pick(srd.Background("criminal"));
            making.SuggestTraits();

            foreach (Skill pick in making.SkillChoices.Take(making.SkillPicksLeft))
                making.Train(pick);

            foreach (Skill pick in making.Skills.Take(making.ExpertisePicksLeft).ToList())
                making.Master(pick);

            making.Call("Pell");

            Hero hero = making.Finish();

            if (hero == null)
            {
                GD.PushError("table: could not build the demonstration hero - " +
                             string.Join("; ", making.Problems));
                return;
            }

            _someone = hero.Actor;

            GD.Print($"table   {hero}");

            LayAMap(hero);

            Throw();
        }

        // A MAP THE MAP BUILDER COULD HAVE MADE, laid on the board with the hero standing on it
        // and a goblin across the room. Built in code here only because there is no campaign to
        // load one from yet - content/Maps/MapDraft.cs is the thing that makes them, and this is
        // the same MapLayout it produces.
        // `--corners`: a cross of walls as well, so every kind of post is on the table at once - an L at the room's
        // corners, a T where the wall meets the outer one, an end, a wall meeting a door, and this (cc_task_f 1.7)
        static void Cross(Content.Maps.MapDraft draft)
        {
            draft.Wall(new Border(new Cell(3, 2), true), Edge.Wall);
            draft.Wall(new Border(new Cell(3, 3), true), Edge.Wall);
            draft.Wall(new Border(new Cell(2, 3), false), Edge.Wall);
            draft.Wall(new Border(new Cell(3, 3), false), Edge.Wall);
        }

        void LayAMap(Hero hero)
        {
            if (Board == null)
            {
                GD.Print("table   no board on the table yet - the dice work without one");
                return;
            }

            var draft = new Content.Maps.MapDraft(12, 10);

            draft.Paint(new Cell(0, 0), new Cell(11, 9), Tile.Floor);
            draft.Enclose();

            // a little difficult ground and a wall to walk round, so the tiles have something to
            // draw other than floor
            draft.Paint(new Cell(5, 3), new Cell(6, 4), Tile.Rough);

            for (int y = 0; y < 6; y++) draft.Wall(new Border(new Cell(7, y), true), Edge.Wall);

            draft.Wall(new Border(new Cell(7, 4), true), Edge.Door);

            if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--corners") >= 0) Cross(draft);

            draft.PlaceStart(new Cell(1, 5));
            draft.PlaceSpawn(1, new Cell(10, 1));

            if (!draft.Sound)
            {
                GD.PushError("table: the demonstration map does not hold up - " +
                             string.Join("; ", draft.Problems()));
                return;
            }

            MapLayout map = draft.Layout();

            Board.Lay(map);
            GmScreen?.StandBehind(Board);

            _field = new Battlefield(map);

            _field.Place(hero.Actor, map.Start);
            Board.Place(hero.Actor, map.Start);

            Actor goblin = Library.Srd().Bestiary.Find("goblin").Spawn();
            Cell there = map.SpawnAt(1) ?? new Cell(8, 1);

            _field.Place(goblin, there);
            Board.Place(goblin, there);

            GD.Print($"table   {Board}, {map}");

            // `--open-door`: the demonstration's door opened, for a picture of the door model swung on its hinge
            // (cc_task_open-questions-answers.md 2.1)
            if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--open-door") >= 0)
            {
                var door = new Border(new Cell(7, 4), true);
                _field.OpenDoor(door);
                Board.Reopen(_field.Map, door);
            }

            // THE REACH IS NOT LIT ON BOOT ANY MORE, and that is the whole of the "washed out
            // squares" report. Board.ShowReach lights every square the hero could walk to, which on
            // this map is about a third of it - so opening the scene painted a third of the
            // parchment blue and it read as the map being two different colours, or as the lighting
            // being wrong. It was neither: it was a gameplay cue nobody had asked for yet, held up
            // permanently because Demonstrate() called it once and nothing ever took it down.
            //
            // A reach is something you hold up WHILE A PLAYER IS DECIDING and drop when they have.
            // There is no player and no decision here, so there is nothing to hold up. ShowReach
            // and CellLights are untouched and tested; the real turn UI is what will call them.
            _ = hero;
        }

        Battlefield _field;

        // the demonstration throw, and what space does
        void Throw()
        {
            if (_someone == null || Busy) return;

            const int dc = 15;

            GD.Print("");
            GD.Print($"table   {_text.Get(Skills.NameKey(Skill.Stealth))} against DC {dc}, " +
                     $"modifier {_someone.CheckModifier(Skill.Stealth):+0;-0}");

            Check(_someone, Skill.Stealth, dc, Advantage.Flat, attempt =>
            {
                GD.Print($"table   {attempt}");

                GD.Print(attempt.Succeeded ? "table   unseen." : "table   somebody looks up.");

                // the keeper delta: a natural 1 or 20 changes something whatever the total said
                Consequence drawn = Library.Srd().Consequences
                                           .DrawFor(new SeededRng((int)Time.GetTicksMsec()), attempt);

                if (drawn == null) return;

                GD.Print($"table   and {_text.Get(drawn.LineKey)}");
            });
        }
    }
}
