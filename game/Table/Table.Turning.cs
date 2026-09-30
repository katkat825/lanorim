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
        //
        // THE WHEEL AND THE PINCH ZOOM TOO, everywhere on the table. While a line or a cone is being
        // aimed the combat director takes the plain wheel to turn it (it sees the event first, being
        // deeper in the tree) and leaves Shift+wheel to come here.
        public override void _UnhandledInput(InputEvent @event)
        {
            if (Camera != null && Zoomed(@event))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event is not InputEventKey { Pressed: true, Echo: false }) return;

            if (Pressed(@event, Act.TurnLeft)) Camera?.Turn(-1);
            else if (Pressed(@event, Act.TurnRight)) Camera?.Turn(1);
            else if (Pressed(@event, Act.ZoomIn)) Camera?.Step(1);
            else if (Pressed(@event, Act.ZoomOut)) Camera?.Step(-1);
            else if (Pressed(@event, Act.ThrowDice)) Throw();
            else return;

            GetViewport().SetInputAsHandled();
        }

        // the mouse wheel (with or without Shift), a trackpad's pinch, and its two-finger scroll
        bool Zoomed(InputEvent @event)
        {
            switch (@event)
            {
                case InputEventMouseButton { Pressed: true } wheel
                    when wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown:
                    Camera.Step((wheel.ButtonIndex == MouseButton.WheelUp ? 1 : -1) * Camera.WheelStep *
                                Mathf.Max(1f, wheel.Factor));
                    return true;

                case InputEventMagnifyGesture pinch:
                    Camera.ZoomBy((pinch.Factor - 1f) * Camera.PinchStep * Camera.ZoomStep);
                    return true;

                case InputEventPanGesture pan when Mathf.Abs(pan.Delta.Y) > Mathf.Abs(pan.Delta.X):
                    Camera.Step(-pan.Delta.Y * Camera.PanStep);
                    return true;

                default:
                    return false;
            }
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
