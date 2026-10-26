using System;
using System.Collections.Generic;
using Content.Maps;
using Content.Screens;
using Core.Space;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // ABOVE THE BOTTOM BAR, ONE AT A TIME (cc_task_m2 Part 8): the Buildings picker and the Props picker (PropPalette:
    // thumbnails, the search box, the categories as tabs, pages), each closing when an item is chosen or Esc is pressed;
    // the Walls and doors picker (WallPalette, cc_task_m3 Part 6: a wall or a door, then its styles as cards); and the
    // problems, unfolded, each one a button that shows where. Only while one is open does anything stand over the map
    public partial class MapBuilderUi
    {
        public const string BuildingsPicker = "BuildingsPicker";
        public const string NpcsPicker = "NpcsPicker";
        public const string PropsPicker = "PropsPicker";
        public const string WallsPanel = "WallsPanel";
        public const string ProblemsPanel = "ProblemsPanel";

        CenterContainer _above;

        // what the open one checks to show its choices pressed
        readonly List<Action> _openUpdates = new();

        // the open picker or pop-up, or null; and its name
        public Control Open { get; private set; }

        public string OpenName => Open?.Name;

        // open this one (or close it, when it's the one open)
        public void Toggle(string name)
        {
            if (OpenName == name)
            {
                Close();
                return;
            }

            switch (name)
            {
                case BuildingsPicker: Show(name, Picker(PropShelf.Buildings)); break;
                case PropsPicker: Show(name, Picker(PropShelf.Props)); break;
                case NpcsPicker: Show(name, Picker(PropShelf.People)); break;
                case WallsPanel: Show(name, Walls()); break;
                case ProblemsPanel: Show(name, ProblemsList()); break;
            }

            Refresh();
        }

        void Show(string name, Control content)
        {
            Close(refresh: false);

            PanelContainer panel = Ui.Panel(content, "HudPanel");
            panel.Name = name;
            panel.CustomMinimumSize = new Vector2(BuilderLayout.Current.PickerWidth, 0);

            Open = panel;
            _above.AddChild(panel);
        }

        public void Close() => Close(refresh: true);

        void Close(bool refresh)
        {
            // out of the tree at once: a new one of the same name, put up in the same frame, keeps its name
            if (Open != null && IsInstanceValid(Open))
            {
                Open.GetParent()?.RemoveChild(Open);
                Open.QueueFree();
            }

            Open = null;
            _openUpdates.Clear();

            if (refresh && _title != null) Refresh();
        }

        // A PICKER: the buildings, or every other prop. Choosing one puts it in hand and closes the picker; the next
        // click on the map places one and selects it (MapEditor.Apply), Ctrl held places more
        Control Picker(PropShelf shelf)
        {
            var palette = new PropPalette
            {
                Name = "Palette",
                Editor = Editor,
                Shelf = shelf,
                Picked = () =>
                {
                    Close(refresh: false);
                    Builder.Chose();
                },
            };

            _openUpdates.Add(palette.Update);
            return palette;
        }

        // WALLS AND DOORS (cc_task_m3 Part 6): a picker like the others, Wall or Door, then the styles as cards
        // (WallPalette). Either takes up the Wall tool; a style chosen closes it
        Control Walls()
        {
            var palette = new WallPalette
            {
                Name = "Palette",
                Editor = Editor,
                Chose = Walling,
                Picked = () =>
                {
                    Close(refresh: false);
                    Walling();
                    Refresh();
                },
            };

            _openUpdates.Add(palette.Update);
            return palette;
        }

        void Walling()
        {
            if (Editor.Tool != MapTool.Wall)
            {
                Editor.Tool = MapTool.Wall;
                Editor.Deselect();
                Builder.Chose();
            }
            else Refresh();
        }

        // THE PROBLEMS, UNFOLDED: each one a button that shows where it is
        Control ProblemsList()
        {
            int gap = BuilderLayout.Current.Gap;
            var list = Ui.Column(gap / 2);

            IReadOnlyList<MapProblem> problems = Builder.View.Problems;

            if (problems.Count == 0) list.AddChild(Light(Ui.Label(MapBuilderView.NoProblemsKey)));

            foreach (MapProblem problem in problems)
            {
                MapProblem one = problem;
                Button button = Ui.Button(Game.Access.AccessDesk.Words(one.Said), () => Builder.Show(one), true);
                button.Alignment = HorizontalAlignment.Left;
                button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                button.Disabled = one.At == null;
                list.AddChild(button);
            }

            // as tall as the list, up to PopupHeight; then it scrolls
            return Ui.Column(gap, Heading(MapBuilderView.ProblemsKey),
                             problems.Count == 0 ? list : Ui.Scroll(list, BuilderLayout.Current.PopupHeight));
        }

        // words on the bars' dark panels: the HUD's light letters
        internal static Label Heading(string key)
        {
            Label heading = Ui.Label(key);
            heading.ThemeTypeVariation = "HudHeading";
            return heading;
        }

        internal static Label Light(Label words)
        {
            words.ThemeTypeVariation = "HudLabel";
            return words;
        }
    }
}
