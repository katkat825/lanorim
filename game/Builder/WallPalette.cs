using System;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Core.Space;
using Core.Words;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // THE WALLS AND DOORS PICKER (cc_task_m3 Part 6), like the Buildings and Props pickers: Wall or Door at the top, one
    // pressed at a time, then the styles as cards, each with a picture of its wall (or its door), a page at a time
    // (CardPages). Door offers only the styles that have a door (MapEditor.StylesFor). Picking Wall or Door takes up the
    // Wall tool and keeps the picker open; picking a style takes it up too and closes the picker, as choosing a prop does.
    // The pictures are rendered once into res://ui/prop_icons/walls/ (`--prop-icons`, Dev/PropIcons.cs)
    public partial class WallPalette : VBoxContainer
    {
        public const string IconFolder = PropPalette.IconFolder + "walls/";

        public static string Picture(WallStyle style, Edge line) =>
            IconFolder + style.Id() + (line == Edge.Door ? "_door" : "_wall") + ".png";

        public MapEditor Editor { get; set; }

        // a wall or a door chosen: the Wall tool taken up, the picker open
        public Action Chose { get; set; }

        // a style chosen: the Wall tool taken up, the picker closed
        public Action Picked { get; set; }

        CardPages _cards;
        readonly ButtonGroup _lines = new() { AllowUnpress = false };
        readonly System.Collections.Generic.List<(Edge Line, Button Button)> _lineButtons = new();

        public override void _Ready()
        {
            BuilderLayout layout = BuilderLayout.Current;

            var lines = Ui.Row(layout.Gap);
            lines.Name = "Lines";
            lines.Alignment = AlignmentMode.Center;

            foreach (Edge line in MapBuilderView.Lines)
            {
                Edge one = line;
                Button button = Ui.Button(MapBuilderView.LineKey(one), () =>
                {
                    Editor.PickLine(one);
                    Fill();
                    Update();
                    Chose?.Invoke();
                });
                button.Name = "Line_" + one.Id();
                button.ThemeTypeVariation = "HudButton";
                button.ToggleMode = true;
                button.ButtonGroup = _lines;
                lines.AddChild(button);
                _lineButtons.Add((one, button));
            }

            AddChild(Ui.Column(layout.Gap, MapBuilderUi.Heading(MapEditor.ToolKey(MapTool.Wall)), lines,
                               MapBuilderUi.Light(Ui.Label(MapBuilderView.StylesKey))));

            _cards = new CardPages { Name = "Styles" };
            AddChild(_cards);

            Fill();
            _cards.ShowChosen();
            Update();
        }

        // the styles the line has, as cards with its picture
        void Fill() =>
            _cards.Show(MapEditor.StylesFor(Editor.Line)
                                 .Select(style => new CardPages.Card(
                                     "Style_" + style.Id(), Ui.Say(MapBuilderView.StyleKey(style)), Picture(style, Editor.Line),
                                     () =>
                                     {
                                         Editor.PickStyle(style);
                                         Picked?.Invoke();
                                     },
                                     () => Editor.Style == style))
                                 .ToList(), fromTheStart: false);

        // the line chosen pressed, and only it; the style chosen pressed, and only it
        public void Update()
        {
            foreach ((Edge line, Button button) in _lineButtons.Where(b => IsInstanceValid(b.Button)))
                button.SetPressedNoSignal(Editor.Line == line);

            _cards?.Update();
        }
    }
}
