using System;
using System.Linq;
using Content.Play;
using Content.Workshop.Story;
using Content.Workshop.Testing;
using Core.Combat;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // THE WORKSHOP BAR (cc_task_q Part 3): a slim bar along the top of the table, only in a test. Back to the editor (the
    // card that was playing open, its block highlighted), Restart this card, Restart the test; the hero's rolls (for real,
    // always pass, always fail, and the next one only); where the story is (and the cards visited); what the story
    // remembers, live, with Edit; and what went wrong so far
    public partial class WorkshopBar : PanelContainer
    {
        readonly Func<CampaignRun> _run;
        readonly Action<string> _restart;

        Label _where;
        Label _visited;
        VBoxContainer _memory;
        VBoxContainer _problems;
        bool _editing;

        public WorkshopBar(Func<CampaignRun> run, Action<string> restart)
        {
            _run = run;
            _restart = restart;
            Name = "WorkshopBar";
        }

        WorkshopTest Test => GameState.Testing;

        StoryDraft Story => Test?.Editor.Draft.Story;

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            OffsetBottom = Ui.Px(48);
            MouseFilter = MouseFilterEnum.Stop;

            Button back = Ui.Button(TestWords.BackToEditorKey, BackToEditor);
            back.Name = "BackToEditor";

            Button card = Ui.Button(TestWords.RestartCardKey, () => _restart(Test.Card));
            card.Name = "RestartCard";

            Button test = Ui.Button(TestWords.RestartTestKey, () => _restart(Test.Setup.Start));
            test.Name = "RestartTest";

            OptionButton rolls = Forms.Choice("Rolls", Enum.GetValues<RollRule>(), Test.Rolls, TestWords.Rolls, r => Test.Rolls = r);
            rolls.CustomMinimumSize = new Vector2(Ui.Px(170), 0);

            Button pass = Ui.Button(TestWords.NextPassKey, () => Test.Next = RollRule.Pass);
            pass.Name = "NextPass";
            Button fail = Ui.Button(TestWords.NextFailKey, () => Test.Next = RollRule.Fail);
            fail.Name = "NextFail";

            _where = Ui.Label(TestWords.WhereKey, "");
            _where.Name = "WhereAmI";
            _where.MouseFilter = MouseFilterEnum.Stop;
            _where.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) _visited.Visible = !_visited.Visible;
            };

            var memory = new CheckButton { Name = "MemoryFold", Text = Ui.Say(TestWords.MemoryKey) };
            memory.Toggled += on => { _memory.Visible = on; Refresh(); };

            var top = Ui.Row(8, Ui.Label(TestWords.TestingKey), back, card, test, Ui.Label(TestWords.RollsKey), rolls, pass, fail, _where, memory);

            _visited = Forms.Note("");
            _visited.Name = "Visited";
            _visited.Visible = false;

            _memory = Ui.Column(4);
            _memory.Name = "LiveMemory";
            _memory.Visible = false;

            _problems = Ui.Column(2);
            _problems.Name = "ProblemsSeen";

            AddChild(Ui.Column(4, top, _visited, _memory, _problems));
            Refresh();
        }

        // drawn again as the story moves (PlayDirector.Refresh)
        public void Refresh()
        {
            CampaignRun run = _run();
            if (run == null || Test == null || !IsInsideTree()) return;

            Test.Saw(run, key => Ui.Say(key) != key);

            _where.Text = Ui.Say(TestWords.WhereKey, Story.NameOf(Test.Card));
            _visited.Text = Ui.Say(TestWords.VisitedKey) + ": " + string.Join(" > ", Test.Visited.Select(Story.NameOf));

            Ui.Clear(_problems);
            foreach (TestProblem problem in Test.Problems)
            {
                Label said = Ui.Label(TestWords.Problem(problem.Kind), problem.What);
                said.AddThemeColorOverride("font_color", Forms.Wrong);
                _problems.AddChild(said);
            }

            if (!_memory.Visible) return;

            Ui.Clear(_memory);

            var edit = new CheckButton { Name = "EditMemory", Text = Ui.Say(TestWords.EditKey), ButtonPressed = _editing };
            edit.Toggled += on => { _editing = on; Refresh(); };
            _memory.AddChild(edit);

            foreach (Memory.Remembered one in Memory.List(Story))
            {
                string variable = one.Variable;
                object now = run.Recall(variable);
                string phrase = Memory.Phrase(variable, Story.Phrases);

                Control value = one.Count
                    ? Forms.Number("Live_" + variable.TrimStart('$'), -99, 99, now is float n ? (int)n : 0, v => run.Remember(variable, (float)v))
                    : Forms.Tick("Live_" + variable.TrimStart('$'), null, now is true, on => run.Remember(variable, on));

                if (value is Button tick) tick.Text = phrase;
                if (value is SpinBox spin) spin.Prefix = phrase;
                value.SetProcessInput(_editing);
                if (value is BaseButton b) b.Disabled = !_editing;
                if (value is SpinBox s) s.Editable = _editing;

                _memory.AddChild(value);
            }

            _memory.AddChild(Ui.Label(TestWords.HeroLineKey, run.Hero.Pack.Copper / 100, run.Hero.Level, run.Hero.Pack.Everything.Count()));
        }

        // BACK TO THE EDITOR: the campaign page, on the card that was playing, its block highlighted; no question asked
        void BackToEditor()
        {
            GD.Print($"test    back to the editor at {Test?.Card}");
            GameState.StopTesting();
            Scenes.Go(GetTree(), Scenes.Book);
        }
    }

    // A FIGHT STARTS, IN A TEST (cc_task_q 3.4): play it, or skip to how it ended - won (the loot still drops), lost, or run
    // from - and the story carries on as if it had been fought
    public partial class FightSkipCard : Overlay
    {
        readonly string _fight;
        readonly Action<FightSkip> _picked;

        public FightSkipCard(string fight, Action<FightSkip> picked)
        {
            _fight = fight;
            _picked = picked;
            Name = "FightSkip";
        }

        protected override bool CanCancel => false;

        protected override void Draw()
        {
            Body.AddChild(Ui.Title(TestWords.FightStartsKey, _fight));

            foreach (FightSkip skip in Enum.GetValues<FightSkip>())
            {
                FightSkip one = skip;
                Button button = Ui.Button(TestWords.Skip(one), () => { Close(); _picked(one); });
                button.Name = "Skip_" + one;
                Body.AddChild(button);
            }
        }

        public static Outcome OutcomeOf(FightSkip skip) => skip switch
        {
            FightSkip.Win => Outcome.HeroesWon,
            FightSkip.Lose => Outcome.HeroesLost,
            _ => Outcome.Fled,
        };
    }
}
