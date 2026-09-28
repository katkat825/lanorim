using System;
using Core.Localization;
using Core.Space;

namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- what it holds -----------------------------------------------------------------------

        // it dropped what it was holding (Command's Drop, Fear) or had it pulled away
        // (Telekinesis): an attack with a held weapon is not there until it is picked up again.
        // natural weapons - a claw, a bite - cannot be dropped
        public bool Disarmed { get; private set; }

        // where the dropped weapon lies; picking it up is the free object interaction SRD gives a
        // turn, from its square or beside it
        public Cell? DroppedAt { get; private set; }

        public void Disarm(Cell? at)
        {
            Disarmed = true;
            DroppedAt = at;
        }

        public bool Rearm()
        {
            if (!Disarmed) return false;

            Disarmed = false;
            DroppedAt = null;
            return true;
        }

        // whether this attack is one it can make now
        public bool CanUse(Attack attack) =>
            attack != null && !(Disarmed && attack.Hand != Hand.None);

        // the size it is right now: Enlarge makes it one bigger, Reduce one smaller
        public Size CurrentSize =>
            (Size)Math.Clamp((int)Size + Boons.SizeStep, (int)Size.Tiny, (int)Size.Gargantuan);

        public int ProficiencyBonus => Proficiency.Bonus(Level);

        public string NameKey => KeyConventions.ActorName(Id);

        public void SetLevel(int level) => Level = Proficiency.Clamp(level);

        public void SetHealth(Health health)
        {
            Health = health ?? throw new ArgumentNullException(nameof(health));
            Health.RaisedBy(() => Boons.MaxHitPoints);
        }
    }
}
