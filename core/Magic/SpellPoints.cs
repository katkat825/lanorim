using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Magic
{
    // MODE B - SPELL POINTS. One pool and a price list, for a player who would rather not track a
    // grid of bubbles.
    //
    // PROVENANCE, AND IT MATTERS. This is NOT the SRD. Spell points are an optional variant from
    // the Dungeon Master's Guide, which is not the document lanorim is built on. What is
    // implemented here is the MECHANIC - a pool, a cost per level, a cap - because game mechanics
    // and the numbers in them are not copyrightable, and it ships under our own labeling.
    // NOTHING here may be cited as SRD or carried under the SRD's CC-BY attribution
    // (decisions_checklist.md section 1, and srd_legal_decisions.md).
    //
    // THE 6+ CAP IS LOAD-BEARING, not flavour. Without it, points is not a different way to pay for
    // the same day - it is strictly more powerful, because a pool can be poured entirely into the
    // top of the spell list while slots physically cannot. One 6th-, 7th-, 8th- and 9th-level cast
    // per long rest is exactly what the slot table allows at those levels, so the cap is what makes
    // the two modes a choice rather than a right answer.
    public sealed class SpellPoints : ISpellResource
    {
        // spent-this-day flags for the levels that are once a day; index by level
        readonly bool[] _spentHigh = new bool[SpellLevels.Highest + 1];

        public SpellPoints(int maximum, int highestLevel = SpellLevels.Highest)
        {
            Maximum = Math.Max(0, maximum);
            Remaining = Maximum;
            HighestLevel = Math.Clamp(highestLevel, 0, SpellLevels.Highest);
        }

        // THE HIGHEST SPELL LEVEL THIS CASTER MAY REACH AT ALL, and without it the whole mode
        // is broken. A pool is just a number of points, so a 5th-level caster holding 27 of
        // them could pay the 13 a 9th-level spell costs - while the slot table gives them
        // nothing above 3rd. The 6+ once-a-day cap does not catch this: it limits how OFTEN a
        // high spell goes off, not whether this caster has any business casting one.
        //
        // It is read off the slot table rather than kept as a second table, so the two modes
        // cannot disagree about what a level-7 Cleric can reach.
        public int HighestLevel { get; }

        public SpellResourceMode Mode => SpellResourceMode.Points;

        public int Maximum { get; }

        public int Remaining { get; private set; }

        // THE PRICE LIST. Fixed per spell level, and an upcast simply pays the higher level's
        // price - which is the whole of upcasting in this mode.
        static readonly IReadOnlyDictionary<int, int> Prices = new Dictionary<int, int>
        {
            [1] = 2, [2] = 3, [3] = 5, [4] = 6, [5] = 7,
            [6] = 9, [7] = 10, [8] = 11, [9] = 13,
        };

        public static int CostOf(int spellLevel) =>
            Prices.TryGetValue(spellLevel, out int cost) ? cost : 0;

        // a cantrip is level 0 and never reaches here, but saying so costs nothing
        public static bool IsOncePerDay(int spellLevel) => spellLevel >= SpellLevels.HighLevel;

        public bool HasSpent(int spellLevel) =>
            SpellLevels.IsLeveled(spellLevel) && _spentHigh[spellLevel];

        public bool CanPay(int castLevel)
        {
            if (!SpellLevels.IsLeveled(castLevel)) return false;

            if (castLevel > HighestLevel) return false;

            if (IsOncePerDay(castLevel) && _spentHigh[castLevel]) return false;

            return Remaining >= CostOf(castLevel);
        }

        public bool Pay(int castLevel)
        {
            if (!CanPay(castLevel)) return false;

            Remaining -= CostOf(castLevel);

            if (IsOncePerDay(castLevel)) _spentHigh[castLevel] = true;

            return true;
        }

        // A SAVE PUTTING BACK THE DAY, and nothing else calls it. Not done by paying, because a
        // pool with 4 points left and a 6th-level cast behind it could never be reached that way:
        // the cast costs 9. Only the once-a-day levels are marked; anything lower was never a flag.
        public void Resume(int remaining, IEnumerable<int> spentHighLevels = null)
        {
            Remaining = Math.Clamp(remaining, 0, Maximum);

            Array.Clear(_spentHigh, 0, _spentHigh.Length);

            foreach (int level in spentHighLevels ?? Enumerable.Empty<int>())
                if (SpellLevels.IsLeveled(level) && IsOncePerDay(level)) _spentHigh[level] = true;
        }

        public void Restore(Rest rest)
        {
            // same rule as slots: the day's magic comes back on a long rest and not before
            if (rest != Rest.Long) return;

            Remaining = Maximum;

            Array.Clear(_spentHigh, 0, _spentHigh.Length);
        }

        public int Highest
        {
            get
            {
                for (int level = SpellLevels.Highest; level >= SpellLevels.Lowest; level--)
                    if (CanPay(level)) return level;

                return 0;
            }
        }

        public string Describe()
        {
            string pool = $"{Remaining}/{Maximum} points, up to level {HighestLevel}";

            var spent = Enumerable.Range(SpellLevels.HighLevel,
                                         SpellLevels.Highest - SpellLevels.HighLevel + 1)
                                  .Where(level => _spentHigh[level])
                                  .ToList();

            return spent.Count == 0
                ? pool
                : pool + ", spent today: " + string.Join(", ", spent);
        }

        public override string ToString() => "points " + Describe();


        // --- the pool by level -----------------------------------------------------------------

        // Row n is character level n. A half caster does not get its own row: it reads this table
        // at half its level, which is the same trick the slot tables use and keeps the two modes
        // the same size at the same character level.
        static readonly int[] Pool =
        {
            4, 6, 14, 17, 27, 32, 38, 44, 57, 64,
            73, 73, 83, 83, 94, 94, 107, 114, 123, 133,
        };

        public static int PoolFor(CasterProgression progression, int casterLevel)
        {
            int level = Math.Clamp(casterLevel, 1, 20);

            int effective = progression switch
            {
                CasterProgression.Full => level,

                // a half caster counts half its levels, rounded up (SRD 5.2.1 p.25) - so a level 1
                // Paladin has a first-level caster's pool
                CasterProgression.Half => (level + 1) / 2,

                _ => 0,
            };

            return effective < 1 ? 0 : Pool[effective - 1];
        }

        // the top of the slot table this caster has reached; 0 for a caster with no magic yet
        public static int HighestLevelFor(CasterProgression progression, int casterLevel)
        {
            IReadOnlyList<int> table = SpellSlots.Table(progression, casterLevel);

            for (int level = table.Count; level >= 1; level--)
                if (table[level - 1] > 0) return level;

            return 0;
        }

        public static SpellPoints For(CasterProgression progression, int casterLevel) =>
            new SpellPoints(PoolFor(progression, casterLevel),
                            HighestLevelFor(progression, casterLevel));
    }
}
