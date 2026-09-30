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

            Tell();

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

        // WHAT A NATURAL 1 OR 20 BOUGHT, said on the table: the consequence's own line, under the
        // turn hint, for a few seconds. each is said once, however often the screen redraws
        readonly System.Collections.Generic.HashSet<Content.Dialogue.Settled> _told = new();

        void Tell()
        {
            var lines = new System.Collections.Generic.List<string>();

            foreach (Content.Dialogue.Settled settled in Run.Settled)
                if (settled.Visit != null && _told.Add(settled))
                {
                    lines.Add(Ui.Say(settled.Visit.LineKey));

                    // it put the hero down away from a fight: the long rest that followed
                    if (settled.Rested) lines.Add(Ui.Say(ScreenWords.CameRound));
                }

            if (lines.Count == 0) return;

            _notice.Text = string.Join("\n", lines);
            _notice.Visible = true;

            ulong shown = ++_notices;
            GetTree().CreateTimer(NoticeSeconds).Timeout += () =>
            {
                if (shown == _notices && IsInstanceValid(_notice)) _notice.Visible = false;
            };
        }

        const double NoticeSeconds = 6.0;

        ulong _notices;

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
