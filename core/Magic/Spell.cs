using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Localization;

namespace Core.Magic
{
    public enum School
    {
        Abjuration,
        Conjuration,
        Divination,
        Enchantment,
        Evocation,
        Illusion,
        Necromancy,
        Transmutation,
    }

    // one primitive with its numbers filled in. a spell is a list of these, and that list is the
    // whole of what the spell does.
    public sealed class SpellEffect
    {
        public SpellEffect(Primitive primitive,
                           Reach reach = Reach.Creature,
                           DiceRoll amount = default,
                           DiceRoll perExtraLevel = default,
                           DamageType damageType = DamageType.None,
                           Condition condition = Condition.None,
                           Ability? save = null,
                           OnSave onSave = OnSave.None,
                           bool attackRoll = false,
                           Duration duration = Duration.Instant,
                           int radius = 0,
                           int targets = 1,
                           int extraTargetsPerLevel = 0,
                           int sway = 0,
                           DiceRoll swayDice = default,
                           Sways touches = Sways.None,
                           Skill skill = Skill.None,
                           bool cantripScaling = false,
                           string note = null)
        {
            Kind = primitive;
            Reach = reach;
            Amount = amount;
            PerExtraLevel = perExtraLevel;
            DamageType = damageType;
            Condition = condition;
            Save = save;
            OnSave = onSave;
            AttackRoll = attackRoll;
            Duration = duration;
            Radius = Math.Max(0, radius);
            Targets = Math.Max(1, targets);
            ExtraTargetsPerLevel = Math.Max(0, extraTargetsPerLevel);
            Sway = sway;
            SwayDice = swayDice;
            Touches = touches;
            Skill = skill;
            CantripScaling = cantripScaling;
            Note = note ?? "";
        }

        public Primitive Kind { get; }

        public Reach Reach { get; }

        // damage, healing, temporary hit points - whatever this primitive counts in
        public DiceRoll Amount { get; }

        // what upcasting adds, per level above the spell's own
        public DiceRoll PerExtraLevel { get; }

        public DamageType DamageType { get; }

        public Condition Condition { get; }

        // null means no saving throw; the DC is the caster's, not the spell's
        public Ability? Save { get; }

        public OnSave OnSave { get; }

        // a spell attack roll instead of a save - Fire Bolt, Guiding Bolt
        public bool AttackRoll { get; }

        public Duration Duration { get; }

        // squares, for a burst
        public int Radius { get; }

        public int Targets { get; }

        public int ExtraTargetsPerLevel { get; }

        // a flat shift: Bless's +1d4 is SwayDice, Shield's +5 AC is Sway
        public int Sway { get; }

        public DiceRoll SwayDice { get; }

        // which rolls the sway reaches. declared, never inferred
        public Sways Touches { get; }

        // narrows a Checks sway to one skill - Guidance is any check, Enhance Ability is one
        public Skill Skill { get; }

        // SRD cantrips get another damage die at 5, 11 and 17
        public bool CantripScaling { get; }

        // free text for the eight bounded approximations; never shown to a player
        public string Note { get; }

        public DiceRoll AmountAt(int spellLevel, int castAt, int casterLevel)
        {
            DiceRoll amount = Amount;

            if (CantripScaling && spellLevel == 0)
            {
                int tiers = casterLevel >= 17 ? 3 : casterLevel >= 11 ? 2 : casterLevel >= 5 ? 1 : 0;

                return new DiceRoll(amount.Count * (1 + tiers), amount.Die, amount.Modifier);
            }

            int extra = Math.Max(0, castAt - spellLevel);

            if (extra == 0 || PerExtraLevel.IsNothing) return amount;

            return new DiceRoll(amount.Count + PerExtraLevel.Count * extra,
                                amount.RollsAnything ? amount.Die : PerExtraLevel.Die,
                                amount.Modifier + PerExtraLevel.Modifier * extra);
        }

        public int TargetsAt(int spellLevel, int castAt) =>
            Targets + ExtraTargetsPerLevel * Math.Max(0, castAt - spellLevel);

        public override string ToString() =>
            $"{Kind.Id()} {Reach.Id()}" +
            (Amount.IsNothing ? "" : $" {Amount}") +
            (DamageType == DamageType.None ? "" : $" {DamageType.Id()}") +
            (Condition == Condition.None ? "" : $" {Condition.Id()}") +
            (Save.HasValue ? $" save {Save.Value.Id()} {OnSave.Id()}" : "") +
            (AttackRoll ? " attack" : "") +
            (Radius > 0 ? $" r{Radius}" : "");
    }

    public sealed class Spell
    {
        public Spell(string id, int level, School school, IReadOnlyList<SpellEffect> effects,
                     int range = 0, bool concentration = false, bool ritual = false,
                     bool approximated = false, IReadOnlyList<string> classes = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Level = Math.Clamp(level, 0, 9);
            School = school;
            Effects = effects ?? Array.Empty<SpellEffect>();
            Range = Math.Max(0, range);
            Concentration = concentration;
            Ritual = ritual;
            Approximated = approximated;
            Classes = classes ?? Array.Empty<string>();
        }

        public string Id { get; }

        // 0 is a cantrip, and a cantrip is at-will
        public int Level { get; }

        public School School { get; }

        // squares. 0 is touch or self
        public int Range { get; }

        public bool Concentration { get; }

        public bool Ritual { get; }

        // one of the eight that ship as a bounded approximation (v1_spell_list.md)
        public bool Approximated { get; }

        public IReadOnlyList<SpellEffect> Effects { get; }

        // which class lists it appears on
        public IReadOnlyList<string> Classes { get; }

        public bool IsCantrip => Level == 0;

        public bool Upcastable => !IsCantrip && Effects.Any(
            e => !e.PerExtraLevel.IsNothing || e.ExtraTargetsPerLevel > 0);

        public int MaxRadius => Effects.Count == 0 ? 0 : Effects.Max(e => e.Radius);

        public bool NeedsATargetSquare => Effects.Any(e => e.Reach.NeedsATargetSquare());

        public bool Attacks => Effects.Any(e => e.AttackRoll);

        public Ability? SavedAgainst => Effects.FirstOrDefault(e => e.Save.HasValue)?.Save;

        public bool Does(Primitive primitive) => Effects.Any(e => e.Kind == primitive);

        public string NameKey => KeyConventions.SpellName(Id);

        public string DescriptionKey => KeyConventions.SpellDescription(Id);

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;
        }


        public override string ToString() =>
            $"{Id} (level {Level} {School.ToString().ToLowerInvariant()}" +
            (Concentration ? ", concentration" : "") + $"): " +
            string.Join(" + ", Effects);
    }

    public static class Schools
    {
        public static readonly IReadOnlyList<School> All = new[]
        {
            School.Abjuration, School.Conjuration, School.Divination, School.Enchantment,
            School.Evocation, School.Illusion, School.Necromancy, School.Transmutation,
        };

        public static string Id(this School school) => school.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out School school)
        {
            foreach (School s in All)
            {
                if (!string.Equals(s.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                school = s;
                return true;
            }

            school = School.Evocation;
            return false;
        }

        public static string Id(this Duration duration) => duration.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out Duration duration)
        {
            foreach (Duration d in new[]
                     {
                         Duration.Instant, Duration.Concentration,
                         Duration.Encounter, Duration.Rest,
                     })
            {
                if (!string.Equals(d.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                duration = d;
                return true;
            }

            duration = Duration.Instant;
            return false;
        }
    }
}
