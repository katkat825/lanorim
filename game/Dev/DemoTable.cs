using System.Linq;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Core.Space;
using Game.Localization;
using Godot;

namespace Game.Dev
{
    // THE DEMONSTRATION TABLE: table.tscn opened on its own (the editor, check-table) with no campaign to play and no
    // map to build. ONE REAL CHECK, THROWN ON THE REAL TRAY, so opening the scene shows the whole stack working: a hero
    // built from the SRD data, a d20 that tumbles in a wooden box, and core reading the face off the felt; Space throws
    // it again. And for the look tests, the line-up (DemoLineup).
    //
    // Was Table.Demo.cs, a partial of the table; a dev node that acts on the table since cc_task_j Part 2, added by the
    // dev registry, so a release build has none of it
    public partial class DemoTable : Node
    {
        public Game.Table.Table Table { get; set; }

        readonly ILocalizer _text = new GodotLocalizer();

        Actor _someone;
        Battlefield _field;

        public override void _Ready()
        {
            Table.ThrowPressed = Throw;
            Demonstrate();
        }

        public override void _ExitTree()
        {
            if (Table != null && Table.ThrowPressed == Throw) Table.ThrowPressed = null;
        }

        void Demonstrate()
        {
            Library srd = Library.Srd();

            var making = new Content.Creation.Creation(srd, srd.Backgrounds);

            making.Pick(srd.Class(DevArgs.Class ?? "rogue"));
            // `--species fairy` or a lineage, `--species beastfolk_vulpine`: its species, then it
            Content.Species.Kind kind = srd.Kind(DevArgs.Species ?? "halfling") ?? srd.Kind("halfling");
            making.Pick(kind.IsLineage ? srd.Kind(kind.LineageOf) : kind);
            if (kind.IsLineage) making.PickLineage(kind);
            making.Pick(srd.Background("criminal"));
            making.SuggestTraits();

            foreach (Skill pick in making.SkillChoices.Take(making.SkillPicksLeft))
                making.Train(pick);

            foreach (Skill pick in making.Skills.Take(making.ExpertisePicksLeft).ToList())
                making.Master(pick);

            // a caster (`--class mage`) learns the first spells it is offered
            foreach (var spell in making.SpellChoices.ToList())
                if (making.CantripPicksLeft > 0 || making.SpellPicksLeft > 0) making.Learn(spell);

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

        // the demonstration room: floor, walls round, a little difficult ground and a wall to walk round, so the tiles
        // have something to draw other than floor
        static void Room(Content.Maps.MapDraft draft)
        {
            draft.Paint(new Cell(0, 0), new Cell(11, 9), Tile.Floor);
            draft.Enclose();
            draft.Paint(new Cell(5, 3), new Cell(6, 4), Tile.Rough);

            for (int y = 0; y < 6; y++) draft.Wall(new Border(new Cell(7, y), true), Edge.Wall);

            draft.Wall(new Border(new Cell(7, 4), true), Edge.Door);

            if (DevArgs.Corners) Cross(draft);

            draft.PlaceStart(new Cell(1, 5));
        }

        void LayAMap(Hero hero)
        {
            Game.Board.Board board = Table.Board;

            if (board == null)
            {
                GD.Print("table   no board on the table yet - the dice work without one");
                return;
            }

            // `--wall-styles`: every wall style and its door, in place of the demonstration room (DemoTable.Styles.cs)
            var draft = DevArgs.WallStyles ? StyleSampler() : new Content.Maps.MapDraft(12, 10);

            if (!DevArgs.WallStyles) Room(draft);

            draft.PlaceSpawn(1, new Cell(draft.Columns - 2, 1));

            if (!draft.Sound)
            {
                GD.PushError("table: the demonstration map does not hold up - " +
                             string.Join("; ", draft.Problems()));
                return;
            }

            MapLayout map = draft.Layout();

            board.Lay(map, draft.Look());
            Table.GmScreen?.StandBehind(board);

            _field = new Battlefield(map);

            var lineup = new DemoLineup(board, _field);
            lineup.Figure(hero);

            _field.Place(hero.Actor, map.Start);
            board.Place(hero.Actor, map.Start);

            if (!lineup.Stand(map.Start))
            {
                Actor goblin = Library.Srd().Bestiary.Find("goblin").Spawn();
                Cell there = map.SpawnAt(1) ?? new Cell(8, 1);

                _field.Place(goblin, there);
                board.Place(goblin, there);
            }

            lineup.ShowTheCompanion(Table, hero);

            GD.Print($"table   {board}, {map}");

            // `--open-door`: the demonstration's door opened, for a picture of the door model swung on its hinge
            // (cc_task_open-questions-answers.md 2.1)
            if (DevArgs.OpenDoor) OpenTheDoor(board);

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
        }

        void OpenTheDoor(Game.Board.Board board)
        {
            var door = new Border(new Cell(7, 4), true);
            _field.OpenDoor(door);
            board.Reopen(_field.Map, door);
        }

        // the demonstration throw, and what space does
        void Throw()
        {
            if (_someone == null || Table.Busy) return;

            const int dc = 15;

            GD.Print("");
            GD.Print($"table   {_text.Get(Skills.NameKey(Skill.Stealth))} against DC {dc}, " +
                     $"modifier {_someone.CheckModifier(Skill.Stealth):+0;-0}");

            Table.Check(_someone, Skill.Stealth, dc, Advantage.Flat, attempt =>
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
