using Content.Combat;
using Core.Characters;
using Core.Resolution;
using Game.Screens;
using Godot;

namespace Game.Play
{
    public partial class PlayDirector
    {
        // --- what the hero's roll came to, under the tray's prompt -------------------------------------
        //
        // The log said "rolls 1d20+2: 4, for 6" and nothing else, so a miss read as "I attacked and never
        // got to roll damage" (cc_task_table-ui-minis-zoom-damage.md). Now the verdict stands under the
        // turn hint - Hit, Miss, Critical, a save made or failed, with the armor class or DC - and while
        // the damage dice wait on the tray it heads the "Space to throw" prompt.

        FightLog _log;
        string _verdict = "";
        ulong _verdictUntil;
        bool _waiting;

        // main thread; the rules thread posts it the moment the roll is settled
        void Verdict(Actor by, Actor target, Attempt attempt)
        {
            Actor hero = Run?.Hero.Actor;

            if (hero == null || attempt == null) return;
            if (!ReferenceEquals(by, hero) && !ReferenceEquals(target, hero)) return;

            string text = VerdictText(target, attempt, ReferenceEquals(by, hero));

            if (text == "") return;

            _verdict = text;
            _verdictUntil = Time.GetTicksMsec() + (ulong)(HudLayout.Current.VerdictSeconds * 1000);
            ShowPrompt();
        }

        string VerdictText(Actor target, Attempt attempt, bool herosOwn)
        {
            if (attempt.Kind == RollKind.Save)
                return Ui.Say(attempt.Succeeded ? ScreenWords.VerdictSaved : ScreenWords.VerdictFailed,
                              LogText.NameOf(target, _combat.Battle, Run.Hero.Name), attempt.Total, attempt.Against);

            // a foe's swing at the hero is the log's; the caption is for the hero's own rolls
            if (attempt.Kind != RollKind.Attack || attempt.Diverted || !herosOwn) return "";

            string key = !attempt.Succeeded ? ScreenWords.VerdictMiss
                : attempt.IsCritical ? ScreenWords.VerdictCritical
                : ScreenWords.VerdictHit;

            return Ui.Say(key, attempt.Total, attempt.Against);
        }

        void Prompt(bool waiting)
        {
            _waiting = waiting;
            ShowPrompt();
        }

        void ShowPrompt()
        {
            string throwing = Ui.Say(ScreenWords.ThrowPrompt);

            _prompt.Text = _verdict == "" ? throwing : _waiting ? _verdict + "\n" + throwing : _verdict;
            _prompt.Visible = !_auto && (_waiting || _verdict != "");
        }

        // a verdict stays while the dice it leads to are still on the tray, then its few seconds
        void AgeVerdict()
        {
            if (_verdict == "" || _waiting || Time.GetTicksMsec() < _verdictUntil) return;

            _verdict = "";
            ShowPrompt();
        }
    }
}
