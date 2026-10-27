using System.Linq;
using Godot;

namespace Game.Dev
{
    // `--press A,B,C`: buttons pressed in order, by name, as each comes up - at the launch screen and on at the table, so
    // a picture can be of a screen a button opens (Preview, the test hero panel, a test at the table, its fight choice, the
    // live memory folded out). A toggle is flipped. For pictures only: nothing checks what a press did
    public partial class Presser : Node
    {
        static int _next;

        int _frame;

        static string[] Names => (DevArgs.Press ?? "").Split(',', System.StringSplitOptions.RemoveEmptyEntries);

        public override void _Process(double delta)
        {
            if (++_frame % 10 != 0 || _next >= Names.Length) return;

            // by its name, or by the words on it (the dialogue's Continue)
            BaseButton button = Nodes.Under<BaseButton>(GetTree().Root)
                                     .FirstOrDefault(b => (b.Name == Names[_next] || b is Button { Text: var t } && t == Names[_next]) && b.IsVisibleInTree());
            if (button == null) return;

            GD.Print($"press   {Names[_next]}");
            _next++;

            if (button is MenuButton menu) menu.ShowPopup();
            else if (button.ToggleMode) button.ButtonPressed = !button.ButtonPressed;
            else button.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }
}
