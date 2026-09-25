using System;
using System.Collections.Generic;
using System.Linq;
using Content.Combat;
using Core.Dice;
using Game.Tray;
using Godot;

namespace Game.Play
{
    // THE HERO'S DICE ARE THE TRAY'S. The rules ask for faces (on the rules thread); this puts the
    // question to the table, waits for the player to throw - Space, or the tray itself - and hands
    // back what the felt shows, in throw order. The physics is the random number generator.
    //
    // With "skip throwing the dice" on, or when asked from the main thread (a test, a probe), the
    // faces come from a generator instead and nothing is thrown.
    public sealed class TrayDice : IDiceSource
    {
        readonly DiceTray _tray;
        readonly IRng _fallback;

        public TrayDice(DiceTray tray, IRng fallback)
        {
            _tray = tray ?? throw new ArgumentNullException(nameof(tray));
            _fallback = fallback ?? new SeededRng(System.Environment.TickCount);
        }

        // throw as soon as the tray is free rather than waiting for Space (headless play, a probe)
        public bool AutoThrow { get; set; }

        public Func<bool> Skip { get; set; } = () => false;

        // main thread: the dice the rules are waiting on, or null
        public IReadOnlyList<Die> Waiting => _pending?.Dice;

        public bool Thrown => _pending?.Thrown ?? false;

        // main thread: something is waiting on the player's throw
        public event Action<IReadOnlyList<Die>> Asked;

        // main thread: the dice landed and were handed to the rules
        public event Action<TrayRoll> Landed;

        sealed class Pending
        {
            public IReadOnlyList<Die> Dice;
            public Action<int[]> Done;
            public bool Thrown;
        }

        Pending _pending;

        public IReadOnlyList<int> Throw(IReadOnlyList<Die> dice)
        {
            dice ??= Array.Empty<Die>();

            if (dice.Count == 0) return Array.Empty<int>();

            if (Skip() || MainQueue.OnMain) return dice.Select(d => d.Roll(_fallback)).ToList();

            int[] faces = MainQueue.Ask<int[]>(done =>
            {
                _pending = new Pending { Dice = dice, Done = done };
                Asked?.Invoke(dice);

                if (AutoThrow) Go();

                return true;
            });

            // a throw the table could not make (no tray, torn down mid-fight) is the generator's
            return faces ?? dice.Select(d => d.Roll(_fallback)).ToList().ToArray();
        }

        // main thread: the player threw (Space, or a click on the tray). False while there is
        // nothing to throw or the tray is still busy with the last one
        public bool Go()
        {
            Pending pending = _pending;

            if (pending == null || pending.Thrown) return false;

            if (_tray.IsThrowing) return false;

            _tray.Rolled += Read;

            if (!_tray.Throw(pending.Dice.ToArray()))
            {
                _tray.Rolled -= Read;
                return false;
            }

            pending.Thrown = true;
            return true;
        }

        void Read(TrayRoll roll)
        {
            _tray.Rolled -= Read;

            Pending pending = _pending;
            _pending = null;

            if (pending == null) return;

            int[] faces = roll.Felt.Select(f => f.Value).ToArray();

            // a die lost off the tray reads as its lowest face rather than hanging the rules
            if (faces.Length < pending.Dice.Count)
                faces = faces.Concat(Enumerable.Repeat(1, pending.Dice.Count - faces.Length)).ToArray();

            Landed?.Invoke(roll);
            pending.Done(faces);
        }

        // main thread: the table is going away - whatever is waiting gets digital faces
        public void Abandon()
        {
            Pending pending = _pending;
            _pending = null;

            if (pending == null) return;

            _tray.Rolled -= Read;
            pending.Done(pending.Dice.Select(d => d.Roll(_fallback)).ToArray());
        }
    }
}
