using System;
using System.Collections.Generic;
using System.Linq;
using Content.Maps;
using Content.Screens;
using Game.Access;
using Game.Screens;
using Godot;

namespace Game.Builder
{
    // THE TWO BARS (cc_task_m2 Part 8). Along the top, small like an application's: the menu bar (File: Save, Save as...,
    // Copy to campaign..., Play this map, Main menu; Edit: Undo, Redo, Erase; View: View from above, Player's view), the
    // map's place and name, and Problems with its count. Along the bottom, like the combat HUD's: Select; Terrain (Paint,
    // Fill, and the ground or rule they lay); Spawn (Hero start, Monster spawn 1-9); Buildings; Walls and doors; Props;
    // Leave. Above the bottom bar, what's chosen or in hand and what can be done with it (Context). Every item is a key
    // too (Act); a menu shows each one's key, and the keyboard works it (Alt or F10 opens the bar; arrows and Enter)
    public partial class MapBuilderUi
    {
        MenuBar _menus;
        Button _problemsButton;
        HBoxContainer _context;

        // --- the top ------------------------------------------------------------------------------------------------

        Control TopBar()
        {
            BuilderLayout layout = BuilderLayout.Current;

            _menus = new MenuBar { Name = "Menus", Flat = true };

            // the builder answers every key itself (MapBuilder.Do); the bar only shows them
            _menus.SetProcessShortcutInput(false);

            _menus.AddChild(Menu("File", MapBuilderView.MenuFileKey,
                (MapBuilderView.SaveKey, Act.SaveMap), (MapBuilderView.SaveAsKey, Act.SaveMapAs),
                (MapBuilderView.CopyToKey, Act.CopyMapTo), (MapTrial.PlayKey, Act.PlayMap),
                (CampaignBook.ContentsKey, Act.BuilderToMainMenu)));

            _menus.AddChild(Menu("Edit", MapBuilderView.MenuEditKey,
                (MapBuilderView.UndoKey, Act.UndoEdit), (MapBuilderView.RedoKey, Act.RedoEdit),
                (MapEditor.ToolKey(MapTool.Erase), Act.EraseTool)));

            _menus.AddChild(Menu("View", MapBuilderView.MenuViewKey,
                (MapBuilderView.TopDownKey, Act.ViewFromAbove), (MapBuilderView.PlayersViewKey, Act.PlayersView)));

            _title = Ui.Plain("");
            _title.Name = "MapTitle";
            _title.ThemeTypeVariation = "HudLabel";
            _title.AutowrapMode = TextServer.AutowrapMode.Off;
            _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _title.HorizontalAlignment = HorizontalAlignment.Center;
            _title.ClipText = true;

            _problemsButton = Ui.Button(MapBuilderView.ProblemsKey, () => Builder.Do(Act.BuilderProblems), true);
            _problemsButton.Name = "ProblemsButton";
            _problemsButton.ThemeTypeVariation = "HudButton";
            _problemsButton.ToggleMode = true;
            _updates.Add(() => _problemsButton.SetPressedNoSignal(OpenName == ProblemsPanel));

            PanelContainer bar = Ui.Panel(Ui.Row(layout.Gap, _menus, _title, _problemsButton), "HudPanel");
            bar.Name = "TopBar";
            bar.AnchorRight = 1;
            bar.OffsetLeft = layout.EdgeMargin;
            bar.OffsetRight = -layout.EdgeMargin;
            bar.OffsetTop = layout.EdgeMargin / 2;
            bar.OffsetBottom = layout.EdgeMargin / 2 + layout.TopBarHeight;

            return bar;
        }

        // one menu of the bar: its title, and each item with its act's key beside it
        PopupMenu Menu(string name, string titleKey, params (string Key, Act Act)[] items)
        {
            var menu = new PopupMenu { Name = name, Title = Ui.Say(titleKey) };

            for (int i = 0; i < items.Length; i++)
            {
                menu.AddItem(Ui.Say(items[i].Key), i);
                menu.SetItemAccelerator(i, Accelerator(items[i].Act));
            }

            menu.IdPressed += id => Builder.Do(items[(int)id].Act);
            return menu;
        }

        // an act's key as a menu shows it: Ctrl+Shift+S
        static Key Accelerator(Act act)
        {
            Bindings.Bound bound = Game.Play.GameState.Access.Keys.Of(act);

            if (!bound.Any) return Key.None;

            long key = (long)bound.Key;

            if (bound.Ctrl) key |= (long)KeyModifierMask.MaskCmdOrCtrl;
            if (bound.Shift) key |= (long)KeyModifierMask.MaskShift;

            return (Key)key;
        }

        // ALT OR F10: the menu bar opens on File, and the arrows and Enter work it (PopupMenu's own)
        public void OpenMenus()
        {
            if (_menus.GetMenuCount() == 0) return;

            Close();
            PopupMenu file = _menus.GetMenuPopup(0);
            Rect2 at = _menus.GetGlobalRect();
            file.Popup(new Rect2I((Vector2I)(at.Position + new Vector2(0, at.Size.Y)), Vector2I.Zero));
            file.SetFocusedItem(0);
        }

        // --- the bottom ---------------------------------------------------------------------------------------------

        Control BottomBar()
        {
            BuilderLayout layout = BuilderLayout.Current;

            var bar = Ui.Row(layout.Gap);
            bar.Name = "Bar";

            bar.AddChild(Pressing(MapEditor.ToolKey(MapTool.Select), Act.SelectTool,
                                  () => Editor.Tool == MapTool.Select, "Select"));
            bar.AddChild(Terrain());
            bar.AddChild(Spawns());
            bar.AddChild(Pressing(MapBuilderView.NpcsKey, Act.NpcsPicker, () => OpenName == NpcsPicker, "Npcs"));
            bar.AddChild(Pressing(MapBuilderView.BuildingsKey, Act.BuildingsPicker, () => OpenName == BuildingsPicker, "Buildings"));
            bar.AddChild(Pressing(MapEditor.ToolKey(MapTool.Wall), Act.WallsAndDoors,
                                  () => Editor.Tool == MapTool.Wall || OpenName == WallsPanel, "WallsAndDoors"));
            bar.AddChild(Pressing(MapEditor.ToolKey(MapTool.Prop), Act.PropsPicker, () => OpenName == PropsPicker, "Props"));
            bar.AddChild(new VSeparator());

            Button leave = Ui.Button(MapBuilderView.LeaveKey, () => Builder.Do(Act.LeaveBuilder));
            leave.Name = "Leave";
            leave.ThemeTypeVariation = "HudButton";
            bar.AddChild(leave);

            _context = Ui.Row(layout.Gap);
            _context.Name = "Context";
            _context.Alignment = BoxContainer.AlignmentMode.Center;

            var bottom = new VBoxContainer
            {
                Name = "Bottom",
                AnchorTop = 1,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetBottom = -layout.EdgeMargin / 2,
                GrowVertical = GrowDirection.Begin,
                Alignment = BoxContainer.AlignmentMode.End,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            bottom.AddThemeConstantOverride("separation", layout.Gap / 2);

            // the picker or pop-up open above the bar goes in here (Show), then what's chosen, then the bar
            _above = new CenterContainer { Name = "Above", MouseFilter = MouseFilterEnum.Ignore };
            bottom.AddChild(_above);

            var context = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
            context.AddChild(_context);
            bottom.AddChild(context);

            var centre = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
            centre.AddChild(Ui.Panel(bar, "HudPanel"));
            bottom.AddChild(centre);

            return bottom;
        }

        // a bar button that stays pressed while `chosen` is true, and does its act
        Button Pressing(string key, Act act, Func<bool> chosen, string name)
        {
            Button button = Ui.Button(key, () => Builder.Do(act));
            button.Name = name;
            button.ThemeTypeVariation = "HudButton";
            button.ToggleMode = true;
            _updates.Add(() => button.SetPressedNoSignal(chosen()));
            return button;
        }

        // TERRAIN ▾: Paint and Fill, then the grounds and the rules they lay. A ground or a rule picked takes up the
        // terrain tool in hand (Paint, unless Fill is)
        MenuButton Terrain()
        {
            var items = new List<(string Words, Action Do)>
            {
                (Ui.Say(MapEditor.ToolKey(MapTool.Paint)), () => Builder.Do(Act.TerrainTool)),
                (Ui.Say(MapEditor.ToolKey(MapTool.Fill)), () => Builder.Do(Act.FillTool)),
                (null, null),
            };

            foreach (Ground ground in MapBuilderView.Looks)
            {
                Ground one = ground;
                items.Add((Ui.Say(MapBuilderView.GroundKey(one)), () => Lay(Brush.Of(one))));
            }

            items.Add((null, null));

            foreach (Core.Space.Tile rule in MapBuilderView.Rules)
            {
                Core.Space.Tile one = rule;
                items.Add((Ui.Say(MapBuilderView.RuleKey(one)), () => Lay(Brush.Of(one))));
            }

            return Dropping("Terrain", MapBuilderView.TerrainKey, items, () => Editor.Tool is MapTool.Paint or MapTool.Fill);
        }

        void Lay(Brush brush)
        {
            Editor.Brush = brush;
            if (Editor.Tool is not (MapTool.Paint or MapTool.Fill)) Builder.Do(Act.TerrainTool);
            else Refresh();
        }

        // SPAWN ▾: the hero's start, and the monster spawns 1 to 9
        MenuButton Spawns()
        {
            var items = new List<(string Words, Action Do)>
            {
                (Ui.Say(MapEditor.ToolKey(MapTool.Start)), () => Builder.Do(Act.StartTool)),
                (null, null),
            };

            foreach (int slot in MapBuilderView.SpawnSlots)
            {
                int one = slot;
                items.Add((Ui.Say(MapBuilderView.SpawnKey, one), () =>
                {
                    Editor.SpawnSlot = one;
                    Builder.Do(Act.SpawnTool);
                }));
            }

            return Dropping("Spawn", MapBuilderView.SpawnMenuKey, items, () => Editor.Tool is MapTool.Start or MapTool.Spawn);
        }

        // a bar button with a menu under it (the combat HUD's More actions ▾); a null item is a line between
        MenuButton Dropping(string name, string key, IReadOnlyList<(string Words, Action Do)> items, Func<bool> chosen)
        {
            var button = new MenuButton { Name = name, Text = Ui.Say(key) + " ▾", Flat = false, FocusMode = FocusModeEnum.All };
            button.ThemeTypeVariation = "HudButton";
            PopupMenu popup = button.GetPopup();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Words == null) popup.AddSeparator();
                else popup.AddItem(items[i].Words, i);
            }

            popup.IdPressed += id => items[(int)id].Do();
            _updates.Add(() => button.Modulate = chosen() ? Chosen : Colors.White);
            return button;
        }

        // a pressed look for a menu button, which has no pressed state of its own (the combat HUD's)
        static readonly Color Chosen = new Color(1f, 0.9f, 0.62f);

        // --- what's chosen, and what can be done with it ------------------------------------------------------------

        void Context()
        {
            Ui.Clear(_context);

            // the selection: Delete and Turn (cc_task_l's Select verbs, now the selection's own; Copy and Paste went in
            // cc_task_m3 Part 8, "they don't seem to do anything")
            if (Editor.Tool == MapTool.Select && Editor.Chosen != null)
            {
                _context.AddChild(Small(MapBuilderView.DeleteKey, Builder.Delete));

                if (Editor.Picked != null) Turn(Editor.Picked.Turn);
                if (Editor.Picked != null && Editor.Palette.IsFigure(Editor.Picked.Id)) Who(Editor.Picked);
                return;
            }

            // a prop or building in hand: its name, its turn, and what Ctrl and Shift do
            if (Editor.InHand)
            {
                _context.AddChild(Panelled(Ui.Plain(Ui.Say(Editor.Palette.Find(Editor.PropId)?.NameKey ?? Editor.PropId))));
                Turn(Editor.Turn);
                _context.AddChild(Panelled(Ui.Label(MapBuilderView.ScatterHintKey)));
                return;
            }

            // a terrain tool: what it lays
            if (Editor.Tool is MapTool.Paint or MapTool.Fill)
            {
                string lays = Editor.Brush.Look is Ground look ? MapBuilderView.GroundKey(look) : MapBuilderView.RuleKey(Editor.Brush.Rule ?? Core.Space.Tile.Floor);
                _context.AddChild(Panelled(Ui.Plain($"{Ui.Say(MapEditor.ToolKey(Editor.Tool))}: {Ui.Say(lays)}")));
            }

            if (Editor.Tool == MapTool.Spawn)
                _context.AddChild(Panelled(Ui.Plain(Ui.Say(MapBuilderView.SpawnKey, Editor.SpawnSlot))));
        }

        // WHO A PICKED FIGURE IS (cc_task_m3 Part 10): Someone, one of the campaign's characters, or New character...,
        // which asks for a name here and adds them to the campaign's Characters tab. My maps offers only Someone
        void Who(Prop figure)
        {
            var who = new OptionButton { Name = "Who", ThemeTypeVariation = "HudButton", FitToLongestItem = true };
            var people = Builder.View.People;

            who.AddItem(Ui.Say(MapBuilderView.SomeoneKey));
            foreach ((string _, string name) in people) who.AddItem(name);
            if (!Builder.View.Place.IsLibrary) who.AddItem(Ui.Say(MapBuilderView.NewPersonKey));

            int at = people.ToList().FindIndex(p => p.Id == figure.Actor);
            who.Selected = at < 0 ? 0 : at + 1;

            who.ItemSelected += i =>
            {
                if (i == 0) Builder.Who("");
                else if (i <= people.Count) Builder.Who(people[(int)i - 1].Id);
                else NewPerson();
            };

            _context.AddChild(Panelled(Ui.Plain(Ui.Say(MapBuilderView.WhoKey))));
            _context.AddChild(who);
        }

        // the name of someone new, typed here; Enter adds them to the campaign and makes the figure them
        void NewPerson()
        {
            var name = new LineEdit { Name = "NewPerson", PlaceholderText = Ui.Say(MapBuilderView.NewPersonNameKey), CustomMinimumSize = new Vector2(240, 0) };

            name.TextSubmitted += typed =>
            {
                if (Builder.View.AddPerson(typed) is { } id) Builder.Who(id);
                else Refresh();
            };

            _context.AddChild(name);
            Ui.FocusLater(name);
        }

        void Turn(int quarters)
        {
            _context.AddChild(Small(MapBuilderView.RotateKey, () => Builder.Rotate(1)));
            _context.AddChild(Panelled(Ui.Plain($"{quarters * 90}°")));
        }

        static Button Small(string key, Action pick)
        {
            Button button = Ui.Button(key, pick);
            button.ThemeTypeVariation = "HudButton";
            return button;
        }

        // a word or two on the bar's own dark panel: the HUD's light letters, on one line
        static Control Panelled(Label words)
        {
            words.ThemeTypeVariation = "HudLabel";
            words.AutowrapMode = TextServer.AutowrapMode.Off;
            return Ui.Panel(words, "HudPanel");
        }

        // --- the problems, folded to a button ----------------------------------------------------------------------

        void Problems()
        {
            int count = Builder.View.Problems.Count;
            _problemsButton.Text = count == 0 ? Ui.Say(MapBuilderView.ProblemsKey) : Ui.Say(MapBuilderView.ProblemsCountKey, count);

            if (OpenName == ProblemsPanel) Show(ProblemsPanel, ProblemsList());
        }
    }
}
