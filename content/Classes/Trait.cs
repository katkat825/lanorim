using Core.Words;

namespace Content.Classes
{
    // what a class or species feature can be. a closed list, exactly like the spell primitives and
    // for the same reason: features are data, and data that can say anything is code.
    // decisions_checklist.md section 6 calls class features the second-biggest cost; this is the
    // cheap path - implement the shapes, not the features.
    [Fallback(Trait.Narrate)]
    public enum Trait
    {
        // extra damage or a condition on a hit. Sneak Attack, Divine Smite, Improved Critical
        Rider,

        // a boon on yourself you switch on. Rage, a Paladin's aura, Reckless Attack
        Stance,

        // hit points back, a limited number of times. Second Wind, Lay on Hands
        Recovery,

        // an extra action or reaction, each round or once per rest. Extra Attack is one every
        // round, Action Surge one per rest
        ActionGrant,

        // armor class from an ability instead of armor. the Barbarian's Constitution
        UnarmoredDefense,

        // double proficiency on a number of skills. the Rogue's
        Expertise,

        // makes the character a caster: the ability, the progression, the spell list
        Spellcasting,

        // more squares per turn. the Wood Elf's
        Speed,

        // a trained skill, a trained save, or a permanent flat bonus
        Training,

        // at 0 hit points, stay up instead. the Orc's Relentless Endurance, once per rest
        DeathIntercept,

        // a curated shape to turn into. the Druid's Wild Shape, as form cards
        Shape,

        // authored: the campaign decides what it means. Trance, Stonecunning's secrets
        Narrate,

        // a failed saving throw rerolled, with a bonus, so many times a long rest: Indomitable
        Reroll,

        // a bonus action for a Dash and temporary hit points equal to the proficiency bonus, so
        // many times a rest: the Orc's Adrenaline Rush
        Boost,

        // lets a bonus action be spent on Dash, Disengage or Hide. the Rogue's Cunning Action -
        // what the bonus action can do, not how many there are
        Nimble,
    }
}
