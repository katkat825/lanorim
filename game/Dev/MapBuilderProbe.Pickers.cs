using System.Linq;
using Content.Maps;
using Content.Screens;
using Core.Space;
using Game.Builder;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // THE PROBE AT THE TWO PICKERS cc_task_m3 added or remade, the Walls and doors picker (Part 6) and the people (Part
    // 10), apart from MapBuilderProbe.cs, which they would have taken past 400 lines
    public partial class MapBuilderProbe
    {
        // THE WALLS AND DOORS PICKER (cc_task_m3 Part 6): Wall or Door, one pressed at a time; Door offers only the styles
        // with a door; a style's card takes up the wall and closes the picker; Esc shuts it
        void Fence()
        {
            Bar("WallsAndDoors");
            Expect(Builder.Panels.OpenName == MapBuilderUi.WallsPanel, "Walls and doors opened its picker");

            Press(MapBuilderView.LineKey(Edge.Door));
            Expect(Editor.Line == Edge.Door && PressedLines() == "door", $"Door pressed alone (pressed: {PressedLines()})");
            Expect(!StyleCards().Contains("Style_town_wall") && !StyleCards().Contains("Style_ruin_low") && StyleCards().Contains("Style_ruin"),
                   $"Door offers only the styles with a door ({string.Join(", ", StyleCards())})");

            Press(MapBuilderView.LineKey(Edge.Wall));
            Expect(Editor.Line == Edge.Wall && PressedLines() == "wall", $"Wall pressed alone, Door let go (pressed: {PressedLines()})");
            Expect(StyleCards().Contains("Style_town_wall"), "and Wall offers every style");

            Press(MapBuilderView.StyleKey(WallStyle.Fence));
            Expect(Editor.Tool == MapTool.Wall && Editor.Style == WallStyle.Fence, "a style's card took up the wall in that style");
            Expect(Builder.Panels.Open == null, "and closed the picker");

            Drag(Line(new Border(new Cell(7, 2), true)), Line(new Border(new Cell(7, 4), true)));
            Expect(Enumerable.Range(2, 3).All(y => Draft.StyleAt(new Border(new Cell(7, y), true)) == WallStyle.Fence), "the drag laid a run of fence");
            Expect(Draft.Layout().At(new Border(new Cell(7, 3), true)) == Edge.Bars, "which the rules see through");

            // Esc puts the wall in hand down first, as it does a prop (Kathleen's UI notes, 2026-10-27), then shuts the picker
            Bar("WallsAndDoors");
            Key(Godot.Key.Escape, ctrl: false);
            Expect(Editor.Tool == MapTool.Select && Builder.Panels.Open != null, "Esc put the wall down and left the picker open");
            Key(Godot.Key.Escape, ctrl: false);
            Expect(Builder.Panels.Open == null, "a second Esc shut the picker");
        }

        // SOMEONE ON THE MAP (cc_task_m3 Part 10): the NPCs button opens the people; one placed is selected; it turns, says
        // Who (in My maps only Someone), stands on the board as a mini, and Delete takes it off
        void Person()
        {
            Expect(Named<Button>("Npcs") != null, "an NPCs button on the bar");
            PickProp("npc_chef_female", PropShelf.People);

            Click(Centre(new Cell(8, 6)));
            Expect(Editor.Picked?.Id == "npc_chef_female" && Editor.Picked.Cell == new Cell(8, 6), "the cook stands where it was clicked, selected");
            Expect(Builder.Table.Board.FindChild("npc_chef_female_8_6", true, false) is Game.Board.Mini, "as a mini on the board");

            Press(MapBuilderView.RotateKey);
            Expect(Editor.Picked?.Turn == 1, "Turn turned it");

            OptionButton who = Named<OptionButton>("Who");
            Expect(who != null && who.ItemCount == 1 && who.GetItemText(0) == Ui.Say(MapBuilderView.SomeoneKey), "Who offers only Someone in My maps");
            Expect(Draft.Layout().Blocked.Contains(new Cell(8, 6)), "and blocks its square");

            Press(MapBuilderView.DeleteKey);
            Expect(!Draft.Props.Any(p => p.Id == "npc_chef_female"), "Delete took it off");

            Key(Godot.Key.Escape, ctrl: false);
        }
    }
}
