using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Characters;
using Core.Combat;
using Core.Localization;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Combat
{
    // ONE LINE OF THE COMBAT LOG: a key and what fills it. The words are the locale's; an Actor in
    // the arguments is shown by the presentation as the hero's typed name or the monster's key
    public sealed class LogLine
    {
        public LogLine(string key, params object[] args)
        {
            Key = key;
            Args = args ?? Array.Empty<object>();
        }

        public string Key { get; }

        public IReadOnlyList<object> Args { get; }

        public override string ToString() =>
            Key + (Args.Count == 0 ? "" : " " + string.Join(", ", Args.Select(a => a is Actor x ? x.Id : a)));
    }

    // THE COMBAT LOG THE PLAYER READS (docs/combat_ux.md): every roll in plain words - who rolled
    // what against what - and every thing that happened, as keyed lines. A roll behind the GM screen
    // is logged as "the GM rolls" with no numbers; one on the table shows its faces and total
    public sealed class FightLog : CombatObserver
    {
        readonly LineLog<LogLine> _log = new LineLog<LogLine>();

        public IReadOnlyList<LogLine> Lines => _log.Lines;

        public event Action<LogLine> Wrote;

        static string K(string what) => KeyConventions.Key(KeyConventions.UiNs, "log", what, "name");

        public static IEnumerable<string> Keys() =>
            new[] { "initiative", "round", "turn", "moved", "hit", "critical", "miss", "damage",
                    "damage_lessened", "save_made", "save_failed",
                    "opportunity", "reacted", "condition_on", "condition_off", "down", "death_save_up",
                    "death_save_dead", "won", "lost", "fled", "roll_table", "roll_hidden", "diverted",
                    "roll_attack", "roll_save", "roll_check", "roll_initiative" }
                .Select(K);

        // the verdicts the table also puts under the tray: hit, miss, critical, a save made or failed
        public static readonly IReadOnlyList<string> VerdictKeys =
            new[] { "hit", "critical", "miss", "save_made", "save_failed" }.Select(K).ToList();

        void Add(string key, params object[] args) => Write(new LogLine(K(key), args));

        void Write(LogLine line)
        {
            _log.Add(line);
            Wrote?.Invoke(line);
        }

        // what a critical hit bought from the consequence pool: its own line, as it is written
        public void Befell(Content.Schema.Consequences.Visit visit)
        {
            if (visit != null) Write(new LogLine(visit.LineKey));
        }

        TableResolver _heard;
        bool _begun;

        // hear every roll the table resolver makes, until the fight ends. The resolver outlives the
        // fight (it is the campaign's), and a log that never stopped listening wrote every later
        // roll - a story check, the next fight's initiative - into its own fight's queue as well:
        // from the second fight on, each roll was in the log twice (cc_task_table-ui-minis-zoom-damage.md)
        public void Listen(TableResolver resolver)
        {
            Stop();

            if (resolver == null) return;

            _heard = resolver;
            _heard.Rolled += Heard;
        }

        public void Stop()
        {
            if (_heard != null) _heard.Rolled -= Heard;

            _heard = null;
        }

        // the hero's own rolls, with what each is for. Rolls heard before the fight's first round are
        // its initiative (a Dexterity check, so the resolver can't tell it from any other)
        void Heard(Throw roll)
        {
            if (!roll.OnTheTable) return;

            string key = roll.Kind switch
            {
                RollKind.Attack => "roll_attack",
                RollKind.Save => "roll_save",
                RollKind.Check => _begun ? "roll_check" : "roll_initiative",
                _ => "roll_table",
            };

            Add(key, roll.Roller, roll.Dice.ToString(), string.Join(" ", roll.Faces), roll.Total);
        }

        public override void Began(IReadOnlyList<InitiativeRoll> order)
        {
            _begun = true;
            Add("initiative", string.Join(", ", order.Select(o => o.Total)));
        }

        public override void RoundBegan(int round) => Add("round", round);

        public override void TurnBegan(Turn turn) => Add("turn", turn.Actor);

        public override void Moved(Actor actor, IReadOnlyList<Cell> route) =>
            Add("moved", actor, route.Count - 1);

        // hit, miss or critical against the armor class; made or failed against the DC. Said the
        // moment it is settled, so the damage throw follows the word "hit"
        public override void Judged(Actor by, Actor target, Attempt attempt)
        {
            if (attempt == null) return;

            if (attempt.Kind == RollKind.Save)
            {
                Add(attempt.Succeeded ? "save_made" : "save_failed", target, attempt.Total, attempt.Against);
                return;
            }

            if (attempt.Kind != RollKind.Attack) return;

            if (attempt.Diverted) Add("diverted", by, target);
            else if (!attempt.Succeeded) Add("miss", by, target, attempt.Total, attempt.Against);
            else Add(attempt.IsCritical ? "critical" : "hit", by, target, attempt.Total, attempt.Against);
        }

        // "Greataxe hits Giant Rat: 1d12+3, for 9 Slashing damage", and the hit points it really lost
        // when that is fewer (resistance, or less left to lose)
        public override void Dealt(Harm harm)
        {
            if (harm == null) return;

            var what = new Named(harm.WhatKey, harm.By);

            if (harm.Suffered == harm.Rolled)
                Add("damage", what, harm.Target, harm.Dice.ToString(), harm.Rolled, harm.Type.NameKey());
            else
                Add("damage_lessened", what, harm.Target, harm.Dice.ToString(), harm.Rolled, harm.Suffered,
                    harm.Type.NameKey());
        }

        public override void Opportunity(Actor attacker, Actor fleeing) =>
            Add("opportunity", attacker, fleeing);

        public override void Reacted(Actor reactor, string reaction, Moment moment) =>
            Add("reacted", reactor, reaction);

        public override void ConditionChanged(Actor actor, Condition condition, bool applied) =>
            Add(applied ? "condition_on" : "condition_off", actor, condition.NameKey());

        public override void Downed(Actor actor) => Add("down", actor);

        public override void DeathSaved(Actor actor, Attempt attempt) =>
            Add(attempt.Succeeded ? "death_save_up" : "death_save_dead", actor, attempt.Total);

        public override void Ended(Outcome outcome)
        {
            Stop();
            Add(outcome == Outcome.HeroesWon ? "won" : outcome == Outcome.Fled ? "fled" : "lost");
        }

        public override string ToString() => _log.ToString();
    }
}
