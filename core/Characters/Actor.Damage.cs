using System.Collections.Generic;
using System.Linq;
using Core.Words;

namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- damage ---------------------------------------------------------------------------

        // what every boon on it says about this damage type - its nature, its features, the spells
        // on it - read the SRD's way: immunity wins; resistance and vulnerability cancel; a second
        // resistance adds nothing. Petrified is resistance to everything
        public Defense DefenseAgainst(DamageType type)
        {
            var said = Boons.DefensesAgainst(type).ToList();

            if (said.Contains(Defense.Immune)) return Defense.Immune;

            bool resistant = said.Contains(Defense.Resistant) || Has(Condition.Petrified);
            bool vulnerable = said.Contains(Defense.Vulnerable);

            return resistant && vulnerable ? Defense.Normal
                 : resistant ? Defense.Resistant
                 : vulnerable ? Defense.Vulnerable
                 : Defense.Normal;
        }

        // what the creature is by nature - a statblock's resistance - is a boon that never ends.
        // setting it again replaces it
        public void SetDefense(DamageType type, Defense defense)
        {
            if (type == DamageType.None) return;

            string id = "nature." + type.Id();

            Boons.EndId(id);

            if (defense != Defense.Normal)
                Boons.Add(Boon.Of(new BoonSpec
                {
                    Duration = Duration.Permanent,
                    Defenses = new Dictionary<DamageType, Defense> { [type] = defense },
                }, id, Nature));
        }

        // the single path damage takes into an actor: the multiplier, then the hit points, then
        // the unconscious condition. nothing else may call Health.Take on its own.
        public int Suffer(int amount, DamageType type)
        {
            int after = DefenseAgainst(type).Apply(amount);

            // Death Ward: the first drop to 0 is a drop to 1, and the ward is spent
            Boon ward = Boons.DeathWard;

            if (ward != null && after > 0 && after >= Health.Current + Health.Temporary &&
                Health.Current > 0)
            {
                Boons.Remove(ward);

                int kept = Health.Current - 1;
                int soaked = Health.Temporary;

                Health.Take(soaked + kept);
                Stable = false;

                return kept;
            }

            int taken = Health.Take(after);

            // SRD 5.2.1: a Stable creature that takes damage is no longer Stable
            if (after > 0) Stable = false;

            if (Health.IsDown) Apply(Condition.Unconscious);

            return taken;
        }

        // DROPPED TO 0 AND STAYED UP INSTEAD: the Orc's Relentless Endurance, the Barbarian's Relentless Rage, the
        // zombie's Undead Fortitude - each "drops to 1 Hit Point instead". It never fell, so the Unconscious goes and
        // so does the Prone that came with it (before 2026-10-03 a hero who stayed up was left lying down)
        public void StaysUp(int hitPoints)
        {
            Health.Revive(System.Math.Max(1, hitPoints));
            Remove(Condition.Unconscious);

            if (_proneFromFalling) Remove(Condition.Prone);
        }

        // SRD 5.2.1 Stable: at 0 hit points but no longer rolling death saves. Spare the Dying.
        // taking damage ends it; healing above 0 makes it moot
        public bool Stable { get; private set; }

        public bool Stabilize()
        {
            if (!IsDown || IsDead || Stable) return false;

            Stable = true;
            return true;
        }

        public int Mend(int amount)
        {
            // nothing heals the dead; a raise is a separate thing and it clears the flag itself
            if (IsDead) return 0;

            bool wasDown = Health.IsDown;
            int healed = Health.Heal(amount);

            // healing wakes the dying, not the sleeping - a Sleep spell's Unconscious is not the
            // 0-hit-points one and healing does not end it
            if (wasDown && !Health.IsDown) Remove(Condition.Unconscious);

            return healed;
        }
    }
}
