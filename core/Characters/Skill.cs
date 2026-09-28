using System.Collections.Generic;
using Core.Localization;
using Core.Words;

namespace Core.Characters
{
    // the 18 SRD 5.2.1 skills. a campaign names one of these for a check and nothing else -
    // content.tests refuses a campaign that asks for a skill not on this list. the word is the name
    // in snake case, "animal_handling": derived, never listed, so the two can't drift
    public enum Skill
    {
        [Unread] None = 0,

        Acrobatics,
        AnimalHandling,
        Arcana,
        Athletics,
        Deception,
        History,
        Insight,
        Intimidation,
        Investigation,
        Medicine,
        Nature,
        Perception,
        Performance,
        Persuasion,
        Religion,
        SleightOfHand,
        Stealth,
        Survival,
    }

    public static class Skills
    {
        public static readonly IReadOnlyList<Skill> All = new[]
        {
            Skill.Acrobatics, Skill.AnimalHandling, Skill.Arcana, Skill.Athletics,
            Skill.Deception, Skill.History, Skill.Insight, Skill.Intimidation,
            Skill.Investigation, Skill.Medicine, Skill.Nature, Skill.Perception,
            Skill.Performance, Skill.Persuasion, Skill.Religion, Skill.SleightOfHand,
            Skill.Stealth, Skill.Survival,
        };

        public const int Count = 18;

        // which ability a skill is rolled off - SRD 5.2.1, and it is the skill that decides, never
        // the campaign: a campaign asking for "stealth vs strength" is a content error
        static readonly Dictionary<Skill, Ability> Governing = new()
        {
            [Skill.Acrobatics] = Ability.Dexterity,
            [Skill.AnimalHandling] = Ability.Wisdom,
            [Skill.Arcana] = Ability.Intelligence,
            [Skill.Athletics] = Ability.Strength,
            [Skill.Deception] = Ability.Charisma,
            [Skill.History] = Ability.Intelligence,
            [Skill.Insight] = Ability.Wisdom,
            [Skill.Intimidation] = Ability.Charisma,
            [Skill.Investigation] = Ability.Intelligence,
            [Skill.Medicine] = Ability.Wisdom,
            [Skill.Nature] = Ability.Intelligence,
            [Skill.Perception] = Ability.Wisdom,
            [Skill.Performance] = Ability.Charisma,
            [Skill.Persuasion] = Ability.Charisma,
            [Skill.Religion] = Ability.Intelligence,
            [Skill.SleightOfHand] = Ability.Dexterity,
            [Skill.Stealth] = Ability.Dexterity,
            [Skill.Survival] = Ability.Wisdom,
        };

        public static Ability Governs(this Skill skill) =>
            Governing.TryGetValue(skill, out Ability ability) ? ability : Ability.Strength;

        public static string NameKey(this Skill skill) =>
            KeyConventions.Key(KeyConventions.SkillNs, skill.Id(), "name");
    }
}
