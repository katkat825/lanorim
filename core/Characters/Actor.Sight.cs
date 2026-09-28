using System;

namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- sight ------------------------------------------------------------------------------

        // SRD 5.2.1's Blinded and Invisible, as the one question both come down to. what the board
        // adds - walls, heavily obscured squares - is the fight's to ask (Encounter.Sees)
        public bool CanSee(Actor other)
        {
            if (other == null || ReferenceEquals(other, this)) return true;

            if (Has(Condition.Blinded)) return false;

            if (!other.Has(Condition.Invisible)) return true;

            return other.Boons.Exposed || Boons.Truesight;
        }

        public bool IsInvisible => Has(Condition.Invisible);

        // Disintegrate's gray dust: dead, and past anything v1 has that brings the dead back
        public bool Dust { get; private set; }

        public void TurnToDust() => Dust = true;

        // flying: difficult ground and zones on the ground don't touch it (2026-09-25)
        public bool IsFlying => Boons.FlySpeed > 0;

        // what a turn actually gets to walk: the speed, and whatever is slowing or hastening it
        public int Moves
        {
            get
            {
                if (Boons.SpeedZero) return 0;

                // a Fly Speed is its speed for as long as it flies (Fly, Gaseous Form)
                int feet = IsFlying ? Boons.FlySpeed : Math.Max(0, Speed + Boons.ExtraSpeed);

                // SRD doubling and halving; both at once is neither
                if (Boons.SpeedDoubled && !Boons.SpeedHalved) feet *= 2;
                else if (Boons.SpeedHalved && !Boons.SpeedDoubled) feet /= 2;

                return feet;
            }
        }

        // which of Dash, Disengage and Hide this actor may spend a bonus action on. empty for
        // nearly everybody; the Rogue's Cunning Action fills it
        public Manoeuvre QuickOnBonus { get; set; }
    }
}
