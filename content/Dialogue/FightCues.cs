using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Combat;
using Core.Resolution;
using Core.Words;

namespace Content.Dialogue
{
    // THE MOMENTS IN A FIGHT A STORY CAN SPEAK AT (the tutorials, 2026-11-04): the companion's "on your first turn"
    // and "on the rat's turn" lines. A story can only talk between fights - <<fight>> stops it until the fight is
    // over - so a node that says when it plays, in a header, is played at that moment instead of being jumped to:
    //
    //     title: rat_first_turn
    //     speaker: companion
    //     cue: granary_rat hero_turn 1
    //
    // The lines are said, not run: a cue node's commands, options and branches are not read (the fight is the
    // table's), only its lines, in order. Checked before writing it: the bark pools (BarkBank) play a random line
    // of a pool, the beats (BeatBook) are intents with no moment, and CompanionMind's own fight-start bark has no
    // author's words; none says a written line at a written moment.
    public enum CueMoment
    {
        // the fight's first moment, once initiative is rolled: "start"
        Start,

        // the hero's Nth turn of the fight: "hero_turn 1"
        HeroTurn,

        // the Nth turn any of the hero's foes takes: "foe_turn 2" is the second foe turn of the fight, whoever's
        FoeTurn,

        // the Nth time the hero's attack or spell is judged (hit or miss, saved or not): "hero_attack 1"
        HeroAttack,
    }

    public readonly struct Cue : IEquatable<Cue>
    {
        public Cue(string fight, CueMoment moment, int count)
        {
            Fight = fight ?? "";
            Moment = moment;
            Count = moment == CueMoment.Start ? 0 : count;
        }

        // the fight entry's id: <<fight granary_rat>>, or the entry an <<encounter>> picked
        public string Fight { get; }

        public CueMoment Moment { get; }

        // which one: the 1st hero turn, the 2nd foe turn. 0 for the start
        public int Count { get; }

        // "granary_rat start", "granary_rat hero_turn 1"
        public static bool TryParse(string text, out Cue cue, out string problem)
        {
            cue = default;
            problem = null;

            string[] words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length < 2 || !Schema.Json.IsId(words[0]) || !EnumWords.TryParse(words[1], out CueMoment moment))
            {
                problem = $"'{text}' - a cue is a fight and a moment: 'granary_rat start', 'granary_rat hero_turn 1'. " +
                          $"The moments are {string.Join(", ", EnumWords.Ids<CueMoment>())}";
                return false;
            }

            int count = 0;

            if (moment == CueMoment.Start)
            {
                if (words.Length != 2)
                {
                    problem = $"'{text}' - 'start' happens once, so it takes no number";
                    return false;
                }
            }
            else if (words.Length != 3 || !int.TryParse(words[2], out count) || count < 1)
            {
                problem = $"'{text}' - say which one: '{words[0]} {words[1]} 1' is the first";
                return false;
            }

            cue = new Cue(words[0], moment, count);
            return true;
        }

        public bool Equals(Cue other) =>
            string.Equals(Fight, other.Fight, StringComparison.Ordinal) && Moment == other.Moment && Count == other.Count;

        public override bool Equals(object obj) => obj is Cue other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Fight, Moment, Count);

        public override string ToString() =>
            Moment == CueMoment.Start ? $"{Fight} {Moment.Id()}" : $"{Fight} {Moment.Id()} {Count}";
    }

    // watches one fight and says when one of its moments comes round: the lines of the cue node written for it, if
    // there is one. Raised on the rules thread, like every observer
    public sealed class CueWatcher : CombatObserver
    {
        readonly string _fight;
        readonly Actor _hero;
        readonly Func<Cue, IReadOnlyList<DialogueLine>> _lines;

        int _heroTurns;
        int _foeTurns;
        int _attacks;

        public CueWatcher(string fight, Actor hero, Func<Cue, IReadOnlyList<DialogueLine>> lines)
        {
            _fight = fight ?? "";
            _hero = hero;
            _lines = lines;
        }

        public event Action<IReadOnlyList<DialogueLine>> Said;

        public override void Began(IReadOnlyList<InitiativeRoll> order) => Fire(CueMoment.Start, 0);

        public override void TurnBegan(Turn turn)
        {
            if (turn?.Actor == null) return;

            if (ReferenceEquals(turn.Actor, _hero)) Fire(CueMoment.HeroTurn, ++_heroTurns);
            else if (turn.Actor.Side != _hero?.Side) Fire(CueMoment.FoeTurn, ++_foeTurns);
        }

        public override void Judged(Actor by, Actor target, Attempt attempt)
        {
            if (ReferenceEquals(by, _hero)) Fire(CueMoment.HeroAttack, ++_attacks);
        }

        void Fire(CueMoment moment, int count)
        {
            IReadOnlyList<DialogueLine> lines = _lines?.Invoke(new Cue(_fight, moment, count));

            if (lines != null && lines.Count > 0) Said?.Invoke(lines);
        }
    }
}
