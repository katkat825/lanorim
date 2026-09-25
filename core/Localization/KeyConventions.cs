using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Localization
{
    // shape: namespace.subject.aspect[.qualifier]*[.index]; one key = one whole sentence.
    // core emits keys and never player-visible text - English is a locale file like any other
    // language. CLAUDE.md calls this one of the two things most likely to be got wrong.
    public static class KeyConventions
    {
        // suffixed Ns so they don't shadow the Ability/Condition/Skill domain types

        public const string ActorNs = "actor";
        public const string AbilityNs = "ability";
        public const string SkillNs = "skill";
        public const string ConditionNs = "condition";
        public const string DamageNs = "damage";

        public const string ClassNs = "class";
        public const string SpeciesNs = "species";
        public const string BackgroundNs = "background";
        public const string FeatureNs = "feature";

        public const string SpellNs = "spell";
        public const string ItemNs = "item";
        public const string MonsterNs = "monster";

        // a Wild Shape form card. its own namespace rather than monster's: the card says what the
        // shape is for, and a bestiary's cat and a druid's cat are described for different reasons
        public const string FormNs = "form";

        public const string DifficultyNs = "difficulty";
        public const string ConsequenceNs = "consequence";
        public const string EncounterNs = "encounter";

        public const string DialogueNs = "dialogue";
        public const string CombatNs = "combat";
        public const string QuestNs = "quest";
        public const string CampaignNs = "campaign";
        public const string MerchantNs = "merchant";
        public const string UiNs = "ui";

        // a map prop from the builder's palette
        public const string PropNs = "prop";

        public static readonly IReadOnlyCollection<string> Namespaces = new[]
        {
            ActorNs, AbilityNs, SkillNs, ConditionNs, DamageNs,
            ClassNs, SpeciesNs, BackgroundNs, FeatureNs,
            SpellNs, ItemNs, MonsterNs, FormNs,
            DifficultyNs, ConsequenceNs, EncounterNs,
            DialogueNs, CombatNs, QuestNs, CampaignNs, MerchantNs, UiNs, PropNs,
        };


        public static string Key(string ns, string subject, string aspect, params string[] qualifiers) =>
            string.Join(".", new[] { ns, subject, aspect }.Concat(qualifiers ?? Array.Empty<string>()));

        public static string Indexed(string ns, string subject, string aspect, string qualifier, int index) =>
            $"{ns}.{subject}.{aspect}.{qualifier}.{index:000}";

        public static string ActorName(string id) => Key(ActorNs, id, "name");

        public static string ClassName(string id) => Key(ClassNs, id, "name");

        public static string ClassDescription(string id) => Key(ClassNs, id, "description");

        public static string SpeciesName(string id) => Key(SpeciesNs, id, "name");

        public static string SpeciesDescription(string id) => Key(SpeciesNs, id, "description");

        public static string FeatureName(string id) => Key(FeatureNs, id, "name");

        public static string FeatureDescription(string id) => Key(FeatureNs, id, "description");

        public static string SpellName(string id) => Key(SpellNs, id, "name");

        public static string SpellDescription(string id) => Key(SpellNs, id, "description");

        public static string ItemName(string id) => Key(ItemNs, id, "name");

        public static string ItemDescription(string id) => Key(ItemNs, id, "description");

        public static string MonsterName(string id) => Key(MonsterNs, id, "name");

        public static string FormName(string id) => Key(FormNs, id, "name");

        public static string FormDescription(string id) => Key(FormNs, id, "description");

        public static string BackgroundName(string id) => Key(BackgroundNs, id, "name");

        public static string Line(string speaker, string aspect, string situation, int index) =>
            Indexed(DialogueNs, speaker, aspect, situation, index);

        // A BARK IS A LINE WITH ITS ASPECT SPELLED ONCE. The companion's short reactions are the
        // one kind of line the engine itself asks for by name, so the word "bark" belongs here
        // beside the other conventions rather than in the dialogue module that happens to use it.
        public static string Bark(string speaker, string situation, int index) =>
            Line(speaker, "bark", situation, index);


        static bool IsSegment(string s) =>
            s.Length > 0 && s.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_');

        static bool IsIndex(string s) => s.Length == 3 && s.All(char.IsDigit);

        public const string WellFormed = "well formed";

        // delegates to Explain so the two can't drift into copies that disagree
        public static bool IsWellFormed(string key) => Explain(key) == WellFormed;

        public static string Explain(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "key is empty";
            if (key != key.ToLowerInvariant()) return $"'{key}' is not lowercase";

            string[] parts = key.Split('.');
            if (parts.Length < 3) return $"'{key}' has {parts.Length} segments; the grammar needs at least 3";
            if (!Namespaces.Contains(parts[0]))
                return $"'{parts[0]}' is not a known namespace ({string.Join(", ", Namespaces)})";

            string bad = parts.FirstOrDefault(p => !IsSegment(p));
            if (bad != null) return $"segment '{bad}' must be lowercase a-z, 0-9 and underscore only";

            for (int i = 0; i < parts.Length - 1; i++)
                if (IsIndex(parts[i])) return $"'{key}' has an index at position {i}; indices go last";

            return WellFormed;
        }
    }
}
