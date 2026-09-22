using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Characters
{
    // six numbers and the arithmetic on them. mutable, because an ASI, a species bump and a
    // consequence's until-rest shift all land here - but the shift is kept apart from the score so
    // a rest can take it back without remembering what the score used to be.
    public sealed class AbilityScores
    {
        readonly int[] _base = new int[Abilities.Count];
        readonly int[] _shift = new int[Abilities.Count];

        public AbilityScores(int all = 10)
        {
            for (int i = 0; i < Abilities.Count; i++) _base[i] = all;
        }

        public AbilityScores(int strength, int dexterity, int constitution,
                             int intelligence, int wisdom, int charisma)
        {
            _base[(int)Ability.Strength] = strength;
            _base[(int)Ability.Dexterity] = dexterity;
            _base[(int)Ability.Constitution] = constitution;
            _base[(int)Ability.Intelligence] = intelligence;
            _base[(int)Ability.Wisdom] = wisdom;
            _base[(int)Ability.Charisma] = charisma;
        }

        public static AbilityScores From(IReadOnlyDictionary<Ability, int> scores)
        {
            var made = new AbilityScores();

            if (scores == null) return made;

            foreach (KeyValuePair<Ability, int> pair in scores) made.SetBase(pair.Key, pair.Value);

            return made;
        }

        // the score on the sheet before until-rest shifts
        public int Base(Ability ability) => _base[(int)ability];

        public void SetBase(Ability ability, int score) =>
            _base[(int)ability] = Math.Clamp(score, Abilities.Floor, Abilities.Ceiling);

        // an ASI, or a species bump. never takes a score past 20 (decisions_checklist.md section 1)
        public int Raise(Ability ability, int by)
        {
            int was = Base(ability);
            SetBase(ability, was + by);
            return Base(ability) - was;
        }

        public int Shift(Ability ability) => _shift[(int)ability];

        // the consequence pool's "minus 1 to wisdom until you rest"
        public void ShiftUntilRest(Ability ability, int by) => _shift[(int)ability] += by;

        public void Rested()
        {
            for (int i = 0; i < Abilities.Count; i++) _shift[i] = 0;
        }

        public bool AnyShifted => _shift.Any(s => s != 0);

        // what every roll actually reads. the ceiling applies to the score you earned, not to a
        // temporary shift, so a boon can carry a 20 to 21 for one adventuring day.
        public int Score(Ability ability) =>
            Math.Max(Abilities.Floor, Base(ability) + Shift(ability));

        public int Modifier(Ability ability) => Abilities.Modifier(Score(ability));

        public int this[Ability ability] => Score(ability);

        public int PointBuySpend =>
            Abilities.All.Sum(a => Math.Max(0, Abilities.PointBuyCost(Base(a))));

        // a legal point-buy array: every score in 8..15 and the budget not overspent. species
        // bumps are applied after, so this is asked of the array as picked, not of the finished hero.
        public bool IsLegalPointBuy(out string problem)
        {
            foreach (Ability a in Abilities.All)
            {
                int score = Base(a);

                if (score < Abilities.PointBuyFloor || score > Abilities.PointBuyCeiling)
                {
                    problem = $"{a.Id()} is {score}; point buy runs " +
                              $"{Abilities.PointBuyFloor}-{Abilities.PointBuyCeiling}";
                    return false;
                }
            }

            int spend = PointBuySpend;

            if (spend > Abilities.PointBuyBudget)
            {
                problem = $"{spend} points spent of {Abilities.PointBuyBudget}";
                return false;
            }

            problem = null;
            return true;
        }

        public AbilityScores Copy()
        {
            var copy = new AbilityScores();

            for (int i = 0; i < Abilities.Count; i++)
            {
                copy._base[i] = _base[i];
                copy._shift[i] = _shift[i];
            }

            return copy;
        }

        public override string ToString() =>
            string.Join(" ", Abilities.All.Select(a =>
                $"{a.Id()} {Score(a)}({Modifier(a):+0;-0;+0})"));
    }
}
