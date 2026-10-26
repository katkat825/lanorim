using System.Linq;
using Content.Maps;
using Core.Space;
using Game.Builder;
using Game.Screens;
using Godot;

namespace Game.Dev
{
    // THE PROBE'S HANDS (cc_task_m2 Part 8): a bar's button, a menu's item, a picker's prop, a click, a drag, a key - each
    // through the builder's own controls and Godot's own input, as an author's would be
    public partial class MapBuilderProbe
    {
        // --- the author's hands -------------------------------------------------------------------------------------

        T Named<T>(string name) where T : Node => Nodes.Under<T>(Builder).FirstOrDefault(n => n.Name == name);

        // a button on the screen with these words, pressed
        void Press(string key, params object[] args)
        {
            string words = Ui.Say(key, args);
            Button button = Nodes.Under<Button>(Builder).FirstOrDefault(b => b.Text == words && b.IsVisibleInTree());

            if (button == null)
            {
                Expect(false, $"a button saying '{words}'");
                return;
            }

            button.EmitSignal(BaseButton.SignalName.Pressed);
        }

        // a button of the bottom bar, by its name
        void Bar(string name)
        {
            Button button = Named<Button>(name);
            Expect(button != null, $"the bar's {name}");
            button?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        // an item of a bar button's menu (Terrain, Spawn), by its words
        void Menu(string name, string key, params object[] args) =>
            Item(Named<MenuButton>(name)?.GetPopup(), name, Ui.Say(key, args));

        // an item of the menu bar's menu (File, Edit, View), by its words
        void MenuItem(string menu, string key) =>
            Item(Named<MenuBar>("Menus")?.GetChildren().OfType<PopupMenu>().FirstOrDefault(p => p.Name == menu), menu, Ui.Say(key));

        void Item(PopupMenu popup, string where, string words)
        {
            int index = -1;

            for (int i = 0; popup != null && i < popup.ItemCount; i++)
                if (popup.GetItemText(i) == words) index = i;

            if (index < 0)
            {
                Expect(false, $"{where} has '{words}'");
                return;
            }

            popup.EmitSignal(PopupMenu.SignalName.IdPressed, popup.GetItemId(index));
        }

        // the Props or Buildings picker from the bar, the thing found by its search
        void PickProp(string id, bool buildings) => PickProp(id, buildings ? PropShelf.Buildings : PropShelf.Props);

        void PickProp(string id, PropShelf shelf)
        {
            (string bar, string picker) = shelf switch
            {
                PropShelf.Buildings => ("Buildings", MapBuilderUi.BuildingsPicker),
                PropShelf.People => ("Npcs", MapBuilderUi.NpcsPicker),
                _ => ("Props", MapBuilderUi.PropsPicker),
            };

            Bar(bar);
            Expect(Builder.Panels.OpenName == picker, $"the {bar} picker opened");

            LineEdit search = Nodes.Under<LineEdit>(Builder).FirstOrDefault(l => l.Name == "Search");
            if (search != null)
            {
                search.Text = Ui.Say(Editor.Palette.Find(id).NameKey);
                search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            }

            Press(Editor.Palette.Find(id).NameKey);
            Expect(Editor.PropId == id && Editor.InHand, $"the picker put the {id} in hand");
        }

        // the Walls and doors picker's Wall and Door buttons that look pressed: "wall", "door", or both
        string PressedLines() =>
            string.Join(",", Nodes.Under<Button>(Builder).Where(b => b.Name.ToString().StartsWith("Line_") && b.ButtonPressed)
                                  .Select(b => b.Name.ToString()["Line_".Length..]));

        // every style card the Walls and doors picker offers, page by page, back on its first page after
        System.Collections.Generic.List<string> StyleCards()
        {
            var seen = new System.Collections.Generic.List<string>();
            CardPages cards = Nodes.Under<CardPages>(Builder).FirstOrDefault(c => c.Name == "Styles");
            if (cards == null) return seen;

            Button back = Nodes.Under<Button>(cards).First(b => b.Name == "PageBack");
            Button on = Nodes.Under<Button>(cards).First(b => b.Name == "PageOn");

            while (!back.Disabled) back.EmitSignal(BaseButton.SignalName.Pressed);

            while (true)
            {
                seen.AddRange(cards.Showing.Select(b => b.Name.ToString()));
                if (on.Disabled) break;
                on.EmitSignal(BaseButton.SignalName.Pressed);
            }

            while (!back.Disabled) back.EmitSignal(BaseButton.SignalName.Pressed);
            return seen;
        }

        Vector2 Centre(Cell cell) => OnScreen(Builder.Table.Board.Where(cell));

        Vector2 Line(Border line) => OnScreen(Builder.Table.Board.Metrics.Centre(line));

        Vector2 OnScreen(Vector3 local) =>
            GetViewport().GetCamera3D().UnprojectPosition(Builder.Table.Board.ToGlobal(local));

        // in the viewport's own coordinates (true): pushed as the window's, they are scaled from the window to the
        // 1920x1080 canvas, and a headless window is small
        void Click(Vector2 at, bool ctrl = false) => Drag(at, at, ctrl);

        void Drag(Vector2 from, Vector2 to, bool ctrl = false)
        {
            Mouse(from, true, ctrl: ctrl);
            GetViewport().PushInput(new InputEventMouseMotion { Position = to, GlobalPosition = to, ButtonMask = MouseButtonMask.Left }, true);
            Mouse(to, false, ctrl: ctrl);
        }

        void Mouse(Vector2 at, bool down, bool shift = false, bool ctrl = false) =>
            GetViewport().PushInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left, Pressed = down, Position = at, GlobalPosition = at, ShiftPressed = shift,
                CtrlPressed = ctrl,
            }, true);

        void Key(Key key, bool ctrl, bool shift = false) =>
            GetViewport().PushInput(new InputEventKey
            {
                Keycode = key, PhysicalKeycode = key, Pressed = true, CtrlPressed = ctrl, ShiftPressed = shift,
            });
    }
}
