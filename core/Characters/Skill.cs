using System;
using System.Collections.Generic;
using Core.Localization;

namespace Core.Characters
{
    // the 18 SRD 5.2.1 skills. a campaign names one of these for a check and nothing else -
    // content.tests refuses a campaign that asks for a skill not on this list.
    public enum Skill
    {
        None = 0,

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

        static readonly Dictionary<Skill, string> Ids = BuildIds();

        static Dictionary<Skill, string> BuildIds()
        {
            var ids = new Dictionary<Skill, string>();

            foreach (Skill skill in All) ids[skill] = Snake(skill.ToString());

            return ids;
        }

        // AnimalHandling -> animal_handling; derived, never listed, so the two can't drift
        static string Snake(string pascal)
        {
            var s = new System.Text.StringBuilder();

            for (int i = 0; i < pascal.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascal[i])) s.Append('_');

                s.Append(char.ToLowerInvariant(pascal[i]));
            }

            return s.ToString();
        }

        public static string Id(this Skill skill) =>
            skill == Skill.None ? "none"
          : Ids.TryGetValue(skill, out string id) ? id
          : "none";

        public static bool TryParse(string id, out Skill skill)
        {
            foreach (Skill s in All)
            {
                if (!string.Equals(s.Id(), id, StringComparison.OrdinalIgnoreCase)) continue;

                skill = s;
                return true;
            }

            skill = Skill.None;
            return false;
        }

        public static string NameKey(this Skill skill) =>
            KeyConventions.Key(KeyConventions.SkillNs, skill.Id(), "name");
    }
}
