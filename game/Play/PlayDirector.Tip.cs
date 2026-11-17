using Game.Access;
using Godot;

namespace Game.Play
{
    // <<tip section>> (cc_task_nt Part 4): the story's line stays where it was, the F1 help card opens at that section,
    // and closing it (F1 or Esc, as ever) lets the story go on. A run that reads on by itself closes it at once
    public partial class PlayDirector
    {
        bool _tipOpen;

        void OpenTip()
        {
            AccessDesk desk = AccessDesk.Instance;

            if (desk == null || _autoStory || _auto)
            {
                GD.Print($"play    tip {Run.Tip}, closed at once");
                Do(() => Run.TipClosed());
                return;
            }

            if (_tipOpen) return;

            _tipOpen = true;
            desk.HelpClosed += TipClosed;
            desk.Help(true, Run.Tip);
        }

        void TipClosed()
        {
            AccessDesk.Instance.HelpClosed -= TipClosed;
            _tipOpen = false;
            Do(() => Run.TipClosed());
        }
    }
}
