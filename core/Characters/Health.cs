using System;
using Core.Dice;
using Core.Resolution;

namespace Core.Characters
{
    // hit points, temporary hit points and hit dice. the one place damage lands, so the floor at
    // zero and the "damage eats temporary HP first" rule can't be got wrong in two places.
    public sealed class Health
    {
        public Health(int maximum, Die hitDie = Die.None, int hitDice = 0)
        {
            Maximum = Math.Max(1, maximum);
            Current = Maximum;
            HitDie = hitDie;
            HitDiceMax = Math.Max(0, hitDice);
            HitDice = HitDiceMax;
        }

        public int Maximum { get; private set; }

        public int Current { get; private set; }

        // SRD: temporary HP is not healing, doesn't stack (the bigger pool wins) and is lost first
        public int Temporary { get; private set; }

        public Die HitDie { get; }

        public int HitDiceMax { get; private set; }

        public int HitDice { get; private set; }

        public bool IsDown => Current <= 0;

        public bool IsBloodied => Current * 2 <= Maximum;

        public int Missing => Maximum - Current;

        public void SetMaximum(int maximum)
        {
            Maximum = Math.Max(1, maximum);

            if (Current > Maximum) Current = Maximum;
        }

        public void SetHitDiceMax(int dice)
        {
            HitDiceMax = Math.Max(0, dice);

            if (HitDice > HitDiceMax) HitDice = HitDiceMax;
        }

        // returns what was actually taken off hit points, which is what a "you took N" line reads
        public int Take(int damage)
        {
            if (damage <= 0) return 0;

            int fromTemporary = Math.Min(Temporary, damage);
            Temporary -= fromTemporary;

            int rest = damage - fromTemporary;
            int taken = Math.Min(Current, rest);

            // floors at zero: there is no negative-HP track in v1, the death save decides
            Current -= taken;

            return taken;
        }

        public int Heal(int amount)
        {
            if (amount <= 0 || Current >= Maximum) return 0;

            int healed = Math.Min(amount, Maximum - Current);
            Current += healed;

            return healed;
        }

        public void GrantTemporary(int amount)
        {
            if (amount > Temporary) Temporary = amount;
        }

        public void Revive(int at = DeathSave.RevivedAt) =>
            Current = Math.Clamp(at, 1, Maximum);

        public void Kill()
        {
            Current = 0;
            Temporary = 0;
        }

        // SRD short rest: spend a hit die, heal the roll plus your CON modifier, minimum 1
        public int SpendHitDie(IResolver resolver, int constitutionModifier)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            if (HitDice <= 0 || !HitDie.IsReal() || Current >= Maximum) return 0;

            HitDice--;

            int rolled = Math.Max(1, resolver.Roll(new DiceRoll(1, HitDie, constitutionModifier)));

            return Heal(rolled);
        }

        // long rest is the delta: full HP, not half your dice back. updated_decisions.md -
        // "long rest = full hp recovery". SRD's half-your-hit-dice return is kept.
        public void LongRest()
        {
            Current = Maximum;
            Temporary = 0;
            HitDice = HitDiceMax;
        }

        public void ShortRest()
        {
            // nothing on its own - a short rest only returns what a feature says it does, and hit
            // dice are spent by choice, not restored
        }

        public override string ToString() =>
            $"{Current}/{Maximum} hp" + (Temporary > 0 ? $" (+{Temporary} temp)" : "") +
            (HitDie.IsReal() ? $", {HitDice}/{HitDiceMax}{HitDie.Label()}" : "");
    }
}
