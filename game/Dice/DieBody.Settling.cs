using Godot;

namespace Game.Dice
{
    public partial class DieBody
    {
        public override void _PhysicsProcess(double delta)
        {
            // before the in-flight check: a settled die knocked loose is moving too, and the tray waits on IsAtRest
            WatchKnockedLoose(delta);

            if (!_inFlight) return;

            _flightTime += delta;

            // an escaped die never comes to rest, so without this the tray waits forever for a Settled that never arrives
            if (HasLeftTray())
            {
                _inFlight = false;
                _lostRethrows++;

                DieRecoveryStep step = Recovery.Escaped(new EscapedDie(_lostRethrows, _flightTime));

                if (step.Action == DieRecoveryAction.Rethrow)
                {
                    GD.Print($"{Name}: left the tray after {_flightTime:0.00}s - rethrow {_lostRethrows} at {step.Energy:0.00} energy");
                    EmitSignal(SignalName.LeftTray, _lostRethrows);
                    Launch(_lastThrowFrom, step.Energy);
                    return;
                }

                // a nudge cannot help a die off the table, so anything but a re-throw ends it here
                GD.Print($"{Name}: STILL outside the tray after {_lostRethrows} attempts - forcing a settle");
                GiveUp();
                return;
            }

            // the ceiling, or a die seen wobbling (DieBody.Wobble.cs) before it: no use waiting out the rest
            if (_flightTime > (_restless > 0 ? AfterNudgeSeconds : MaxFlightSeconds) || _restless == 0 && Wobbling)
            {
                _inFlight = false;

                // knocked loose, it was never thrown this time: there is no throw to repeat, only a place to leave it
                if (_knocked)
                {
                    GD.Print($"{Name}: knocked loose and still moving after {_flightTime:0.00}s - forcing a settle");
                    GiveUp();
                    return;
                }

                _restless++;

                DieRecoveryStep step = Recovery.Restless(new RestlessDie(_restless, _flightTime));

                if (step.Action == DieRecoveryAction.Nudge)
                {
                    GD.Print($"{Name}: still moving after {_flightTime:0.00}s - nudge {_restless}");
                    Nudge();
                    return;
                }

                if (step.Action == DieRecoveryAction.Rethrow)
                {
                    GD.Print($"{Name}: still moving after {_flightTime:0.00}s - rethrow {_restless} at {step.Energy:0.00} energy");
                    Launch(_lastThrowFrom, step.Energy);
                    return;
                }

                GD.Print($"{Name}: STILL moving after {_restless - 1} nudges - reading it as it lies");
                GiveUp();
                return;
            }

            // ignore the first moments: the kick may land a frame after this runs
            if (_flightTime < 0.1) return;

            bool still = LinearVelocity.Length() < RestLinearSpeed
                      && AngularVelocity.Length() < RestAngularSpeed;
            _stillTime = still ? _stillTime + delta : 0;
            WatchWobble(delta, still);

            if (!Sleeping && _stillTime < RestHoldSeconds) return;

            _inFlight = false;

            (int value, float alignment) = ReadFace();
            float required = RequiredAlignment;

            // every GD.Print below is developer diagnostic, never player-facing - not localized

            if (alignment < required)
            {
                DieRecoveryStep step = Recovery.Cocked(
                    new CockedDie(value, alignment, required, _nudges, _cockedRethrows));

                if (step.Action == DieRecoveryAction.Nudge)
                {
                    _nudges++;
                    if (LogSettles)
                        GD.Print($"{Name}: cocked at {alignment:0.000} (needs {required:0.000}) - nudge {_nudges}");
                    EmitSignal(SignalName.Nudged, alignment, _nudges);
                    Nudge();
                    return;
                }

                if (step.Action == DieRecoveryAction.Rethrow)
                {
                    _cockedRethrows++;
                    if (LogSettles)
                        GD.Print($"{Name}: still cocked at {alignment:0.000} after {_nudges} nudges - rethrow {_cockedRethrows}");
                    EmitSignal(SignalName.Cocked, alignment, _cockedRethrows);
                    Launch(_lastThrowFrom, step.Energy);
                    return;
                }

                // the nearest face is a guess, not a reading, but looping silently is worse, so say it loudly
                GD.Print($"{Name}: STILL cocked at {alignment:0.000} (needs {required:0.000}) - taking {value} anyway");
            }

            SettledValue = value;

            if (LogSettles)
                GD.Print($"{Name}: settled after {_flightTime:0.00}s showing {value} (alignment {alignment:0.000}), " +
                         $"centre {(TraySpace?.ToLocal(GlobalPosition) ?? GlobalPosition).Y * 1000f:0} mm up{(Sleeping ? " (asleep)" : "")}");

            EmitSignal(SignalName.Settled, value);
        }

        // a settled die bumped by another's landing moves without a throw to watch it, so no Settled was coming for
        // it and the tray, which waits on IsAtRest, could wait for good. moving a moment, it is watched as a throw again
        void WatchKnockedLoose(double delta)
        {
            if (_inFlight || Freeze || Sleeping || IsAtRest)
            {
                _looseTime = 0;
                return;
            }

            _looseTime += delta;

            if (_looseTime < KnockedLooseSeconds) return;

            _looseTime = 0;
            _knocked = true;
            _inFlight = true;
            _flightTime = 0;
            _stillTime = 0;
            SettledValue = 0;
            ForgetWobble();
        }

        // the end that always terminates: frozen where it lies, so it is at rest for the tray as well as settled, and
        // read as it lies. developer diagnostics above say it loudly; frequent means the tray or the throw needs a look
        void GiveUp()
        {
            Freeze = true;
            SettledValue = ReadFace().Value;
            EmitSignal(SignalName.Settled, SettledValue);
        }

        // alignment is a dot product: 1.0 flat, below RequiredAlignment is cocked
        // not always the top face: a d4 has a corner up and is read from the face on the felt
        public (int Value, float Alignment) ReadFace() => _faces.Read(GlobalBasis);

        // asked in the tray's own space, so moving the tray moves what 'out' means with it
        bool HasLeftTray()
        {
            Vector3 p = TraySpace?.ToLocal(GlobalPosition) ?? GlobalPosition;

            return p.Y < LostBelowY
                || new Vector2(p.X, p.Z).Length() > LostRadius;
        }

        // A POINT IN A CUBE, NORMALIZED, IS NOT A RANDOM DIRECTION. The density landing on a
        // patch of the sphere goes as the cube of the distance to the box wall in that direction,
        // so the eight corners at sqrt(3) get (sqrt 3)^3, about five times, the directions the six
        // face centres at 1 do. The fix is to throw away everything outside the unit ball before
        // normalizing, which costs a second draw about half the time (the ball is pi/6 of the box)
        // and nothing else.
        //
        // THIS WENT UNSEEN FOR AS LONG AS THE GAME ONLY ROLLED A d6. A corner-biased spin axis
        // lines up with a cube's own symmetry and most of it cancels; on an icosahedron nothing
        // cancels, and a 4000-throw sweep of the d20 came out BIASED past the 0.1% line with the
        // solid and its numbering both provably correct. The die was fine. The throw was not.
        // A UNIFORM AXIS AND A UNIFORM ANGLE IS NOT A UNIFORM ROTATION. Turning by a uniform angle
        // in 0..2pi about a uniform axis piles orientations up near the identity, because on a
        // genuinely uniform rotation the angle's density goes as (1 - cos theta) - small turns are
        // rare and this made them common. A die that does not tumble enough to forget where it
        // started then carries that into where it stops.
        //
        // Shoemake's method instead: three uniform numbers into a quaternion, which IS uniform on
        // the rotations. Four lines, no rejection, and nothing to tune.
        Quaternion RandomTurn()
        {
            float u1 = _rng.Randf();
            float u2 = _rng.Randf() * Mathf.Tau;
            float u3 = _rng.Randf() * Mathf.Tau;

            float a = Mathf.Sqrt(1f - u1);
            float b = Mathf.Sqrt(u1);

            return new Quaternion(a * Mathf.Sin(u2), a * Mathf.Cos(u2),
                                  b * Mathf.Sin(u3), b * Mathf.Cos(u3)).Normalized();
        }

        Vector3 RandomAxis()
        {
            Vector3 v;
            float length;

            do
            {
                v = new Vector3(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f),
                                _rng.RandfRange(-1f, 1f));

                length = v.LengthSquared();
            }
            while (length > 1f || length < 0.001f);

            return v.Normalized();
        }
    }
}
