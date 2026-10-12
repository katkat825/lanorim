using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Space;
using Core.Words;

namespace Content.Maps
{
    // what a click on the map does
    public enum MapTool
    {
        // a rectangle to copy, move or clear, or a placed prop picked up to turn, move or take away (cc_task_l Part 4)
        Select,

        // squares: a ground (grass, planks...) or a rule (difficult, rock, chasm)
        Paint,

        // the same, over every joined square of one ground, up to the walls (cc_task_l Part 4.2)
        Fill,

        // the lines between squares: a wall or a door, in a style
        Wall,

        // a prop from the palette on a square, or a scatter of them along a Shift-drag
        Prop,

        // a numbered monster spawn
        Spawn,

        // where the hero starts
        Start,

        // takes back whatever is there: a prop, then a spawn, then the square itself
        Erase,
    }

    // THE MAP BUILDER'S TOOL MODES (Tier 2.10), on MapDraft: pick a tool and what it lays, then click
    // a square or a line, or drag a rectangle. Every change goes through MapDraft, so undo and redo and
    // the problems check are the draft's. The Godot editor is a view over this.
    public sealed partial class MapEditor
    {
        public MapEditor(MapDraft draft, PropCatalogue palette = null)
        {
            Draft = draft ?? throw new ArgumentNullException(nameof(draft));
            Palette = palette ?? draft.Palette;

            // the draft judges which props block by the palette the author is placing from
            draft.Palette = Palette;
        }

        public MapDraft Draft { get; }

        public PropCatalogue Palette { get; }

        public MapTool Tool { get; set; } = MapTool.Paint;

        // what Paint and Fill lay: a ground, or a rule on its own
        public Brush Brush { get; set; } = Brush.Of(Tile.Floor);

        public Edge Line { get; set; } = Edge.Wall;

        // what the Wall tool lays its walls in (a door takes its wall's)
        public WallStyle Style { get; set; } = WallStyle.Stone;

        public string Category { get; private set; }

        public string PropId { get; private set; }

        public int Turn { get; private set; }

        public int SpawnSlot { get; set; } = MapDraft.FirstSpawn;

        // the palette's props in the chosen category
        public IEnumerable<PropEntry> Showing =>
            Category == null ? Palette.Props : Palette.In(Category);

        public void Browse(string category) => Category = Palette.Categories.Contains(category) ? category : null;

        public bool Choose(string propId)
        {
            if (!Palette.Has(propId)) return false;

            PropId = propId;
            Tool = MapTool.Prop;
            return true;
        }

        // R and Shift+R: a quarter at a time. the prop in hand, or the one picked up with Select
        public int Rotate(int quarters)
        {
            if (Tool == MapTool.Select && Picked != null)
            {
                Picked = Draft.TurnProp(Picked, quarters) ?? Picked;
                return Picked.Turn;
            }

            return Turn = ((Turn + quarters) % 4 + 4) % 4;
        }

        // a click on a square
        public bool Click(Cell cell)
        {
            switch (Tool)
            {
                case MapTool.Paint: return Draft.Paint(cell, cell, Brush) > 0;
                case MapTool.Fill: return Draft.Fill(cell, Brush) > 0;
                case MapTool.Prop: return PropId != null && Draft.PlaceProp(PropId, cell, Turn);
                case MapTool.Spawn: return Draft.PlaceSpawn(SpawnSlot, cell);
                case MapTool.Start: return Draft.PlaceStart(cell);
                case MapTool.Erase: return Erase(cell);
                case MapTool.Select: Pick(cell); return false;
                default: return false;
            }
        }

        // a click on the line between two squares
        public bool Click(Border border) =>
            Tool == MapTool.Wall ? Draft.Wall(border, Line, Style)
            : Tool == MapTool.Erase && Draft.At(border) != Edge.None && Draft.Wall(border, Edge.None);

        // a drag: painting fills the rectangle, erasing clears it, selecting picks it (or moves what was picked); the
        // other tools act on the square the drag ended on
        public int Drag(Cell from, Cell to)
        {
            if (Tool == MapTool.Paint) return Draft.Paint(from, to, Brush);

            if (Tool == MapTool.Select) return Grab(from, to);

            if (Tool == MapTool.Erase)
            {
                int erased = 0;

                foreach (Cell cell in CellBox.Between(from, to).Cells)
                    if (Erase(cell)) erased++;

                return erased;
            }

            return Click(to) ? 1 : 0;
        }

        bool Erase(Cell cell)
        {
            if (Draft.PropAt(cell) is Prop prop) return Draft.RemoveProp(prop);

            foreach (KeyValuePair<int, Cell> spawn in Draft.Spawns.ToList())
                if (spawn.Value == cell) return Draft.ClearSpawn(spawn.Key);

            // the start cannot be erased, only moved: a map always has one
            if (cell == Draft.Start) return false;

            return Draft.At(cell) != Tile.Void && Draft.Paint(cell, Tile.Void);
        }

        // --- scatter (cc_task_l Part 4.5) ---------------------------------------------------------------------------

        // how far apart a scatter drops its props: every other square, so trees stand in a wood and not a wall
        public const int ScatterSpacing = 2;

        // A SHIFT-DRAG WITH A PROP IN HAND: the prop dropped along the path every few squares, each at a turn of its
        // own, as one undo step. the seed makes the turns the same every time for the same stroke. how many stood
        public int Scatter(IReadOnlyList<Cell> path, int seed)
        {
            if (PropId == null || path == null || path.Count == 0) return 0;

            var dice = new SeededRng(seed);
            var dropped = new List<Cell>();

            Draft.Change(() =>
            {
                foreach (Cell cell in path.Distinct())
                {
                    if (dropped.Any(d => Math.Max(Math.Abs(d.X - cell.X), Math.Abs(d.Y - cell.Y)) < ScatterSpacing)) continue;

                    if (Draft.PlaceProp(PropId, cell, dice.Roll(4) - 1)) dropped.Add(cell);
                }
            });

            return dropped.Count;
        }

        // props on the map the palette doesn't know - drawn as a placeholder, and named
        public IEnumerable<string> UnknownProps =>
            Draft.Props.Select(p => p.Id).Where(id => !Palette.Has(id)).Distinct();

        public static readonly IReadOnlyList<MapTool> Tools = Enum.GetValues<MapTool>();

        public static string ToolKey(MapTool tool) =>
            Screens.ScreenKeys.Key("map", "tool_" + EnumWords.Name(tool));

        public static IEnumerable<string> Keys() => Tools.Select(ToolKey);
    }
}
