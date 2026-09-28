using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Game.Audio;
using Game.Dice;
using Godot;

namespace Game.Tray
{
    public partial class DiceTray
    {
        // --- throwing -------------------------------------------------------------------------

        // THE ONE WAY IN. Hand it the dice the rules want thrown, in the order the rules will read
        // them, and the answer comes back through Rolled.
        //
        //     tray.Throw(Die.D20);                       a check, a save, an attack
        //     tray.Throw(Die.D20, Die.D20);              advantage - both are thrown and shown
        //     tray.Throw(Die.D8, Die.D8, Die.D8);        damage
        public bool Throw(params Die[] dice)
        {
            if (dice == null || dice.Length == 0)
            {
                GD.PushError("dice tray: asked to throw nothing");
                return false;
            }

            foreach (Die die in dice)
            {
                if (die.IsReal() && die != Die.D100) continue;

                GD.PushError($"dice tray: {die.Label()} is not a solid the tray can throw " +
                             "(a d100 is two d10s)");
                return false;
            }

            if (IsThrowing)
            {
                GD.PushError("dice tray: asked to throw while a throw is still in the air");
                return false;
            }

            // MORE DICE THAN THE TRAY SEATS IS NOT AN ERROR, IT IS A BIG SPELL. Meteor Swarm is
            // 20d6 and no tray holds twenty dice comfortably, so the tray does what a person does:
            // throws a handful, writes the numbers down, and throws the rest. The faces come back
            // in one TrayRoll in the order they were asked for, so nothing downstream can tell.
            _queued.Clear();
            _read.Clear();

            for (int i = _dice.Count; i < dice.Length; i += _dice.Count)
                _queued.Add(dice.Skip(i).Take(_dice.Count).ToArray());

            Handful(dice.Take(_dice.Count).ToArray());

            return true;
        }

        // the handfuls still to throw, and the faces read from the ones already thrown
        readonly List<Die[]> _queued = new();
        readonly List<Felt> _read = new();

        void Handful(Die[] dice)
        {
            Seat(dice.Length);

            for (int i = 0; i < _active.Count; i++) _active[i].Size = dice[i];

            ThrowAll();
        }

        public bool Throw(DiceRoll roll) =>
            roll.RollsAnything && Throw(Enumerable.Repeat(roll.Die, roll.Count).ToArray());

        // how many dice play; the rest are benched - frozen, hidden and off the collision layers -
        // so a stale last face can never be read as part of a throw nobody made
        void Seat(int count)
        {
            count = Mathf.Clamp(count, 0, _dice.Count);

            _active.Clear();

            for (int i = 0; i < _dice.Count; i++)
            {
                DieBody die = _dice[i];

                if (i < count)
                {
                    _active.Add(die);
                    Unbench(die);
                }
                else
                {
                    Bench(die);
                }
            }
        }

        void Bench(DieBody die)
        {
            if (_benched.ContainsKey(die)) return;

            _benched[die] = (die.CollisionLayer, die.CollisionMask);

            die.CollisionLayer = 0;
            die.CollisionMask = 0;
            die.Freeze = true;
            die.Visible = false;
        }

        void Unbench(DieBody die)
        {
            if (!_benched.TryGetValue(die, out (uint Layer, uint Mask) was)) return;

            die.CollisionLayer = was.Layer;
            die.CollisionMask = was.Mask;
            die.Freeze = false;
            die.Visible = true;

            _benched.Remove(die);
        }

        void ThrowAll()
        {
            _awaitingSettle = true;
            _pending.Clear();

            // captioned once a throw, not once a bounce: a die strikes a wooden tray a dozen times
            // between the hand and the felt, and what a deaf player needs to know is that the dice
            // went up
            Captions?.Says(Sound.Dice);

            for (int i = 0; i < _active.Count; i++)
            {
                Transform3D from = _throwPoints[i].GlobalTransform;

                _voices[i]?.Rattle(from.Origin);

                _pending.Add((_active[i], from, RattleLeadTicks + i * LaunchStaggerTicks));
            }
        }

        // set by the table so a throw is captioned; null when nobody is listening
        public Game.Access.Captioned Captions { get; set; }

        public override void _PhysicsProcess(double delta)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                (DieBody die, Transform3D from, int delay) = _pending[i];

                if (delay > 0)
                {
                    _pending[i] = (die, from, delay - 1);
                    continue;
                }

                die.Throw(from);
                _pending.RemoveAt(i);
            }
        }

        public override void _Process(double delta)
        {
            // belt and braces: a die knocked loose by another's landing moves without re-arming its
            // own flight tracking, so no Settled signal is coming for it
            if (_awaitingSettle) TryRead();
        }

        // waits on IsAtRest as well as IsSettled, so a die bumped after settling is not read
        // mid-tumble
        void TryRead()
        {
            if (!_awaitingSettle || _pending.Count > 0) return;

            // a loop, not All(): this runs every frame a throw is out, and All boxes an enumerator
            for (int i = 0; i < _active.Count; i++)
                if (!_active[i].IsSettled || !_active[i].IsAtRest) return;

            _awaitingSettle = false;

            // read the faces live rather than each die's latched value, so a die nudged flat after
            // settling is read as it is now
            var felt = _active.Select(d =>
            {
                (int value, float alignment) = d.ReadFace();

                return new Felt(d.Size, value, alignment);
            }).ToList();

            // developer diagnostic, exempt from localization
            GD.Print("tray    " + string.Join(", ", _active.Select(
                (d, i) => $"{d.Name} {d.Size.Label()} {felt[i].Value} ({felt[i].Alignment:0.00})")));

            _read.AddRange(felt);

            // another handful to go: the answer is not owed until the last of them is still
            if (_queued.Count > 0)
            {
                Die[] next = _queued[0];
                _queued.RemoveAt(0);

                Handful(next);
                return;
            }

            Last = new TrayRoll(_read);

            Rolled?.Invoke(Last);
        }
    }
}
