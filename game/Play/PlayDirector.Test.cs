using Content.Workshop.Testing;
using Game.Workshop;
using Godot;

namespace Game.Play
{
    // A WORKSHOP TEST AT THE TABLE (cc_task_q Part 3): the Workshop bar along the top, a fight's skip choice, restarting,
    // and the way back to the editor when the story or a tried fight ends. Nothing here runs in play
    public partial class PlayDirector
    {
        WorkshopBar _bar;
        bool _skipAsked;

        // where a test starts: the card it was started from, else the campaign's own start
        static string TestStart => GameState.Testing?.Setup.Start is { Length: > 0 } start ? start : null;

        void MountWorkshopBar()
        {
            if (GameState.Testing == null) return;

            _bar = new WorkshopBar(() => Run, RestartAt);
            _ui.AddChild(_bar);
        }

        // A FIGHT, IN A TEST: play it, or skip to how it ended. true when the question is open
        bool AskToSkip()
        {
            if (GameState.Testing == null || _skipAsked) return false;

            _skipAsked = true;

            string fight = Run.Fight?.Entry?.Id ?? "";
            string named = GameState.Testing.Editor.Draft.Tables.NameOf("fight", fight);

            Open(new FightSkipCard(named, skip =>
            {
                GD.Print($"test    fight {fight}: {skip}");

                if (skip == FightSkip.Play)
                {
                    Refresh();
                    return;
                }

                _rules.Post(() => Run.EndFight(FightSkipCard.OutcomeOf(skip)), AfterTestFight);
            }));

            return true;
        }

        // a fight over in a test: Try this fight goes back to the editor; anything else plays on
        void AfterTestFight()
        {
            _skipAsked = false;

            if (GameState.Testing?.Fight is { Length: > 0 })
            {
                BackToEditor();
                return;
            }

            LayChapterMap();
            Refresh();
        }

        // RESTART: the same test from a card (this one, or the test's own start), with a fresh hero
        void RestartAt(string card)
        {
            WorkshopTest was = GameState.Testing;
            if (was == null) return;

            var again = new WorkshopTest(was.Editor, was.Setup.Copy(start: card)) { Rolls = was.Rolls, Fight = was.Fight };
            GD.Print($"test    restart at {card}");

            GameState.Test(again, Run.Pack, again.Setup.Hero(GameState.Content.With(Run.Pack), Run.Hero.Name));
            Scenes.Go(GetTree(), Scenes.Table);
        }

        void BackToEditor()
        {
            GameState.StopTesting();
            Scenes.Go(GetTree(), Scenes.Book);
        }
    }
}
