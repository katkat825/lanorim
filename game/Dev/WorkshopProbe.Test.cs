using System;
using System.Linq;
using Content.Campaigns;
using Content.Creation;
using Content.Workshop;
using Content.Workshop.Story;
using Content.Workshop.Testing;
using Game.Play;
using Game.Screens;
using Game.Workshop;
using Godot;

namespace Game.Dev
{
    // PLAY-TESTING, DRIVEN (cc_task_q, check-workshop extended): on the probe's story -
    //   1. Play from here on the gate, the test hero panel's preset "picked the lock" set false, rolls Always fail: the
    //      check fails, the story goes round to the ending's "otherwise", and the test's end lands back on the ending's
    //      card with that line's block highlighted;
    //   2. again, rolls Always pass: the fight's skip choice, Win it - $fight is "won" and the loot drops - and the
    //      ending's other line;
    //   3. Run it for me from the page, and on the sample (every one of its cards reached)
    public partial class WorkshopProbe
    {
        // 1 and 2: the panel, the preset, Start, and the rule over the rolls
        void StartTest(RollRule rolls, Stage next)
        {
            Page.PlayFromHere(Gate, null);

            TestPanel panel = Page.Asking;
            Expect(panel != null, "Play from here asks for the test hero");
            if (panel == null) return;

            CheckBox preset = Find<CheckBox>("Preset_picked_the_lock");
            Expect(preset != null, "with a preset for what the story remembers");
            preset?.EmitSignal(BaseButton.SignalName.Toggled, true);
            preset?.EmitSignal(BaseButton.SignalName.Toggled, false);
            Expect(panel.Setup.Memory.TryGetValue(Picked, out string set) && set == "false", "preset: they didn't pick the lock");

            _stage = next;
            Press("StartTest");

            Expect(GameState.Testing != null && GameState.Testing.Setup.Start == Gate, $"the test starts at the gate ({GameState.Testing?.Setup})");
            if (GameState.Testing != null) GameState.Testing.Rolls = rolls;
        }

        void FailTest() => StartTest(RollRule.Fail, Stage.Failing);

        void PassTest() => StartTest(RollRule.Pass, Stage.Passing);

        // at the table: the bar is up; in the passing test, the fight's skip choice is answered Win it
        void WatchTest()
        {
            if (GetParent() is not PlayDirector director) return;

            if (!_said)
            {
                _said = true;
                Expect(Nodes.Under<WorkshopBar>(director).Any(), "the Workshop bar is along the top of the table");
                Expect(GameState.Run?.Recall(Picked) is false, $"the preset is set before the first line ({GameState.Run?.Recall(Picked)})");
            }

            // Try this fight: played, by the --auto hand
            if (_stage == Stage.Trying && Nodes.Under<FightSkipCard>(director).FirstOrDefault() is { } play)
            {
                Expect(GameState.Run?.Fight?.Entry?.Id == Fight, $"Try this fight starts {Fight} ({GameState.Run?.Fight?.Entry?.Id})");
                Nodes.Under<Button>(play).FirstOrDefault(b => b.Name == "Skip_Play")?.EmitSignal(BaseButton.SignalName.Pressed);
            }

            if (_stage != Stage.Passing) return;

            if (Nodes.Under<FightSkipCard>(director).FirstOrDefault() is { } skip && !_skipped)
            {
                _skipped = true;
                _before = (GameState.Run.Hero.Pack.Copper, GameState.Run.Hero.Pack.CountOf("torch"));
                _tested = GameState.Run;
                Expect(true, "a fight starts, and the test asks: play it, win it, lose it, or run away");
                Nodes.Under<Button>(skip).FirstOrDefault(b => b.Name == "Skip_Win")?.EmitSignal(BaseButton.SignalName.Pressed);
            }

            if (_skipped && !_won && GameState.Run?.Recall("$fight") is "won")
            {
                _won = true;
                Expect(true, "Win it: $fight is \"won\" (the loot is looked at once the test is over)");
            }
        }

        static bool _skipped, _won;
        static (int Copper, int Torches) _before;
        static Content.Play.CampaignRun _tested;

        // back on the page: the card that was playing, its block highlighted
        void BackFromFail()
        {
            // the launch screen's own first screen: back from the table, the page that sent it there
            Launch.ShowFirst("title", null);

            Expect(Page != null && Page.OpenStoryCard == Ending, $"the test's end came back to the editor, on the ending ({Page?.OpenStoryCard})");
            Expect(Has<PanelContainer>(p => p.HasMeta("highlighted")), "with the line that was playing highlighted");
            Expect(GameState.LastTest?.Visited.Contains(Gate) == true && GameState.LastTest.Visited.Contains(Ending), $"visited the gate and the ending ({string.Join(" > ", GameState.LastTest?.Visited ?? new())})");
            Expect(!GameState.LastTest.Visited.Contains(Meadow), "and never the meadow");
            Expect(LineOf(GameState.LastTest.LastLine)?.Contains("long way") == true, "the check failed: the ending said its otherwise");
            Expect(GameState.LastTest.Problems.Count == 0, "nothing went wrong: " + string.Join("; ", GameState.LastTest.Problems));
        }

        void BackFromPass()
        {
            Launch.ShowFirst("title", null);

            // the run the fight was skipped in, looked at after it ended (--auto plays on faster than the probe looks)
            Expect(_skipped && _tested?.Recall("$fight") is "won", $"the fight was skipped as a win ({_tested?.Recall("$fight")})");
            Expect(_tested != null && _tested.Hero.Pack.Copper > _before.Copper && _tested.Hero.Pack.CountOf("torch") > _before.Torches,
                   $"and the loot dropped, as playing would ({_before.Copper} -> {_tested?.Hero.Pack.Copper} copper, a torch)");
            Expect(Page != null && Page.OpenStoryCard == Ending, $"back on the ending ({Page?.OpenStoryCard})");
            Expect(LineOf(GameState.LastTest?.LastLine)?.Contains("stands open") == true, "the check passed: they picked the lock, and the ending knew it");
        }

        // TRY THIS FIGHT (cc_task_n Part 1): from the fight's card, a hero at its level, played at the table, and back to
        // the fight's card when it's over
        void TryTheFight()
        {
            Page.GoThere(WorkshopTab.Fights);
            Page.OpenCard("fight:" + Fight);
            Press("TryFight_" + Fight);
            Expect(Page.Asking?.Setup.Level == 1, $"a hero at the fight's level ({Page.Asking?.Setup.Level})");

            _stage = Stage.Trying;
            Press("StartTest");
            Expect(GameState.Testing?.Fight == Fight, "Try this fight starts the test");
        }

        void BackFromFight()
        {
            Launch.ShowFirst("title", null);

            Expect(Page?.Tab == WorkshopTab.Fights && Has<VBoxContainer>(v => v.Name == "Form_" + Fight),
                   $"the fight over, back on its card ({Page?.Tab})");
            Expect(GameState.LastTest?.Visited.Contains(WorkshopTest.FightCard) == true, "having played it");
        }

        string LineOf(string id) =>
            Content.Workshop.Story.Blocks.All(Story.Cards.Where(c => !c.IsText).SelectMany(c => c.Blocks)).OfType<Say>().FirstOrDefault(s => s.LineId == id)?.Text;

        // 3. Run it for me: from the page (the report on the Story tab), and on the sample
        void RunItForMe()
        {
            Page.OpenStory("");
            Press("TryEvery");
        }

        void RunReported()
        {
            Expect(Find<PanelContainer>("RunReport") != null || Nodes.Under<VBoxContainer>(Here).Any(v => v.Name == "RunReport"), "Run it for me reports on the page");

            Package sample = GameState.Package("sample_millbrook");
            var library = GameState.Content.With(sample);
            RunForMe.Report report = RunForMe.All(library, sample, () => QuickHero.Make(library, "fighter", 3, "Probe"), null,
                                                  StoryFiles.Open(sample.Folder), false,
                                                  new Content.Saves.SaveLibrary(ProjectSettings.GlobalizePath(Root + "/sample_saves")));

            GD.Print($"workshop run it for me on sample_millbrook: reached {string.Join(", ", report.Reached)}");
            Expect(report.NeverReached.Count == 0 && report.Reached.Count == 5, "Run it for me on the sample reaches every card");
        }
    }
}
