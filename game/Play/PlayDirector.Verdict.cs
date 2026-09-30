using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Combat;
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

            _verdictThisCast = true;
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

        // A SPELL SAYS WHAT IT DID (cc_task_ui-issues-9-30.md 1.2): "Mage Armor: your Armor Class is now
        // 14". What changed on the hero is said to the hero, on anyone else by name. A spell that
        // changed nothing the damage line doesn't already say gets "You cast Fire Bolt on Giant Rat",
        // unless its hit or save is already standing there - that verdict says more
        bool _verdictThisCast;

        void CastCaption(Actor caster, string spellKey, IReadOnlyList<Actor> at, IReadOnlyList<Change> changes)
        {
            Actor hero = Run?.Hero.Actor;

            if (hero == null) return;

            bool mine = ReferenceEquals(caster, hero);
            var said = changes.Where(c => mine || ReferenceEquals(c.Who, hero)).Select(Said).ToList();
            bool verdictStands = _verdictThisCast;
            _verdictThisCast = false;

            string text =
                said.Count > 0 ? Ui.Say(ScreenWords.CaptionChanged, Ui.Say(spellKey), string.Join("; ", said))
                : !mine || verdictStands ? ""
                : at.Count == 0 ? Ui.Say(ScreenWords.CaptionCast, Ui.Say(spellKey))
                : Ui.Say(ScreenWords.CaptionCastOn, Ui.Say(spellKey),
                         string.Join(", ", at.Select(a => LogText.NameOf(a, _combat.Battle, Run.Hero.Name))));

            if (text == "") return;

            _verdict = verdictStands && _verdict != "" ? _verdict + "\n" + text : text;
            _verdictUntil = Time.GetTicksMsec() + (ulong)(HudLayout.Current.VerdictSeconds * 1000);
            ShowPrompt();
        }

        string Said(Change change) =>
            ReferenceEquals(change.Who, Run.Hero.Actor)
                ? Ui.Say(ScreenWords.YouChanged(change.What), change.To, change.To - change.From)
                : Ui.Say(ScreenWords.TheyChanged(change.What), LogText.NameOf(change.Who, _combat.Battle, Run.Hero.Name),
                         change.To, change.To - change.From);

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
