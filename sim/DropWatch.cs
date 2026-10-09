using Core.Characters;
using Core.Combat;

namespace Sim
{
    // DID THE HERO FALL? Told by the fight's own Downed, which comes after whatever keeps a creature up (Relentless
    // Endurance, the keepsake) has had its say - so a drop the keepsake caught is not a fall. MageBalance's watch
    // builds on it; Classes and Versus use it as it is
    public class DropWatch : CombatObserver
    {
        readonly Actor _hero;

        public DropWatch(Actor hero) => _hero = hero;

        public bool Dropped { get; private set; }

        public override void Downed(Actor actor)
        {
            if (ReferenceEquals(actor, _hero) && _hero.IsDown) Dropped = true;
        }
    }
}
