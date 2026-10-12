using System;
using Content.Maps;
using Content.Screens;
using Core.Space;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // WHAT THE TOOL IN HAND LAYS (cc_task_f Part 2; cc_task_l): a ground or a rule for Paint and Fill, a wall or a door
    // and its style for Walls, a spawn's number, the prop palette (PropPalette) and its turn, the Select tool's verbs
    public partial class MapBuilderUi
    {
        MapTool? _optionsFor;

        void Options()
        {
            // rebuilt only when the tool changes, so a press isn't lost to a rebuild
            if (_optionsFor == Editor.Tool) return;

            _optionsFor = Editor.Tool;
            _optionUpdates.Clear();

            Ui.Clear(_options);

            switch (Editor.Tool)
            {
                case MapTool.Paint:
                case MapTool.Fill:
                    _options.AddChild(Ui.Label(MapBuilderView.LooksKey));
                    foreach (Ground ground in MapBuilderView.Looks)
                        Option(MapBuilderView.GroundKey(ground), () => Editor.Brush = Brush.Of(ground),
                               () => Editor.Brush.Look == ground);

                    _options.AddChild(Ui.Label(MapBuilderView.RulesKey));
                    foreach (Tile rule in MapBuilderView.Rules)
                        Option(MapBuilderView.RuleKey(rule), () => Editor.Brush = Brush.Of(rule),
                               () => Editor.Brush.Rule == rule);
                    break;

                case MapTool.Wall:
                    foreach (Edge line in MapBuilderView.Lines)
                        Option(MapBuilderView.LineKey(line), () => Editor.Line = line, () => Editor.Line == line);

                    _options.AddChild(Ui.Label(MapBuilderView.StylesKey));
                    foreach (WallStyle style in MapBuilderView.Styles)
                        Option(MapBuilderView.StyleKey(style), () => Editor.Style = style, () => Editor.Style == style);
                    break;

                case MapTool.Spawn:
                    foreach (int slot in MapBuilderView.SpawnSlots)
                        Option(MapBuilderView.SpawnKey, () => Editor.SpawnSlot = slot, () => Editor.SpawnSlot == slot, slot);
                    break;

                case MapTool.Prop:
                    var palette = new PropPalette { Name = "Palette", Editor = Editor, Picked = Refresh };
                    _options.AddChild(palette);
                    _optionUpdates.Add(palette.Update);
                    Turn();
                    _options.AddChild(Ui.Label(MapBuilderView.ScatterHintKey));
                    break;

                case MapTool.Select:
                    _options.AddChild(Ui.Label(MapBuilderView.SelectHintKey));
                    _options.AddChild(Ui.Row(BuilderLayout.Current.Gap,
                        Ui.Button(MapBuilderView.CopyKey, Builder.Copy),
                        Ui.Button(MapBuilderView.PasteKey, Builder.Paste),
                        Ui.Button(MapBuilderView.DeleteKey, Builder.Delete)));
                    Turn();
                    break;
            }
        }

        // the turn, in degrees: a number, not a word. the prop picked up, under Select; the one in hand, under Props
        void Turn()
        {
            Label turn = Ui.Plain("");
            _optionUpdates.Add(() => turn.Text = $"{(Editor.Tool == MapTool.Select ? Editor.Picked?.Turn ?? 0 : Editor.Turn) * 90}°");
            _options.AddChild(Ui.Row(BuilderLayout.Current.Gap, Ui.Button(MapBuilderView.RotateKey, () => Builder.Rotate(1)), turn));
        }

        void Option(string key, Action pick, Func<bool> chosen, params object[] args) =>
            _options.AddChild(Pressable(Ui.Say(key, args), pick, chosen));

        // a button on the options page that stays pressed while `chosen` is true
        Button Pressable(string words, Action pick, Func<bool> chosen)
        {
            Button button = Ui.Button(words, () => { pick(); Refresh(); }, true);
            button.ToggleMode = true;
            _optionUpdates.Add(() => button.SetPressedNoSignal(chosen()));
            return button;
        }
    }
}
