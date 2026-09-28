using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Words;

namespace Content.Classes
{
    public static class ClassReader
    {
        public static bool TryRead(string text, out IReadOnlyList<CharacterClass> classes,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<CharacterClass>();
            var trouble = new List<string>();

            classes = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                Keyed.OnlyKnown(document.RootElement, new[] { "classes" }, "the classes file", trouble);

                foreach (JsonElement entry in document.RootElement.Items("classes"))
                {
                    CharacterClass read = ReadOne(entry, trouble);

                    if (read != null) found.Add(read);
                }

                if (found.Count == 0 && trouble.Count == 0)
                    trouble.Add("no classes in it - the file is an object with a 'classes' array");
            }

            return trouble.Count == 0;
        }

        // every key a class takes (its features' are FeatureReader.Keys)
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "id", "hit_die", "saves", "skill_choices", "skill_picks", "armor_training", "shields", "priority",
            "subclass", "companion", "starting_gear", "gold", "tools", "weapon_proficiencies", "features",
            "improvement_levels",
        };

        static CharacterClass ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a class id");
                return null;
            }

            Keyed.OnlyKnown(entry, Keys, id, problems);

            if (!DieExtensions.TryParse(entry.Text("hit_die", "d8"), out Die hitDie))
            {
                problems.Add($"{id}: '{entry.Text("hit_die")}' is not a die");
                hitDie = Die.D8;
            }

            var saves = entry.AbilityList("saves", problems, id).ToList();

            if (saves.Count != 2)
                problems.Add($"{id}: {saves.Count} save proficiencies - SRD gives every class two");

            var skills = entry.SkillList("skill_choices", problems, id).ToList();

            List<ArmorCategory> armor = ArmorTraining(entry, id, problems);

            var priority = entry.AbilityList("priority", problems, id).ToList();

            var features = new List<Feature>();

            foreach (JsonElement raw in entry.Items("features"))
            {
                Feature feature = FeatureReader.ReadOne(raw, id, problems);

                if (feature != null) features.Add(feature);
            }

            int picks = entry.Number("skill_picks", 2);

            if (picks > skills.Count && skills.Count > 0)
                problems.Add($"{id}: {picks} skills to pick from a list of {skills.Count}");

            IReadOnlyList<int> improvements = ImprovementLevels(entry, id, problems);

            return new CharacterClass(id, hitDie, saves, skills, picks, armor,
                                      entry.Flag("shields"),
                                      entry.Strings("starting_gear"),
                                      features,
                                      entry.Text("subclass"),
                                      entry.Text("companion"),
                                      priority)
            {
                ImprovementLevels = improvements,
                Gold = entry.Number("gold"),
                Tools = entry.Strings("tools"),
                // SRD's Weapon Proficiencies: simple, martial
                WeaponProficiencies = entry.Strings("weapon_proficiencies"),
            };
        }

        // SRD's Armor Training: the categories, light, medium and heavy
        static List<ArmorCategory> ArmorTraining(JsonElement entry, string id, List<string> problems)
        {
            var armor = new List<ArmorCategory>();

            foreach (string category in entry.Strings("armor_training"))
            {
                if (EnumWords.TryParse(category, out ArmorCategory read)) armor.Add(read);
                else problems.Add($"{id}: '{category}' is not an armor category");
            }

            return armor;
        }

        // the class's own ASI levels (the Fighter's 6 and 14), or the usual ones
        static IReadOnlyList<int> ImprovementLevels(JsonElement entry, string id, List<string> problems)
        {
            if (!entry.Has("improvement_levels")) return CharacterClass.UsualImprovementLevels;

            var levels = new List<int>();

            foreach (JsonElement level in entry.Items("improvement_levels"))
                if (level.ValueKind == JsonValueKind.Number && level.TryGetInt32(out int at) && at >= 1 && at <= 20)
                    levels.Add(at);
                else
                    problems.Add($"{id}: improvement_levels holds '{level}', which is not a level from 1 to 20");

            return levels.Distinct().OrderBy(l => l).ToList();
        }
    }
}
