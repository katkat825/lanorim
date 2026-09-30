using System;
using System.Collections.Generic;
using System.Linq;
using Core.Words;
using Game.Access;
using Game.Play;
using Godot;

namespace Game.Screens
{
    // DOES THE CONTROLS SECTION LIST EVERYTHING, AND DOES CHANGE WORK? (cc_task_table-ui-minis-zoom-damage.md 2b)
    //
    //   godot --headless --path game res://launch.tscn -- --controls
    //
    // Every act has a row, every row names a key that is bound, no line shows a raw {0}, and a
    // rebinding made the player's way - Change, then a key press through the input system - lands in
    // the InputMap with the number pad's extra zoom key kept beside it. Plays apart from the player's
    // own settings file (GameState.SaveProbesApart) and says "controls ok" or what is wrong.
    public partial class ControlsProbe : Node
    {
        public const string Flag = "--controls";

        public override async void _Ready()
        {
            GameState.SaveProbesApart();

            var problems = new List<string>();
            var section = new ControlsSection();
            AddChild(section);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            foreach (Act act in Enum.GetValues<Act>())
            {
                if (!section.ActKeys.TryGetValue(act, out string keys)) problems.Add($"'{act.Id()}' has no row");
                else if (string.IsNullOrWhiteSpace(keys)) problems.Add($"'{act.Id()}' names no key");
                else GD.Print($"controls  {act.Id(),-14} {keys}");
            }

            foreach (string line in section.Lines)
                if (string.IsNullOrWhiteSpace(line) || line.Contains('{') || line.StartsWith("ui."))
                    problems.Add($"a line reads '{line}'");

            // Change on zoom in, then K, the way a player presses it
            section.Listen(Act.ZoomIn);
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.K, Keycode = Key.K, Pressed = true });

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            List<string> zoom = Keyboard.Keys(Act.ZoomIn.Id()).ToList();
            GD.Print($"controls  rebound zoom_in -> {string.Join(" / ", zoom)}");

            if (zoom.FirstOrDefault() != "K") problems.Add("Change then K did not put zoom_in on K");
            if (!zoom.Contains(Keyboard.Label(Key.KpAdd))) problems.Add("the number pad's + was lost from zoom_in");
            if (!section.ActKeys[Act.ZoomIn].StartsWith("K")) problems.Add("the row did not show the new key");

            foreach (string problem in problems) GD.PrintErr("controls  " + problem);

            GD.Print(problems.Count == 0
                ? $"controls ok - {section.ActKeys.Count} acts, {section.Lines.Count} lines"
                : $"controls FAILED - {problems.Count} problem(s)");

            GameState.ForgetProbeSaves();
            GetTree().Quit(problems.Count == 0 ? 0 : 1);
        }
    }
}
