using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Magic;
using Core.Dice;
using Core.Localization;
using Core.Rules;
using Core.Words;

namespace Content.Classes
{
    public sealed class Feature
    {
        public Feature(string id, Trait trait, int level = 1,
                       DiceRoll amount = default, DiceRoll perLevel = default,
                       DamageType damageType = DamageType.None,
                       Condition condition = Condition.None,
                       Ability? ability = null,
                       When when = When.Always, Core.Combat.Spend grants = Core.Combat.Spend.Action,
                       int uses = 0, int count = 0, int perLevels = 1,
                       Duration duration = Duration.Encounter,
                       IReadOnlyList<Skill> skills = null,
                       IReadOnlyList<Ability> saves = null,
                       CasterProgression progression = CasterProgression.None,
                       string note = null,
                       Manoeuvre manoeuvres = Manoeuvre.None)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Trait = trait;
            Level = Math.Clamp(level, 1, 20);
            Amount = amount;
            PerLevel = perLevel;
            DamageType = damageType;
            Condition = condition;
            Ability = ability;
            When = when;
            Grants = grants;
            Uses = uses;
            Count = count;
            PerLevels = Math.Max(1, perLevels);
            Duration = duration;
            Skills = skills ?? Array.Empty<Skill>();
            Saves = saves ?? Array.Empty<Ability>();
            Progression = progression;
            Note = note ?? "";
            Manoeuvres = manoeuvres;
        }

        public string Id { get; }

        // what a feature's boons are stamped with: "feature:rage" can't be a spell's id, so ending
        // a spell never takes them off
        public string Source => "feature:" + Id;

        // what owning it makes the creature, as far as a spell can tell: an elf's Trance is
        // "sleepless" (magic can't put it to sleep). the engine also reads a few as rules:
        // evasion, reliable_talent, potent_cantrip, empowered_evocation, disciple_of_life,
        // blessed_healer, supreme_healing, indomitable_might
        public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

        // --- 2026-09-25, the SRD check ----------------------------------------------------------------

        // a rider that fires once on each of your turns, on a hit: Sneak Attack, Divine Strike
        public bool OncePerTurn { get; init; }

        // a rider or an intercept that works only while these stances are up: Frenzy while
        // raging and reckless, Relentless Rage while raging
        public IReadOnlyList<string> WhileStances { get; init; } = Array.Empty<string>();

        // the weapons a rider rides on: "finesse_or_ranged" (Sneak Attack), "melee" (Radiant
        // Strikes). empty is any
        public string NeedsWeapon { get; init; } = "";

        // a rider that gives up this stance's advantage on the attack it rides: Brutal Strike
        // forgoes Reckless Attack's
        public string Forgoes { get; init; } = "";

        // uses by level, where the SRD's table grows: Rage 2, 3, 4, 5, 6
        public IReadOnlyDictionary<int, int> UsesByLevel { get; init; } = new Dictionary<int, int>();

        public Recharge Recharge { get; init; } = Recharge.Short;

        // the pool a use comes out of: Sacred Weapon and Preserve Life spend Channel Divinity
        public string Spends { get; init; } = "";

        // a critical hit on this natural roll or higher: Improved Critical's 19
        public int CritOn { get; init; }

        // Aura of Protection: saves add this ability's modifier, at least +1
        public Ability? AuraAbility { get; init; }

        // the Defense fighting style's armor class, while wearing armor
        public int ArmoredArmorClass { get; init; }

        // a stance's flat bonus by level, where it doesn't grow evenly: Rage's +2, +3, +4
        public IReadOnlyDictionary<int, int> FlatByLevel { get; init; } = new Dictionary<int, int>();

        // an amount by level, where it doesn't grow evenly: Frenzy's 2d6, 3d6, 4d6
        public IReadOnlyDictionary<int, DiceRoll> AmountByLevel { get; init; } = new Dictionary<int, DiceRoll>();

        // a stance's flat bonus is this ability's modifier, at least +1: Sacred Weapon's Charisma
        public Ability? FlatAbility { get; init; }

        // THE BOONS IT GIVES, in the same words a spell's boon and an item's use (read by
        // content/Schema/BoonSpecReader.cs). a stance's one boon goes on when it is switched on:
        // Rage's resistances, Strength advantage and +2 damage. any other feature's are the
        // creature's for good: Danger Sense's advantage on Dexterity saves, a dwarf's poison
        // resistance, Aura of Courage's immunity (cc_task_dedupe-leftovers.md Part A and #11)
        public IReadOnlyList<BoonSpec> Boons { get; init; } = Array.Empty<BoonSpec>();

        // feet added to walking speed: Fast Movement's 10
        public int ExtraSpeed { get; init; }

        // the hit points a death intercept stays up with: Relentless Endurance's 1, Relentless
        // Rage's twice the level
        public StaysUpAt StaysUpAt { get; init; }

        // what switching it on costs: a bonus action (Rage, Second Wind), an action (Preserve
        // Life) or nothing (Reckless Attack, Sacred Weapon)
        public Core.Combat.Spend UseTime { get; init; } = Core.Combat.Spend.Bonus;

        // a reaction it gives: "halve" is Uncanny Dodge
        public string Reaction { get; init; } = "";

        // spells always prepared, by the level they come at: a Life Domain's, Paladin's Smite's.
        // names the SRD gives that v1 doesn't build are left out (they are reference cards)
        public IReadOnlyDictionary<int, IReadOnlyList<string>> Spells { get; init; } =
            new Dictionary<int, IReadOnlyList<string>>();

        // casts each long rest that cost no slot: Paladin's Smite's one Divine Smite
        public IReadOnlyDictionary<string, int> FreeCasts { get; init; } = new Dictionary<string, int>();

        // cantrips and prepared spells by level, on a spellcasting feature (SRD's class tables)
        public IReadOnlyDictionary<int, int> CantripsByLevel { get; init; } = new Dictionary<int, int>();

        public IReadOnlyDictionary<int, int> KnownByLevel { get; init; } = new Dictionary<int, int>();

        // one more skill to pick from the class list: Primal Knowledge
        public int SkillPicks { get; init; }

        // the skills an Expertise may choose from: Scholar's six
        public IReadOnlyList<Skill> ExpertiseFrom { get; init; } = Array.Empty<Skill>();

        // an intercept's save: Relentless Rage's DC 10 Constitution, 5 more each time. 0 is no save
        // (the Orc's Relentless Endurance)
        public int Dc { get; init; }

        public int DcStep { get; init; }

        // a recovery that only a Bloodied creature can take, up to half its hit points: Preserve
        // Life
        public bool OnlyBloodied { get; init; }

        public bool CapHalf { get; init; }

        // the hit point maximum rises this much each level: Dwarven Toughness
        public int MaxHitPointsPerLevel { get; init; }

        // a Speed feature that only counts out of heavy armor: Fast Movement
        public bool NotInHeavyArmor { get; init; }

        // a spell of the species' own, inline in its data (the Dragonborn's Breath Weapon), cast
        // uses_by_level times a long rest
        public Spell InnateSpell { get; init; }

        // the ability a species' spells are cast with: the highest of these three, when it names
        // them (SRD 5.2.1 lets the player choose Intelligence, Wisdom or Charisma)
        public IReadOnlyList<Ability> SpellAbilities { get; init; } = Array.Empty<Ability>();

        // the value a table holds at this level: the entry for the highest level at or below it
        public static T At<T>(IReadOnlyDictionary<int, T> table, int level, T otherwise)
        {
            T found = otherwise;
            int best = int.MinValue;

            foreach (KeyValuePair<int, T> entry in table)
                if (entry.Key <= level && entry.Key > best)
                {
                    best = entry.Key;
                    found = entry.Value;
                }

            return found;
        }

        public int UsesAt(int level) => At(UsesByLevel, level, Uses);


        public int CantripsAt(int level) => At(CantripsByLevel, level, -1);

        public int KnownAt(int level) => At(KnownByLevel, level, -1);

        public IEnumerable<string> SpellsAt(int level) =>
            Spells.Where(s => s.Key <= level).SelectMany(s => s.Value);

        // for a Nimble feature, which of Dash, Disengage and Hide the bonus action may do
        public Manoeuvre Manoeuvres { get; }

        public Trait Trait { get; }

        // the class level it arrives at. milestone levelling turns it on and never off
        public int Level { get; }

        public DiceRoll Amount { get; }

        // what each step past Level adds; Sneak Attack's ladder in two fields
        public DiceRoll PerLevel { get; }

        // how many class levels one step takes. Sneak Attack is a d6 every two levels, so the
        // interval is the difference between a rogue and a cleric's divine strike
        public int PerLevels { get; }

        public DamageType DamageType { get; }

        public Condition Condition { get; }

        public Ability? Ability { get; }

        public When When { get; }

        // which part of the turn an action grant adds: an action, a bonus action or a reaction
        public Core.Combat.Spend Grants { get; }

        // how many times between rests; 0 is unlimited
        public int Uses { get; }

        // how many skills an Expertise picks, how many forms a Shape has
        public int Count { get; }

        public Duration Duration { get; }

        public IReadOnlyList<Skill> Skills { get; }

        public IReadOnlyList<Ability> Saves { get; }

        // HOW FAST THIS CASTER CLIMBS THE SPELL LEVELS, and the only spellcasting number a class
        // data file carries now. It used to be two - mana_per_level and mana_flat - because there
        // was one pool and its size was the whole resource. A named progression serves BOTH modes:
        // the slot table reads it directly, and the point pool derives from it.
        public CasterProgression Progression { get; }

        public string Note { get; }

        public string NameKey => KeyConventions.FeatureName(Id);

        public string DescriptionKey => KeyConventions.FeatureDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;

            // a species' own spell (the Dragonborn's breath) has its own card
            if (InnateSpell != null)
                foreach (string key in InnateSpell.Keys()) yield return key;
        }

        // a feature that does something when the player asks, rather than sitting on the sheet
        public bool IsActive =>
            Trait == Trait.Stance || Trait == Trait.Recovery || Trait == Trait.Shape || Trait == Trait.Boost ||
            (Trait == Trait.Rider && When == When.WhenSpent);

        public DiceRoll AmountAt(int level)
        {
            if (AmountByLevel.Count > 0) return At(AmountByLevel, level, Amount);

            if (PerLevel.IsNothing || level <= Level) return Amount;

            int steps = (level - Level) / PerLevels;

            if (steps <= 0) return Amount;

            return new DiceRoll(Amount.Count + PerLevel.Count * steps,
                                Amount.RollsAnything ? Amount.Die : PerLevel.Die,
                                Amount.Modifier + PerLevel.Modifier * steps);
        }

        // the passive half: everything a feature does the moment it is owned
        public void Grant(Actor actor, int level)
        {
            if (actor == null || level < Level) return;

            foreach (string tag in Tags) actor.Tag(tag);

            if (CritOn > 0) actor.CritOn = Math.Min(actor.CritOn, CritOn);

            if (AuraAbility.HasValue) actor.AuraAbility = AuraAbility;

            if (ArmoredArmorClass != 0) actor.ArmoredArmorClassBonus += ArmoredArmorClass;

            // a feature's boons, for good - a stance's go on when it is switched on (BoonFor)
            if (Trait != Trait.Stance)
                for (int i = 0; i < Boons.Count; i++)
                    actor.Boons.Add(Core.Characters.Boon.Of(Boons[i] with { Duration = Duration.Permanent },
                                                            Boons.Count == 1 ? Id : $"{Id}.{i + 1}", Source));

            switch (Trait)
            {
                case Trait.UnarmoredDefense:
                    actor.UnarmoredDefense = Ability;
                    break;

                case Trait.Speed:
                    if (NotInHeavyArmor) actor.SpeedOutOfHeavyArmor += ExtraSpeed;
                    else actor.Speed += ExtraSpeed;
                    break;

                case Trait.Nimble:
                    actor.QuickOnBonus |= Manoeuvres;
                    break;

                case Trait.Training:
                    foreach (Skill skill in Skills) actor.Train(skill);
                    foreach (Ability save in Saves) actor.TrainSave(save);
                    break;

                case Trait.Expertise:
                    // which skills the Rogue doubles is the player's choice at creation; the
                    // feature only says how many, so the sheet asks and Train records it
                    foreach (Skill skill in Skills) actor.Train(skill, Training.Expert);
                    break;
            }
        }

        // the rider a hit carries, or null when this feature is not one or its condition is unmet
        public Rider RiderFor(int level, bool hadAdvantage, bool spent)
        {
            if (Trait != Trait.Rider || level < Level) return null;

            bool fires = When switch
            {
                When.Always => true,
                When.WithAdvantage => hadAdvantage,
                When.WhenSpent => spent,
                When.OnCritical => true,
                _ => false,
            };

            if (!fires) return null;

            return new Rider(Id, AmountAt(level), DamageType, Condition,
                             When == When.OnCritical);
        }

        public Boon BoonFor(int level, Actor actor = null)
        {
            if (Trait != Trait.Stance || level < Level) return null;

            if (Boons.Count == 0) return null;

            BoonSpec boon = Boons[0];

            // Sacred Weapon's Charisma modifier, at least +1; Rage's +2, +3, +4
            int flat = FlatAbility.HasValue && actor != null
                ? Math.Max(1, actor.AbilityModifier(FlatAbility.Value))
                : At(FlatByLevel, level, boon.Flat);

            return Core.Characters.Boon.Of(boon with { Duration = Duration, Flat = flat }, Id, Id);
        }

        public override string ToString() =>
            $"{Id} [{EnumWords.Name(Trait)}] level {Level}" +
            (Amount.IsNothing ? "" : $" {Amount}") +
            (PerLevel.IsNothing ? "" : $" +{PerLevel} every {PerLevels} levels") +
            (Uses > 0 ? $", {Uses} per rest" : "");
    }
}
