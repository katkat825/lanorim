namespace Core.Characters
{
    public sealed partial class Actor
    {
        // --- magic ----------------------------------------------------------------------------

        // AN ACTOR NO LONGER HOLDS A SPELL RESOURCE. It used to carry one mana pool, because there
        // was only ever one way to pay; the resource is now the player's choice between slots and
        // points, so it lives on the Caster beside the spells it pays for (Core.Magic.Caster).
        // A goblin has neither and carries neither.

        // binary: held until you end it or you go down. no CON save on damage
        // (decisions_checklist.md section 6).
        public string Concentrating { get; private set; }

        public bool IsConcentrating => !string.IsNullOrEmpty(Concentrating);

        public string Concentrate(string spellId)
        {
            string dropped = Concentrating;
            Concentrating = spellId;
            return dropped;
        }

        public string EndConcentration()
        {
            string dropped = Concentrating;
            Concentrating = null;
            return dropped;
        }
    }
}
