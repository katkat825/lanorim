using System;
using System.Collections.Generic;
using System.Linq;
using Core.Space;

namespace Content.Maps
{
    // what a click on the map does
    public enum MapTool
    {
        // squares: the chosen ground (floor, difficult, rock)
        Paint,

        // the lines between squares: a wall or a door
        Wall,

        // a prop from the palette on a square
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
    public sealed class MapEditor
    {
        public MapEditor(MapDraft draft, PropCatalogue palette = null)
        {
            Draft = draft ?? throw new ArgumentNullException(nameof(draft));
            Palette = palette ?? PropCatalogue.Srd();
        }

        public MapDraft Draft { get; }

        public PropCatalogue Palette { get; }

        public MapTool Tool { get; set; } = MapTool.Paint;

        public Tile Ground { get; set; } = Tile.Floor;

        public Edge Line { get; set; } = Edge.Wall;

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

        // Q and E: a prop turns a quarter at a time
        public int Rotate(int quarters) => Turn = ((Turn + quarters) % 4 + 4) % 4;

        // a click on a square
        public bool Click(Cell cell)
        {
            switch (Tool)
            {
                case MapTool.Paint: return Draft.Paint(cell, Ground);
                case MapTool.Prop: return PropId != null && Draft.PlaceProp(PropId, cell, Turn);
                case MapTool.Spawn: return Draft.PlaceSpawn(SpawnSlot, cell);
                case MapTool.Start: return Draft.PlaceStart(cell);
                case MapTool.Erase: return Erase(cell);
                default: return false;
            }
        }

        // a click on the line between two squares
        public bool Click(Border border) =>
            Tool == MapTool.Wall ? Draft.Wall(border, Line)
            : Tool == MapTool.Erase && Draft.At(border) != Edge.None && Draft.Wall(border, Edge.None);

        // a drag: painting fills the rectangle, erasing clears it; the other tools act on the square
        // the drag ended on
        public int Drag(Cell from, Cell to)
        {
            if (Tool == MapTool.Paint) return Draft.Paint(from, to, Ground);

            if (Tool == MapTool.Erase)
            {
                int erased = 0;

                for (int y = Math.Min(from.Y, to.Y); y <= Math.Max(from.Y, to.Y); y++)
                    for (int x = Math.Min(from.X, to.X); x <= Math.Max(from.X, to.X); x++)
                        if (Erase(new Cell(x, y))) erased++;

                return erased;
            }

            return Click(to) ? 1 : 0;
        }

        bool Erase(Cell cell)
        {
            if (Draft.ClearProps(cell) > 0) return true;

            foreach (KeyValuePair<int, Cell> spawn in Draft.Spawns.ToList())
                if (spawn.Value == cell) return Draft.ClearSpawn(spawn.Key);

            // the start cannot be erased, only moved: a map always has one
            if (cell == Draft.Start) return false;

            return Draft.At(cell) != Tile.Void && Draft.Paint(cell, Tile.Void);
        }

        // props on the map the palette doesn't know - drawn as a placeholder, and named
        public IEnumerable<string> UnknownProps =>
            Draft.Props.Select(p => p.Id).Where(id => !Palette.Has(id)).Distinct();

        public static readonly IReadOnlyList<MapTool> Tools = Enum.GetValues<MapTool>();

        public static string ToolKey(MapTool tool) =>
            Screens.ScreenKeys.Key("map", "tool_" + tool.ToString().ToLowerInvariant());

        public static IEnumerable<string> Keys() => Tools.Select(ToolKey);
    }
}
