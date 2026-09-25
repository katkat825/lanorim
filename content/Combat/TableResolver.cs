using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Resolution;

namespace Content.Combat
{
    // WHERE A PLAYER'S DICE COME FROM. The Godot table implements this with the physical tray - the
    // die is thrown, the felt is read, and those faces are the roll (game/Tray/TrayRoll.cs: "the
    // physics is the random number generator"). A test, the sim, and the "skip the physical roll"
    // setting use Digital. The call blocks until the dice have landed, which on the table means
    // the rules are running on a thread of their own while the tray does its work.
    public interface IDiceSource
    {
        IReadOnlyList<int> Throw(IReadOnlyList<Die> dice);
    }

    public sealed class Digital : IDiceSource
    {
        readonly IRng _rng;

        public Digital(IRng rng) => _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        public IReadOnlyList<int> Throw(IReadOnlyList<Die> dice) =>
            (dice ?? Array.Empty<Die>()).Select(d => d.Roll(_rng)).ToList();
    }

    // one roll, for the log and the tray's caption: whose, what, and whether it was thrown on the
    // felt or behind the screen
    public sealed class Throw
    {
        public Actor Roller { get; init; }

        public RollKind? Kind { get; init; }

        public DiceRoll Dice { get; init; }

        public IReadOnlyList<int> Faces { get; init; } = Array.Empty<int>();

        public int Total { get; init; }

        // on the felt in front of the player, rather than behind the GM screen
        public bool OnTheTable { get; init; }
    }

    // THE FIGHT'S RESOLVER AT THE TABLE: the player's own rolls go to the dice source (the tray),
    // everything else is the GM's, rolled behind the screen from a seeded generator so the same
    // fight replays the same way. Core tells it whose roll each one is (IResolver's roller
    // overloads), which is the whole of how it knows.
    public sealed class TableResolver : IResolver
    {
        readonly IRng _gm;
        readonly IDiceSource _player;
        readonly Func<Actor, bool> _isPlayer;

        public TableResolver(IRng gm, IDiceSource player, Func<Actor, bool> isPlayer)
        {
            _gm = gm ?? throw new ArgumentNullException(nameof(gm));
            _player = player ?? new Digital(gm);
            _isPlayer = isPlayer ?? (_ => false);
        }

        // every roll, in order - the combat log reads it
        public event Action<Throw> Rolled;

        bool Physical(Actor roller) => roller != null && _isPlayer(roller);

        public Attempt Resolve(RollKind kind, int modifier, int against,
                               Advantage advantage = Advantage.Flat) =>
            Resolve(kind, modifier, against, advantage, null);

        public Attempt Resolve(RollKind kind, int modifier, int against, Advantage advantage,
                               Actor roller)
        {
            // the death save is a bare d20 - no modifier, no advantage, whoever throws it
            if (kind == RollKind.Death)
            {
                modifier = 0;
                advantage = Advantage.Flat;
                against = DeathSave.Dc;
            }

            bool physical = Physical(roller);

            IRng rng = physical
                ? new ScriptedRng(Faces(Enumerable.Repeat(Die.D20, advantage.Dice()).ToList()))
                : _gm;

            D20Roll roll = D20Roll.Make(rng, modifier, advantage);

            Rolled?.Invoke(new Throw
            {
                Roller = roller, Kind = kind, Dice = new DiceRoll(roll.Faces.Count, Die.D20, modifier),
                Faces = roll.Faces, Total = roll.Total, OnTheTable = physical,
            });

            return new Attempt(kind, roll, against);
        }

        public int Roll(DiceRoll dice) => Roll(dice, null);

        public int Roll(DiceRoll dice, Actor roller) => Roll(dice, roller, out _);

        public int Roll(DiceRoll dice, out IReadOnlyList<int> faces) => Roll(dice, null, out faces);

        public int Roll(DiceRoll dice, Actor roller, out IReadOnlyList<int> faces)
        {
            bool physical = Physical(roller) && dice.RollsAnything;

            int total;

            if (physical)
            {
                faces = Faces(Enumerable.Repeat(dice.Die, dice.Count).ToList());
                total = faces.Sum() + dice.Modifier;
            }
            else
            {
                total = dice.Roll(_gm, out faces);
            }

            Rolled?.Invoke(new Throw
            {
                Roller = roller, Dice = dice, Faces = faces, Total = total, OnTheTable = physical,
            });

            return total;
        }

        // what the source threw, held to the dice asked for: a face off the end of a die is read
        // as the die's highest, never as a crash
        int[] Faces(IReadOnlyList<Die> dice)
        {
            IReadOnlyList<int> thrown = _player.Throw(dice);

            var faces = new int[dice.Count];

            for (int i = 0; i < dice.Count; i++)
            {
                int face = i < thrown.Count ? thrown[i] : dice[i].Roll(_gm);
                faces[i] = Math.Clamp(face, 1, Math.Max(1, dice[i].Sides()));
            }

            return faces;
        }
    }
}
