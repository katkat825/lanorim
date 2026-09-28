using System;
using Content.Play;
using Content.Screens;
using Game.Screens;

namespace Game.Play
{
    public partial class PlayDirector
    {
        // --- what the run says now ------------------------------------------------------------------

        void Refresh()
        {
            if (_rules.Busy || _overlay != null) return;

            // a level the story just gave: the level-up card first, whatever comes next
            if (Run.Hero.Level > _level || Run.Hero.PendingImprovements > 0)
            {
                var view = new LevelUpView(Run.Hero, _level, _maxHp);

                if (_auto)
                {
                    while (Run.Hero.PendingImprovements > 0) view.TakeSuggested();
                }
                else
                {
                    Open(new LevelUpScreen(view), () => { Mark(); Refresh(); });
                    return;
                }

                Mark();
            }

            switch (Run.Now)
            {
                case Scene.Line:
                case Scene.Choice:
                    _dialogue.Show(new DialogueView(Run, GameState.Companion));

                    if (_autoStory)
                    {
                        if (Run.Now == Scene.Line) _dialogue.Continue();
                        else _dialogue.Choose(0);
                    }

                    break;

                case Scene.Fight:
                    _dialogue.Visible = false;
                    if (!_fighting) StartFight();
                    break;

                case Scene.Shop:
                    _dialogue.Visible = false;
                    OpenShop();
                    break;

                case Scene.Dead:
                    _dialogue.Visible = false;
                    Died();
                    break;

                case Scene.Over:
                    _dialogue.Visible = false;
                    TheEnd();
                    break;
            }
        }

        void Mark()
        {
            _level = Run.Hero.Level;
            _maxHp = Run.Hero.Actor.Health.Maximum;
        }

        void Open(Overlay overlay, Action closed = null)
        {
            _overlay = overlay;
            overlay.Closed += () =>
            {
                _overlay = null;
                closed?.Invoke();
            };
            _ui.AddChild(overlay);
        }
    }
}
