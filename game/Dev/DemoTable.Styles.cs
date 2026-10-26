using System;
using Content.Maps;
using Core.Space;

namespace Game.Dev
{
    // EVERY WALL STYLE WITH ITS DOOR (cc_task_m3 Part 6: "only offer what we have"), for a picture of each:
    //
    //   godot --path game res://table.tscn -- --wall-styles --shot styles.png
    //
    // on grass, a row a style, top to bottom in the enum's order: two wall lines, the door (where the style has one),
    // two more, and a line turning the corner, so a wall's joins and its door's fit in the run are both in the picture
    public partial class DemoTable
    {
        static MapDraft StyleSampler()
        {
            WallStyle[] styles = Enum.GetValues<WallStyle>();
            var draft = new MapDraft(10, styles.Length * 2 + 2);

            draft.Paint(new Cell(0, 0), new Cell(draft.Columns - 1, draft.Rows - 1), Brush.Of(Ground.Grass));

            for (int i = 0; i < styles.Length; i++)
            {
                WallStyle style = styles[i];
                int y = 1 + i * 2;

                for (int x = 1; x <= 5; x++)
                    draft.Wall(new Border(new Cell(x, y), false),
                               x == 3 && style.HasDoor() ? Edge.Door : Edge.Wall, style);

                draft.Wall(new Border(new Cell(6, y), true), Edge.Wall, style);
            }

            draft.PlaceStart(new Cell(8, draft.Rows - 1));
            return draft;
        }
    }
}
