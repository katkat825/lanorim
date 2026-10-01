using System;

namespace Core.Characters
{
    // A STATBLOCK'S NAMED TRAITS (cc_task_open-questions-answers.md 2.3; Kathleen: "build if easy, defer if not").
    // SRD 5.2.1 calls them Traits; that word is taken here by a class or species feature's shape
    // (Content.Classes.Trait: Rider, Stance...), which none of these is - each is one rule the fight applies at
    // one moment, written once and named by any statblock that has it ("traits": ["pack_tactics"]).
    //
    // Checked before adding (agreement #9): a feature's boons can't say "an ally beside the target" or "while
    // bloodied" or "against spells"; a monster's tags are what it is, not what it does (and a misspelt tag would
    // do nothing silently, where a misspelt trait is refused); Instinct is how the AI fights, not a rule.
    //
    // Built: the four below. Deferred (docs/deferred.md): Sunlight Sensitivity's checks half, Spider Climb (v1 has
    // no climbing), Web Walker, the reactions (Redirect Attack), the shared per-day pools, Shape-Shift
    [Flags]
    public enum Knack
    {
        None = 0,

        // SRD 5.2.1 (wolf, dire wolf, werewolf, giant rat, kobold warrior): Advantage on an attack roll against a
        // creature if at least one of its allies is within 5 feet of the creature and the ally doesn't have the
        // Incapacitated condition
        PackTactics = 1 << 0,

        // SRD 5.2.1 (boar): while Bloodied, Advantage on melee attack rolls
        BloodiedFury = 1 << 1,

        // SRD 5.2.1 (imp): Advantage on saving throws against spells and other magical effects. v1 counts spells
        MagicResistance = 1 << 2,

        // SRD 5.2.1 (zombie): damage that reduces it to 0 Hit Points - unless Radiant or from a Critical Hit - asks a
        // Constitution save, DC 5 plus the damage taken; on a success it drops to 1 Hit Point instead
        UndeadFortitude = 1 << 3,

        // SRD 5.2.1 (kobold warrior): while in sunlight, Disadvantage on ability checks and attack rolls. v1 has no
        // light to stand in, so "in sunlight" is the fight's setting: a campaign tags a fight "sunlight". The
        // attack half is built; ability checks in a fight are not rolled, so that half has nothing to touch
        SunlightSensitivity = 1 << 4,
    }
}
