using System;
using Content.Maps;
using Core.Space;

namespace Game.Dev
{
    // EVERY WALL STYLE WITH ITS DOOR (cc_task_m3 Part 6: "only offer what we have"), for a picture of each:
    //
    //   godot --path game res://table.tscn -- --wall-styles --shot styles.png
    //
    // on grass, a row a style, top to bottom in the enum's order: two wall lines, the door (where the style has one), a
    // wall, the archway and the window (where it has them, cc_task_m4 Part 4), a wall, and a line turning the corner, so a
    // wall's joins and each opening's fit in the run are all in the picture
    public partial class DemoTable
    {
        static MapDraft StyleSampler()
        {
            WallStyle[] styles = Enum.GetValues<WallStyle>();
            var draft = new MapDraft(12, styles.Length * 2 + 2);

            draft.Paint(new Cell(0, 0), new Cell(draft.Columns - 1, draft.Rows - 1), Brush.Of(Ground.Grass));

            for (int i = 0; i < styles.Length; i++)
            {
                WallStyle style = styles[i];
                int y = 1 + i * 2;

                for (int x = 1; x <= 8; x++)
                    draft.Wall(new Border(new Cell(x, y), false), Edge.Wall, style);

                if (style.HasDoor()) draft.Wall(new Border(new Cell(3, y), false), Edge.Door, style);
                if (style.HasArchway()) draft.Wall(new Border(new Cell(5, y), false), Edge.Archway, style);
                if (style.HasWindow()) draft.Wall(new Border(new Cell(6, y), false), Edge.Window, style);

                draft.Wall(new Border(new Cell(9, y), true), Edge.Wall, style);
            }

            draft.PlaceStart(new Cell(8, draft.Rows - 1));
            return draft;
        }
    }
}
