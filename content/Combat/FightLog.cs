using System;
using System.Collections.Generic;
using System.Linq;
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
        readonly List<LogLine> _lines = new List<LogLine>();

        public IReadOnlyList<LogLine> Lines => _lines;

        public event Action<LogLine> Wrote;

        static string K(string what) => KeyConventions.Key(KeyConventions.UiNs, "log", what, "name");

        public static IEnumerable<string> Keys() =>
            new[] { "initiative", "round", "turn", "moved", "hit", "critical", "miss", "damage",
                    "opportunity", "reacted", "condition_on", "condition_off", "down", "death_save_up",
                    "death_save_dead", "won", "lost", "fled", "roll_table", "roll_hidden", "diverted" }
                .Select(K);

        void Add(string key, params object[] args)
        {
            var line = new LogLine(K(key), args);
            _lines.Add(line);
            Wrote?.Invoke(line);
        }

        // hear every roll the table resolver makes
        public void Listen(TableResolver resolver)
        {
            if (resolver == null) return;

            resolver.Rolled += roll =>
            {
                if (roll.OnTheTable)
                    Add("roll_table", roll.Roller, roll.Dice.ToString(), string.Join(" ", roll.Faces),
                        roll.Total);
            };
        }

        public override void Began(IReadOnlyList<InitiativeRoll> order) =>
            Add("initiative", string.Join(", ", order.Select(o => o.Total)));

        public override void RoundBegan(int round) => Add("round", round);

        public override void TurnBegan(Turn turn) => Add("turn", turn.Actor);

        public override void Moved(Actor actor, IReadOnlyList<Cell> route) =>
            Add("moved", actor, route.Count - 1);

        public override void Struck(Blow blow)
        {
            if (blow.Attempt.Diverted)
            {
                Add("diverted", blow.Attacker, blow.Target);
                return;
            }

            if (!blow.Hit)
            {
                Add("miss", blow.Attacker, blow.Target, blow.Attempt.Total, blow.Attempt.Against);
                return;
            }

            Add(blow.Critical ? "critical" : "hit", blow.Attacker, blow.Target, blow.Attempt.Total,
                blow.Attempt.Against);
            Add("damage", blow.Target, blow.Suffered, blow.Attack?.DamageType.NameKey() ?? "");
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

        public override void Ended(Outcome outcome) =>
            Add(outcome == Outcome.HeroesWon ? "won" : outcome == Outcome.Fled ? "fled" : "lost");

        public override string ToString() => string.Join("\n", _lines);
    }
}
