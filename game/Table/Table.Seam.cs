using System;
using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Rules;
using Game.Tray;

namespace Game.Table
{
    public partial class Table
    {
        // --- the seam ---------------------------------------------------------------------------

        // A QUESTION PUT TO THE DICE, and what to do with the answer.
        //
        // THE PHYSICS IS THE RANDOM NUMBER GENERATOR. Nothing rolls a number and then animates a
        // die onto it: the die is thrown, the felt is read, and the faces go to core through a
        // ScriptedRng. That is the whole of the coupling between the table and the rules, and it is
        // why check-dice measures the tray and not just the generator - a biased tray would be a
        // biased game no matter how correct the arithmetic downstream.
        sealed class Ask
        {
            public Ask(Die[] dice, Func<IResolver, object> answer, Action<object> tell)
            {
                Dice = dice;
                Answer = answer;
                Tell = tell;
            }

            public Die[] Dice { get; }

            // given a resolver reading the felt, works out what the throw meant
            public Func<IResolver, object> Answer { get; }

            public Action<object> Tell { get; }
        }

        public bool Busy => _asking != null || (Tray?.IsThrowing ?? false);

        // the general form: throw these dice, then let core read them
        bool Put(Die[] dice, Func<IResolver, object> answer, Action<object> tell)
        {
            if (Tray == null || Busy) return false;

            _asking = new Ask(dice, answer, tell);

            if (Tray.Throw(dice)) return true;

            _asking = null;
            return false;
        }

        void Read(TrayRoll roll)
        {
            Ask asking = _asking;
            _asking = null;

            if (asking == null) return;

            // the faces on the felt, in throw order, as the only randomness core sees
            var resolver = new StandardResolver(roll.AsRng());

            object answer = asking.Answer(resolver);

            asking.Tell?.Invoke(answer);
        }


        // --- what the game actually asks ---------------------------------------------------------

        // a skill check. advantage throws two d20 and core keeps the one the rules say to keep,
        // and both stay on the felt where the player can see them
        public bool Check(Actor actor, Skill skill, int dc, Advantage advantage,
                          Action<Attempt> then)
        {
            Die[] dice = Enumerable.Repeat(Die.D20, advantage.Dice()).ToArray();

            return Put(dice,
                       r => Checks.Check(r, actor, skill, dc, advantage),
                       a => then?.Invoke((Attempt)a));
        }

        public bool Save(Actor actor, Ability ability, int dc, Advantage advantage,
                         Action<Attempt> then)
        {
            Die[] dice = Enumerable.Repeat(Die.D20, advantage.Dice()).ToArray();

            return Put(dice,
                       r => Checks.Save(r, actor, ability, dc, advantage),
                       a => then?.Invoke((Attempt)a));
        }

        // the single d20 against 10, no modifiers, that decides whether a downed hero gets up
        public bool DeathSave(Actor actor, Action<Attempt> then) =>
            Put(new[] { Die.D20 },
                r => Checks.DeathSave(r, actor),
                a => then?.Invoke((Attempt)a));

        // an attack. the damage is a second throw, because that is two handfuls at a table and the
        // player watches the first one land before the second goes up
        public bool Strike(Actor attacker, Actor target, Attack attack, Advantage advantage,
                           IReadOnlyList<Rider> riders, Action<Blow> then)
        {
            Die[] dice = Enumerable.Repeat(Die.D20, advantage.Dice()).ToArray();

            return Put(dice,
                       r => Core.Rules.Strike.Make(r, attacker, target, attack, advantage, riders),
                       a => then?.Invoke((Blow)a));
        }

        // damage, healing, hit dice - anything that is not a d20 against a number
        public bool Roll(DiceRoll dice, Action<int> then)
        {
            if (!dice.RollsAnything)
            {
                // nothing to throw, so nothing is thrown and the flat number comes straight back
                then?.Invoke(dice.Modifier);
                return true;
            }

            Die[] handful = Enumerable.Repeat(dice.Die, dice.Count).ToArray();

            return Put(handful, r => r.Roll(dice), a => then?.Invoke((int)a));
        }
    }
}
