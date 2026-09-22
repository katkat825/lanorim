using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Game.Tray
{
    // one die on the felt: what it is and what it came up.
    public readonly struct Felt
    {
        public readonly Die Die;
        public readonly int Value;

        // how squarely it landed - 1.0 is dead flat. below the die's own threshold it was cocked
        // and re-thrown, so anything that reaches here is a reading the table would accept
        public readonly float Alignment;

        public Felt(Die die, int value, float alignment)
        {
            Die = die;
            Value = value;
            Alignment = alignment;
        }

        public override string ToString() => $"{Die.Label()} showing {Value}";
    }

    // what the tray threw, in throw order.
    //
    // THE PHYSICS IS THE RANDOM NUMBER GENERATOR. The rules never roll first and then animate a
    // die onto the answer - the die is thrown, the felt is read, and the number it shows is fed to
    // the resolver through AsRng. That is why check-fairness measures the *physics* dice and not
    // just SeededRng: if the tray is biased, the game is biased, and no amount of correct
    // arithmetic downstream would save it.
    public sealed class TrayRoll
    {
        readonly Felt[] _felt;

        public TrayRoll(IEnumerable<Felt> felt) =>
            _felt = (felt ?? Enumerable.Empty<Felt>()).ToArray();

        public IReadOnlyList<Felt> Felt => _felt;

        public int Count => _felt.Length;

        public IEnumerable<int> Values => _felt.Select(f => f.Value);

        public int Total => _felt.Sum(f => f.Value);

        // the first die, which for a check, a save or an attack is the d20
        public int First => _felt.Length > 0 ? _felt[0].Value : 0;

        public bool Any => _felt.Length > 0;

        // hand this to a StandardResolver and core sees exactly the faces on the felt, in throw
        // order. the one seam between the physics and the rules, and it is one line wide.
        public IRng AsRng() =>
            _felt.Length == 0 ? new ScriptedRng(1) : new ScriptedRng(_felt.Select(f => f.Value).ToArray());

        public override string ToString() =>
            _felt.Length == 0 ? "an empty tray" : string.Join(", ", _felt.Select(f => f.ToString()));
    }
}
