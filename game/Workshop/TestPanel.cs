using System;
using System.Linq;
using Content.Schema;
using Content.Workshop;
using Content.Workshop.Story;
using Content.Workshop.Testing;
using Core.Localization;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE TEST HERO PANEL (cc_task_q Part 2), before a test starts, remembered per campaign: the class (all seven), the
    // species, the level, the class's own gear (always), every spell at that level if wanted; extra items and gold; and
    // what the story remembers, each phrase a tick box or a number, so a test can start at chapter 3 "as if Tavin was
    // brave and Korr trusts you 2". Made the way --begin makes its hero (TestSetup.Hero)
    public partial class TestPanel : CanvasLayer
    {
        readonly CampaignEditor _editor;
        readonly Library _library;

        public TestPanel(CampaignEditor editor, TestSetup setup, Library library)
        {
            _editor = editor;
            Setup = setup;
            _library = library;
            Name = "TestPanel";
            Layer = 40;
        }

        public TestSetup Setup { get; }

        public event Action Started;

        public event Action Cancelled;

        public override void _Ready()
        {
            var shade = new ColorRect { Color = new Color(0, 0, 0, 0.4f), MouseFilter = Control.MouseFilterEnum.Stop };
            shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(shade);

            var page = Ui.Column(10, Ui.Title(TestWords.HeroTitleKey));
            page.Name = "Page";

            var classes = _library.Classes.Select(c => c.Id).ToList();
            var species = _library.Playable.Select(s => s.Id).ToList();

            var who = Ui.Row(12,
                Forms.Field(TestWords.ClassKey, Forms.Choice("TestClass", classes, classes.Contains(Setup.Class) ? Setup.Class : classes[0],
                                                             c => KeyConventions.Key("class", c, "name"), c => Setup.Class = c), null),
                Forms.Field(TestWords.SpeciesKey, Forms.Choice("TestSpecies", species, species.Contains(Setup.Species) ? Setup.Species : species[0],
                                                               s => KeyConventions.Key("species", s, "name"), s => Setup.Species = s), null),
                Forms.Field(TestWords.LevelKey, Forms.Number("TestLevel", 1, 20, Setup.Level, v => Setup.Level = v), null));
            foreach (Control field in who.GetChildren().OfType<Control>()) field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            page.AddChild(who);

            page.AddChild(Forms.Hint(TestWords.GearKey));
            page.AddChild(Forms.Tick("EverySpell", TestWords.EverySpellKey, Setup.EverySpell, on => Setup.EverySpell = on));

            // extras: items and gold
            Label carried = Forms.Note(Carried());
            page.AddChild(Forms.Field(TestWords.ExtrasKey, Pickers.Item("TestItem", _library.Items, item =>
            {
                Setup.Items[item] = Setup.Items.TryGetValue(item, out int n) ? n + 1 : 1;
                carried.Text = Carried();
            }), null, carried));
            page.AddChild(Forms.Field(TestWords.GoldKey, Forms.Number("TestGold", 0, 100000, Setup.Gold, v => Setup.Gold = v), null));

            // what the story remembers, preset
            var memory = Ui.Column(4);
            memory.Name = "Presets";

            foreach (Memory.Remembered one in Memory.List(_editor.Draft.Story))
            {
                string variable = one.Variable;
                string phrase = Memory.Phrase(variable, _editor.Draft.Story.Phrases);
                Setup.Memory.TryGetValue(variable, out string now);

                if (one.Count)
                {
                    int.TryParse(now, out int count);
                    memory.AddChild(Ui.Row(8, Ui.Plain(phrase), Forms.Number("Preset_" + variable.TrimStart('$'), -99, 99, count,
                        v => Setup.Memory[variable] = v.ToString())));
                }
                else
                {
                    CheckBox tick = Forms.Tick("Preset_" + variable.TrimStart('$'), null, now == "true", on => Setup.Memory[variable] = on ? "true" : "false");
                    tick.Text = phrase;
                    memory.AddChild(tick);
                }
            }

            if (memory.GetChildCount() > 0) page.AddChild(Forms.Field(TestWords.PresetKey, memory, TestWords.PresetHintKey));

            Button start = Ui.Button(TestWords.StartKey, () =>
            {
                QueueFree();
                Started?.Invoke();
            });
            start.Name = "StartTest";

            Button cancel = Ui.Button(WorkshopWords.CancelKey, () => Cancelled?.Invoke());
            cancel.Name = "CancelTest";

            page.AddChild(Ui.Row(8, start, cancel));

            var centre = new CenterContainer();
            centre.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            PanelContainer panel = Ui.Panel(Ui.Scroll(page, 560));
            panel.CustomMinimumSize = new Vector2(Ui.Px(720), 0);
            centre.AddChild(panel);
            AddChild(centre);

            Ui.FocusLater(start);
        }

        string Carried() => string.Join(", ", Setup.Items.Select(i => $"{i.Value} {Pickers.ItemName(i.Key)}"));
    }
}
