using System.Collections.Generic;
using Core.Combat;

namespace Core.Magic
{
    // ZONE: an area that persists and does something to whoever is in it - Web, Spirit Guardians,
    // Wall of Fire. Incantation puts it on the fight (MakeZone); the effects that reach it act when
    // its pulses come round. landing on a creature, the zone itself only reports
    public sealed class ZoneHandler : IPrimitiveHandler
    {
        public Primitive Kind => Primitive.Zone;

        public bool MakesAZone => true;

        public IReadOnlyList<string> Keys { get; } = new[]
        {
            "rough", "ground", "each_time", "cover", "encloses", "ring_size", "beside", "drifts",
            "obscures", "blocks_spells_up_to", "on_caster", "unoccupied",
        };

        public IEnumerable<string> Check(SpellEffect effect, int spellLevel)
        {
            if (effect.AimKind == AimKind.Square && effect.Length <= 0)
                yield return "a square zone needs a 'length'";

            if (effect.Affects == Affects.NotCaster)
                yield return "a zone spares its caster's side ('foes') or keeps to it ('allies'); " +
                             "'not_caster' is for an area";
        }

        public Landing Apply(Contact c) => c.Report(c.Landed);
    }
}
