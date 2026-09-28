using System;
using System.Collections.Generic;
using Core.Dice;
using Core.Words;

namespace Core.Characters
{
    // WHAT A BOON IS, with no owner and nothing rolled: Bless's +1d4 on attacks and saves, Rage's
    // resistances and Strength advantage, a ring's +1 to armor class and saves. spells, class
    // stances and items all describe their boons with this one record and one reader
    // (content/Schema/BoonSpecReader.cs), where there used to be three vocabularies and a Boon
    // copied field by field from each (cc_task_dedupe-effects.md, Leads 1 and 4).
    //
    // a Boon is made from one of these (Boon.Of) and adds only what happens when it is put on: whose
    // it is, what the caster chose, and what was rolled.
    public sealed record BoonSpec
    {
        public static readonly BoonSpec Nothing = new BoonSpec();

        public Duration Duration { get; init; } = Duration.Encounter;

        // a flat number: Shield's +5 armor class, Slow's -2, Rage's +2 damage. on armor_class it
        // is armor class
        public int Flat { get; init; }

        // a rolled number, rolled fresh each time it applies: Bless's 1d4
        public DiceRoll Dice { get; init; }

        // which rolls the number reaches. declared, never inferred
        public Sways Touches { get; init; }

        // narrows the checks it touches or leans on to one skill: Pass without Trace's Stealth
        public Skill Skill { get; init; }

        // the skills the caster may pick one of at the cast: Guidance's any skill. a choice left
        // to the caster is a list of what may be chosen; the cast fills Skill
        public IReadOnlyList<Skill> SkillChoices { get; init; } = Array.Empty<Skill>();

        // narrows it to rolls made with one ability: saves (its number and its leans), its check
        // leans, and its attack advantage and damage - Slow's Dexterity saves, Enlarge's Strength,
        // Rage's Strength attacks
        public Ability? Ability { get; init; }

        // the abilities the caster may pick one of at the cast: Hex's any, Enhance Ability's five
        public IReadOnlyList<Ability> AbilityChoices { get; init; } = Array.Empty<Ability>();

        // advantage or disadvantage, as opposed to a number
        public Leans Leans { get; init; }

        // narrows a save lean to saves against one condition: Fey Ancestry's Charmed. None is every save
        public Condition Against { get; init; }

        // it holds only while the bearer can act: Danger Sense (SRD's "unless you have the
        // Incapacitated condition")
        public bool UnlessIncapacitated { get; init; }

        // spent on the first roll it touches: Guiding Bolt's "the next attack roll against it",
        // Vicious Mockery's "its next attack roll"
        public bool Once { get; init; }

        // extra damage whenever the boon's owner hits the bearer: Hex, Hunter's Mark. null is none
        public Mark Mark { get; init; }

        // an unarmored armor class to use instead of 10: Mage Armor's 13 + Dex. only ever read
        // when no armor is worn, and only if it beats what the actor already has
        public int UnarmoredBase { get; init; }

        // what damage of each type does to the bearer while it lasts: Stoneskin's and Rage's
        // resistances, a dwarf's poison resistance for good, a statblock's immunities. one record
        // where a boon had a list of resistances and an actor had its own table
        // (cc_task_dedupe-leftovers.md #11)
        public IReadOnlyDictionary<DamageType, Defense> Defenses { get; init; } =
            new Dictionary<DamageType, Defense>();

        // conditions it can't have while this lasts: Gaseous Form's Prone
        public IReadOnlyList<Condition> ImmuneTo { get; init; } = Array.Empty<Condition>();

        // sees the Invisible (and, on a board, through magical darkness): True Seeing
        public bool Truesight { get; init; }

        // outlined: whatever would make attacks against it harder does not - Faerie Fire's "can't
        // benefit from the Invisible condition"
        public bool Exposed { get; init; }

        // its advantage-against counts only for an attacker that can see the bearer: Faerie Fire
        public bool IfSeen { get; init; }

        // its disadvantage-against doesn't hold for an attacker with Truesight: Blur
        public bool NotVsTruesight { get; init; }

        // a named spell's damage is turned away while it lasts: Shield and Magic Missile
        public string WardsSpell { get; init; } = "";

        // extra dice on weapon attacks only (a spell attack is not a weapon): Enlarge's +1d4. Less
        // takes them off instead (Reduce), never below 1 damage
        public SignedDice WeaponDice { get; init; }

        // what the bearer may not do: Slow's reactions, Gaseous Form's attacks and casting
        public Forbid Forbids { get; init; }

        // on its turns, an action or a bonus action, not both; one attack when it attacks: Slow
        public bool ActionOrBonus { get; init; }

        // one extra action each turn that only buys a single weapon attack, Dash, Disengage or
        // Hide: Haste
        public bool LimitedAction { get; init; }

        // feet added to walking speed, or taken off: Ray of Frost's -10. 'speed_change' in the
        // data, a number; Feature.ExtraSpeed is the same idea on a feature (Fast Movement)
        public int ExtraSpeed { get; init; }

        // speed doubled, halved or made 0: Haste, Slow, Hypnotic Pattern
        public SpeedChange SpeedChange { get; init; }

        // a Fly Speed in feet, which is the bearer's speed while it lasts - Fly's 60, Gaseous
        // Form's 10. a flyer ignores difficult ground and zones on the ground. v1 has no altitude
        public int FlySpeed { get; init; }

        // the first time the bearer would drop to 0 hit points it drops to 1 instead, and an
        // instant kill that deals no damage is negated; either uses it up. Death Ward
        public bool DeathWard { get; init; }

        // a penalty that eases by this much each long rest instead of ending: Raise Dead's -4
        public int EasesPerLongRest { get; init; }

        // illusory duplicates that may take a hit instead of the bearer: Mirror Image
        public int Decoys { get; init; }

        // size categories up or down while it lasts: Enlarge, Reduce
        public int SizeStep { get; init; }

        // an attack-and-damage rewrite for the named weapons: Shillelagh's club or quarterstaff
        public WeaponRewrite Rewrite { get; init; }

        // the next hit on the bearer does half damage, and the boon is spent: Uncanny Dodge
        public bool HalvesNextHit { get; init; }

        public bool Attacks => (Touches & Sways.Attacks) != 0;

        public bool Saves => (Touches & Sways.Saves) != 0;

        public bool Checks => (Touches & Sways.Checks) != 0;

        public bool Damage => (Touches & Sways.Damage) != 0;

        // a flat number on armor_class is armor class
        public int ArmorClass => (Touches & Sways.ArmorClass) != 0 ? Flat : 0;

        public bool Has(Leans lean) => (Leans & lean) != 0;

        public bool Has(Forbid ban) => (Forbids & ban) != 0;

        // whether it says anything at all - a boon of nothing is a data error
        public bool DoesSomething =>
            Flat != 0 || !Dice.IsNothing || Leans != Leans.None || Mark != null || UnarmoredBase > 0 ||
            Defenses.Count > 0 || ImmuneTo.Count > 0 || Truesight || Exposed || WardsSpell.Length > 0 ||
            !WeaponDice.IsNothing || Forbids != Forbid.None || ActionOrBonus || LimitedAction ||
            ExtraSpeed != 0 || SpeedChange != SpeedChange.None || FlySpeed > 0 || DeathWard ||
            Decoys > 0 || SizeStep != 0 || Rewrite != null || HalvesNextHit;

        public override string ToString() =>
            (Flat != 0 ? $"{Flat:+0;-0}" : "") + (Dice.IsNothing ? "" : $" +{Dice}") +
            (Touches != Sways.None ? $" on {Touches}" : "") +
            (Leans != Leans.None ? $" {Leans}" : "") + $" ({EnumWords.Name(Duration)})";
    }
}
