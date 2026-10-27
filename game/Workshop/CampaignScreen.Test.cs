using System;
using System.Linq;
using System.Threading.Tasks;
using Content.Campaigns;
using Content.Saves;
using Content.Sheet;
using Content.Workshop;
using Content.Workshop.Story;
using Content.Workshop.Testing;
using Game.Play;
using Game.Screens;
using Godot;

namespace Game.Workshop
{
    // PLAY-TESTING FROM THE PAGE (cc_task_q Parts 1, 2 and 5): Play the campaign (as a player would, or with the quick test
    // hero), Play from here on a card, Try this fight on a fight - each through the test hero panel - and Run it for me.
    // The test plays the campaign as the page has it, unsaved changes and all, from a folder of its own
    public partial class CampaignScreen
    {
        // the last Run it for me's report, shown until the page is drawn for another
        RunForMe.Report _report;

        // THE BAR ABOVE THE TABS: Play the campaign, the quick hero tick, Run it for me and Try every choice
        HBoxContainer TestTools()
        {
            TestSetup setup = TestSetup.For(Editor.Folder);

            var quick = Forms.Tick("QuickHero", TestWords.QuickHeroKey, !setup.AsAPlayer, on =>
            {
                TestSetup now = TestSetup.For(Editor.Folder);
                now.AsAPlayer = !on;
                now.Keep(Editor.Folder);
            });

            Button play = Ui.Button(TestWords.PlayCampaignKey, () => AskForHero(TestSetup.For(Editor.Folder).Copy(start: ""), ""));
            play.Name = "PlayCampaign";

            Button run = Ui.Button(TestWords.RunForMeKey, () => RunIt(everyChoice: false));
            run.Name = "RunForMe";

            Button every = Ui.Button(TestWords.TryEveryKey, () => RunIt(everyChoice: true));
            every.Name = "TryEvery";

            return Ui.Row(8, play, quick, run, every);
        }

        public void PlayFromHere(string card, int[] block) => AskForHero(TestSetup.For(Editor.Folder).Copy(start: card), "");

        // TRY THIS FIGHT: a hero at the fight's level by default
        public void TryFight(string fight)
        {
            TestSetup setup = TestSetup.For(Editor.Folder).Copy(start: "");
            setup.Level = Math.Max(1, Draft.Tables.Fight(fight).Fight?.Level ?? 1);
            setup.AsAPlayer = false;
            AskForHero(setup, fight);
        }

        // THE TEST HERO PANEL, then the test
        void AskForHero(TestSetup setup, string fight)
        {
            KeepDraft();

            var panel = new TestPanel(Editor, setup, Things);
            panel.Started += () => Begin(panel.Setup, fight);
            panel.Cancelled += () => panel.QueueFree();
            AddChild(panel);
        }

        public TestPanel Asking => Nodes.Under<TestPanel>(this).FirstOrDefault();

        // THE TEST STARTED: the campaign written as it is now into the test's own folder, read back, and played - with the
        // quick hero, or (Play the campaign, as a player) through creation and the starting shop
        void Begin(TestSetup setup, string fight)
        {
            setup.Keep(Editor.Folder);

            var test = new WorkshopTest(Editor, setup) { Fight = fight ?? "" };
            string folder = test.Write(ProjectSettings.GlobalizePath(GameState.TestFolder), out string problem);

            if (folder == null)
            {
                GD.PushWarning($"workshop: the test could not be written - {problem}");
                return;
            }

            Package pack = Package.Read(folder);

            if (pack.Manifest == null)
            {
                GD.PushWarning("workshop: the test's campaign wouldn't load - " + string.Join("; ", pack.Problems.Where(p => !p.IsACaution)));
                return;
            }

            GD.Print($"test    {Editor.Id}: {setup}{(fight?.Length > 0 ? $", fight {fight}" : "")}");

            if (setup.AsAPlayer && setup.Start.Length == 0)
            {
                var creation = new CreationScreen(GameState.Content);
                creation.Cancelled += () => Show(_launch, Editor, WorkshopTab.Story, fresh: false);
                creation.Finished += hero => Play(test, pack, hero);
                _launch.Show(Ui.Panel(creation), 900);
                return;
            }

            Play(test, pack, setup.Hero(GameState.Content.With(pack), Ui.Say(GameState.Content.Class(setup.Class)?.NameKey ?? setup.Class)));
        }

        void Play(WorkshopTest test, Package pack, Hero hero)
        {
            GameState.Test(test, pack, hero);
            Scenes.Go(GetTree(), Scenes.Table);
        }

        // --- run it for me -------------------------------------------------------------------------------------------

        // RUN IT FOR ME: played by itself from the campaign's start (or the card open), and the report; Try every choice
        // plays again with each other answer at the choices it met, up to RunForMe.MostRuns
        async void RunIt(bool everyChoice)
        {
            KeepDraft();

            TestSetup setup = TestSetup.For(Editor.Folder).Copy(start: _openStory);
            var test = new WorkshopTest(Editor, setup);
            string folder = test.Write(ProjectSettings.GlobalizePath(GameState.TestFolder), out string problem);

            if (folder == null)
            {
                GD.PushWarning($"workshop: the run could not be written - {problem}");
                return;
            }

            Package pack = Package.Read(folder);
            if (pack.Manifest == null) return;

            var library = GameState.Content.With(pack);
            var saves = new SaveLibrary(ProjectSettings.GlobalizePath(GameState.TestFolder + "/run_saves"));
            StoryDraft story = Draft.Story;
            string start = setup.Start.Length > 0 ? setup.Start : null;
            Func<string, bool> hasWords = key => Ui.Say(key) != key;

            GD.Print($"test    run it for me: {Editor.Id}{(everyChoice ? ", every choice" : "")}");

            _report = await Task.Run(() => RunForMe.All(library, pack, () => setup.Hero(library, "Probe"), start, story, everyChoice, saves, hasWords));

            GD.Print($"test    ran {_report.Runs}: reached {string.Join(", ", _report.Reached)}; never {string.Join(", ", _report.NeverReached.Select(n => n.Card))}");

            if (IsInsideTree()) Redraw();
        }

        // THE REPORT: the cards it reached, the ones it never did and what's in the way, and what went wrong - each with
        // Go there
        Control Report()
        {
            if (_report == null) return null;

            var column = Ui.Column(4, Ui.Title(TestWords.ReportKey), Forms.Hint(TestWords.RunsKey, _report.Runs));
            column.Name = "RunReport";

            column.AddChild(Ui.Label(TestWords.ReachedKey, _report.Reached.Count));
            column.AddChild(Forms.Note(string.Join(", ", _report.Reached.Select(Draft.Story.NameOf))));

            column.AddChild(Ui.Label(TestWords.NeverReachedKey, _report.NeverReached.Count));

            foreach ((string card, var behind) in _report.NeverReached)
            {
                string c = card;
                var row = Ui.Row(8, Ui.Plain(Draft.Story.NameOf(card)),
                                 Forms.Note(behind.Count > 0 ? Ui.Say(TestWords.BehindKey, string.Join("; ", behind.Select(GateSaid))) : Ui.Say(StoryWords.NothingLeadsHereKey)),
                                 Ui.Button(WorkshopWords.GoThereKey, () => OpenStory(c)));
                row.Name = "Never_" + card;
                column.AddChild(row);
            }

            foreach (TestProblem seen in _report.Problems)
                column.AddChild(Ui.Row(8, Ui.Label(TestWords.Problem(seen.Kind), seen.What),
                                       Ui.Button(WorkshopWords.GoThereKey, () => OpenStory(seen.Card))));

            return Ui.Panel(column);
        }

        // "Arrival, option C, only if the hero is a Mage or the hero can cast spells"
        string GateSaid(RunForMe.Gate gate) =>
            string.Join(", ", new[] { Draft.Story.NameOf(gate.From), gate.Option.Length > 0 ? Ui.Say(StoryWords.OptionKey) + " " + gate.Option : null }
                                  .Concat(gate.Conditions.Select(c => c.Length == 0 ? Ui.Say(BlockWords.OtherwiseKey).ToLowerInvariant() : Ui.Say(BlockWords.OnlyIfSaidKey, Sentence(c))))
                                  .Where(p => p != null));

        // a condition in the picker's sentences, joined as it's joined; one with no sentence, as written
        string Sentence(string yarn)
        {
            OnlyIf condition = OnlyIf.Of(yarn);
            return string.Join(" " + Ui.Say(condition.Any ? BlockWords.OrKey : BlockWords.AndKey) + " ",
                               condition.Clauses.Select(c => c.Term == Term.Raw ? c.Name : Ui.Say(BlockWords.Clause(c.Term), ClauseArgs(c))));
        }

        // what the last test at the table saw go wrong, each with Go there
        Control Seen()
        {
            if (GameState.LastTest is not { } test || test.Editor != Editor || test.Problems.Count == 0) return null;

            var column = Ui.Column(4, Ui.Title(TestWords.ProblemsKey));
            column.Name = "ProblemsSeen";

            foreach (TestProblem seen in test.Problems)
                column.AddChild(Ui.Row(8, Ui.Label(TestWords.Problem(seen.Kind), seen.What),
                                       Ui.Button(WorkshopWords.GoThereKey, () => OpenStory(seen.Card))));

            return Ui.Panel(column);
        }
    }
}
