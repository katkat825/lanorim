using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Magic
{
    // MODE A - SPELL SLOTS, the SRD default and what a new character is offered first.
    //
    // Counts by slot level, and a cast spends one slot of the level it was cast at. Upcasting is
    // not a special case here: casting a 1st-level spell at 3rd spends a 3rd-level slot, because
    // `castLevel` IS the level the player chose. Nothing about upcasting had to be taught to this
    // class - Casting already carried the number.
    //
    // WHEN THE CHOSEN LEVEL IS EMPTY it climbs rather than refusing: a 3rd-level cast with no 3rd
    // slot left spends a 4th. That is the SRD rule and it is also the kind thing to do, but it is
    // worth being explicit that it never climbs DOWN - a 3rd-level cast is never paid for with a
    // 2nd-level slot, because the spell is doing 3rd-level work.
    public sealed class SpellSlots : ISpellResource
    {
        // index by slot level; [0] is unused so the arithmetic reads like the rules do
        readonly int[] _maximum = new int[SpellLevels.Highest + 1];

        readonly int[] _remaining = new int[SpellLevels.Highest + 1];

        public SpellSlots(IReadOnlyList<int> byLevel)
        {
            if (byLevel == null) throw new ArgumentNullException(nameof(byLevel));

            for (int level = SpellLevels.Lowest; level <= SpellLevels.Highest; level++)
            {
                int count = level - 1 < byLevel.Count ? Math.Max(0, byLevel[level - 1]) : 0;

                _maximum[level] = count;
                _remaining[level] = count;
            }
        }

        public SpellResourceMode Mode => SpellResourceMode.Slots;

        public int Maximum(int level) =>
            SpellLevels.IsLeveled(level) ? _maximum[level] : 0;

        public int Remaining(int level) =>
            SpellLevels.IsLeveled(level) ? _remaining[level] : 0;

        // the whole grid, for the sheet's bubbles. 1-based to match how slots are spoken about
        public IEnumerable<(int Level, int Remaining, int Maximum)> Grid =>
            Enumerable.Range(SpellLevels.Lowest, SpellLevels.Highest)
                      .Where(level => _maximum[level] > 0)
                      .Select(level => (level, _remaining[level], _maximum[level]));

        public bool CanPay(int castLevel) => Cheapest(castLevel) > 0;

        public bool Pay(int castLevel)
        {
            int level = Cheapest(castLevel);

            if (level == 0) return false;

            _remaining[level]--;

            return true;
        }

        // the lowest slot that could pay for a cast at this level - the chosen level if it has one
        // free, and the next one up if it does not. 0 when nothing can
        int Cheapest(int castLevel)
        {
            if (!SpellLevels.IsLeveled(castLevel)) return 0;

            for (int level = castLevel; level <= SpellLevels.Highest; level++)
                if (_remaining[level] > 0) return level;

            return 0;
        }

        // A SAVE PUTTING BACK WHAT WAS LEFT, and nothing else calls it. The maximum is never
        // written to a save - it comes off the table - so what is left is clamped to it, and a
        // retuned table that hands out fewer slots wins over the save that remembers more.
        public void SetRemaining(int level, int remaining)
        {
            if (!SpellLevels.IsLeveled(level)) return;

            _remaining[level] = Math.Clamp(remaining, 0, _maximum[level]);
        }

        public void Restore(Rest rest)
        {
            // SHORT REST GIVES NOTHING BACK. decisions_checklist.md section 1 pins recovery to the
            // SRD, where a long rest refills every slot and a short rest spends hit dice and no
            // more. The Warlock is the one SRD caster whose slots come back on a short rest, and
            // lanorim has no Warlock - the pact identity was folded into the Mage.
            if (rest != Rest.Long) return;

            Array.Copy(_maximum, _remaining, _maximum.Length);
        }

        public int Highest
        {
            get
            {
                for (int level = SpellLevels.Highest; level >= SpellLevels.Lowest; level--)
                    if (_remaining[level] > 0) return level;

                return 0;
            }
        }

        public string Describe() =>
            string.Join(", ", Grid.Select(s => $"{s.Level}: {s.Remaining}/{s.Maximum}"));

        public override string ToString() => "slots " + Describe();


        // --- the two standard tables ---------------------------------------------------------

        // THE SRD SLOT TABLES, AS DATA. A class data file names a progression; it does not write
        // out twenty rows of its own, because twenty rows per class is nineteen chances for a
        // caster to disagree with the rules by a typo.
        //
        // Row n is character level n; the nine numbers are 1st- to 9th-level slots.
        static readonly int[][] FullTable =
        {
            new[] { 2, 0, 0, 0, 0, 0, 0, 0, 0 },   //  1
            new[] { 3, 0, 0, 0, 0, 0, 0, 0, 0 },   //  2
            new[] { 4, 2, 0, 0, 0, 0, 0, 0, 0 },   //  3
            new[] { 4, 3, 0, 0, 0, 0, 0, 0, 0 },   //  4
            new[] { 4, 3, 2, 0, 0, 0, 0, 0, 0 },   //  5
            new[] { 4, 3, 3, 0, 0, 0, 0, 0, 0 },   //  6
            new[] { 4, 3, 3, 1, 0, 0, 0, 0, 0 },   //  7
            new[] { 4, 3, 3, 2, 0, 0, 0, 0, 0 },   //  8
            new[] { 4, 3, 3, 3, 1, 0, 0, 0, 0 },   //  9
            new[] { 4, 3, 3, 3, 2, 0, 0, 0, 0 },   // 10
            new[] { 4, 3, 3, 3, 2, 1, 0, 0, 0 },   // 11
            new[] { 4, 3, 3, 3, 2, 1, 0, 0, 0 },   // 12
            new[] { 4, 3, 3, 3, 2, 1, 1, 0, 0 },   // 13
            new[] { 4, 3, 3, 3, 2, 1, 1, 0, 0 },   // 14
            new[] { 4, 3, 3, 3, 2, 1, 1, 1, 0 },   // 15
            new[] { 4, 3, 3, 3, 2, 1, 1, 1, 0 },   // 16
            new[] { 4, 3, 3, 3, 2, 1, 1, 1, 1 },   // 17
            new[] { 4, 3, 3, 3, 3, 1, 1, 1, 1 },   // 18
            new[] { 4, 3, 3, 3, 3, 2, 1, 1, 1 },   // 19
            new[] { 4, 3, 3, 3, 3, 2, 2, 1, 1 },   // 20
        };

        // nothing at 1, and it never reaches past 5th
        static readonly int[][] HalfTable =
        {
            new[] { 2, 0, 0, 0, 0 },   //  1  (SRD 5.2.1: a Paladin casts from level 1)
            new[] { 2, 0, 0, 0, 0 },   //  2
            new[] { 3, 0, 0, 0, 0 },   //  3
            new[] { 3, 0, 0, 0, 0 },   //  4
            new[] { 4, 2, 0, 0, 0 },   //  5
            new[] { 4, 2, 0, 0, 0 },   //  6
            new[] { 4, 3, 0, 0, 0 },   //  7
            new[] { 4, 3, 0, 0, 0 },   //  8
            new[] { 4, 3, 2, 0, 0 },   //  9
            new[] { 4, 3, 2, 0, 0 },   // 10
            new[] { 4, 3, 3, 0, 0 },   // 11
            new[] { 4, 3, 3, 0, 0 },   // 12
            new[] { 4, 3, 3, 1, 0 },   // 13
            new[] { 4, 3, 3, 1, 0 },   // 14
            new[] { 4, 3, 3, 2, 0 },   // 15
            new[] { 4, 3, 3, 2, 0 },   // 16
            new[] { 4, 3, 3, 3, 1 },   // 17
            new[] { 4, 3, 3, 3, 1 },   // 18
            new[] { 4, 3, 3, 3, 2 },   // 19
            new[] { 4, 3, 3, 3, 2 },   // 20
        };

        public static IReadOnlyList<int> Table(CasterProgression progression, int casterLevel)
        {
            int at = Math.Clamp(casterLevel, 1, 20) - 1;

            return progression switch
            {
                CasterProgression.Full => FullTable[at],
                CasterProgression.Half => HalfTable[at],
                _ => Array.Empty<int>(),
            };
        }

        public static SpellSlots For(CasterProgression progression, int casterLevel) =>
            new SpellSlots(Table(progression, casterLevel));
    }
}
