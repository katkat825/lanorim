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
            _maximum = Math.Max(1, maximum);
            _current = _maximum;
            HitDie = hitDie;
            HitDiceMax = Math.Max(0, hitDice);
            HitDice = HitDiceMax;
        }

        int _maximum;
        int _current;

        // what raises the maximum for a while without changing the base - Aid. the owner hands
        // this in (Actor reads it off its boons), so the maximum is asked, never stored twice
        Func<int> _raise;

        public void RaisedBy(Func<int> raise) => _raise = raise;

        public int Raised => Math.Max(0, _raise?.Invoke() ?? 0);

        public int Maximum => _maximum + Raised;

        // clamped as it is read, so a maximum that falls (Aid ending) takes current down with it
        public int Current
        {
            get => Math.Min(_current, Maximum);
            private set => _current = value;
        }

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
            _maximum = Math.Max(1, maximum);

            if (_current > Maximum) _current = Maximum;
        }

        public void SetHitDiceMax(int dice)
        {
            HitDiceMax = Math.Max(0, dice);

            if (HitDice > HitDiceMax) HitDice = HitDiceMax;
        }

        // a save putting back how many were left. clamped, because the maximum is the class's
        // and a retuned class may hand out fewer than the save remembers
        public void SetHitDice(int dice) => HitDice = Math.Clamp(dice, 0, HitDiceMax);

        // returns what was actually taken off hit points, which is what a "you took N" line reads
        public int Take(int damage)
        {
            if (damage <= 0) return 0;

            Current = Current;

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
            Current = Current;

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

        // SRD 5.2.1 Long Rest: all lost hit points and all spent Hit Point Dice come back.
        // decisions_checklist.md's Rest line still says "half hit dice" (the 2014 rule) - flagged
        // for Kathleen in RUN_LOG_2026-09-25.md; the code follows the SRD text until she decides
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
