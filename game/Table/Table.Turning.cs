using Core.Words;
using Game.Access;
using Godot;

namespace Game.Table
{
    public partial class Table
    {
        // --- turning the table --------------------------------------------------------------------

        // BY THE ACT, NOT BY THE KEYCODE. Every one of these is in the InputMap under its own
        // name, so rebinding it on the accessibility page rebinds it here too - a control the
        // keyboard cannot reach is not a control, it is a mouse gesture (Act).
        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey { Pressed: true, Echo: false }) return;

            if (Pressed(@event, Act.TurnLeft)) Camera?.Turn(-1);
            else if (Pressed(@event, Act.TurnRight)) Camera?.Turn(1);
            else if (Pressed(@event, Act.ZoomIn)) Camera?.ZoomBy(0.12f);
            else if (Pressed(@event, Act.ZoomOut)) Camera?.ZoomBy(-0.12f);
            else if (Pressed(@event, Act.ThrowDice)) Throw();
            else return;

            GetViewport().SetInputAsHandled();
        }

        // an act with no entry in the InputMap is a control nobody can reach, so it is shouted
        // about once rather than silently doing nothing
        static bool Pressed(InputEvent @event, Act act)
        {
            string action = act.Id();

            if (InputMap.HasAction(action)) return @event.IsActionPressed(action);

            GD.PushError($"table: '{action}' is not in the InputMap - add it to project.godot " +
                         "or nothing on the keyboard does it");

            return false;
        }
    }
}
