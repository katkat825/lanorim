using Content.Companions;
using Content.Screens;
using Game.Table;

namespace Game.Play
{
    public partial class PlayDirector
    {
        // --- the companion on the table (cc_task_ui-issues-9-30.md 6.4) -------------------------------
        //
        // CompanionMind watches the fight (it is one of the fight's observers) and feels; the figure on
        // the table plays the feeling. The mind hears on the rules thread, so what it feels is posted
        // to the main thread. It says nothing yet: no campaign ships a bark bank, and a mind with none
        // keeps its silence (CompanionMind: "silence is a real answer").

        CompanionMind _mind;

        void WakeCompanion()
        {
            CompanionFigure figure = Table.Companion;

            figure?.Wear(GameState.Companion, Table.Board?.Paint);

            _mind = new CompanionMind(null, Run.Hero.Actor);
            _mind.Felt += mood => MainQueue.Post(() => figure?.Feel(mood));
        }

        // a held reaction runs out on the main thread's clock
        void TickCompanion(double delta) => _mind?.Tick(delta);

        // the story's "companion" speaker is whichever companion the hero has (DialogueView.Speaker)
        void CompanionSays(DialogueView view)
        {
            if (view.Speaker == GameState.Companion && GameState.Companion != "") Table.Companion?.Speak();
        }
    }
}
