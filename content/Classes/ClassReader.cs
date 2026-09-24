using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Magic;
using Core.Characters;
using Core.Dice;

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

        static CharacterClass ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a class id");
                return null;
            }

            if (!DieExtensions.TryParse(entry.Text("hit_die", "d8"), out Die hitDie))
            {
                problems.Add($"{id}: '{entry.Text("hit_die")}' is not a die");
                hitDie = Die.D8;
            }

            var saves = new List<Ability>();

            foreach (string save in entry.Strings("saves"))
            {
                if (Abilities.TryParse(save, out Ability ability)) saves.Add(ability);
                else problems.Add($"{id}: '{save}' is not an ability");
            }

            if (saves.Count != 2)
                problems.Add($"{id}: {saves.Count} save proficiencies - SRD gives every class two");

            var skills = new List<Skill>();

            foreach (string skill in entry.Strings("skill_choices"))
            {
                if (Skills.TryParse(skill, out Skill read)) skills.Add(read);
                else problems.Add($"{id}: '{skill}' is not one of the eighteen skills");
            }

            var armor = new List<ArmorWeight>();

            foreach (string weight in entry.Strings("armor"))
            {
                if (ArmorWeights.TryParse(weight, out ArmorWeight read)) armor.Add(read);
                else problems.Add($"{id}: '{weight}' is not an armor weight");
            }

            var priority = new List<Ability>();

            foreach (string ability in entry.Strings("priority"))
            {
                if (Abilities.TryParse(ability, out Ability read)) priority.Add(read);
                else problems.Add($"{id}: '{ability}' is not an ability");
            }

            var features = new List<Feature>();

            foreach (JsonElement raw in entry.Items("features"))
            {
                Feature feature = FeatureReader.ReadOne(raw, id, problems);

                if (feature != null) features.Add(feature);
            }

            int picks = entry.Number("skill_picks", 2);

            if (picks > skills.Count && skills.Count > 0)
                problems.Add($"{id}: {picks} skills to pick from a list of {skills.Count}");

            return new CharacterClass(id, hitDie, saves, skills, picks, armor,
                                      entry.Flag("shields"),
                                      entry.Strings("starting_gear"),
                                      features,
                                      entry.Text("subclass"),
                                      entry.Text("companion"),
                                      priority);
        }
    }

    // shared by classes and species: a feature is a feature wherever it came from
    public static class FeatureReader
    {
        public static Feature ReadOne(JsonElement raw, string owner, List<string> problems)
        {
            string id = raw.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"{owner}: '{id}' is not a feature id");
                return null;
            }

            if (!Traits.TryParse(raw.Text("trait"), out Trait trait))
            {
                problems.Add($"{owner}/{id}: '{raw.Text("trait")}' is not a trait - the closed " +
                             "list is in content/Classes/Feature.cs");
                return null;
            }

            if (!Traits.TryParse(raw.Text("when", "always"), out When when))
                problems.Add($"{owner}/{id}: '{raw.Text("when")}' is not a trigger");

            if (!Traits.TryParse(raw.Text("grants", "action"), out Grants grants))
                problems.Add($"{owner}/{id}: '{raw.Text("grants")}' is not an action, a " +
                             "bonus_action or a reaction");

            if (!Core.Magic.Schools.TryParse(raw.Text("duration", "encounter"), out Duration duration))
                problems.Add($"{owner}/{id}: '{raw.Text("duration")}' is not a duration");

            if (!Core.Magic.Primitives.TryParse(raw.Text("touches", "none"),
                                                out Core.Magic.Sways touches))
                problems.Add($"{owner}/{id}: '{raw.Text("touches")}' is not a list of swayed rolls");

            if (!DamageTypes.TryParse(raw.Text("defense", "resistant"), out Defense defense))
                defense = Defense.Resistant;

            // absent is None, which only a spellcasting feature is then refused for - every other
            // kind of feature has no progression and should not have to say so
            if (!Vocabulary.TryWord(raw.Text("progression", "none"),
                                    out CasterProgression progression))
                problems.Add($"{owner}/{id}: '{raw.Text("progression")}' is not a caster " +
                             "progression - it is " + Vocabulary.Offer<CasterProgression>());

            var skills = new List<Skill>();

            foreach (string skill in raw.Strings("skills"))
            {
                if (Skills.TryParse(skill, out Skill read)) skills.Add(read);
                else problems.Add($"{owner}/{id}: '{skill}' is not a skill");
            }

            var saves = new List<Ability>();

            foreach (string save in raw.Strings("saves"))
            {
                if (Abilities.TryParse(save, out Ability read)) saves.Add(read);
                else problems.Add($"{owner}/{id}: '{save}' is not an ability");
            }

            var feature = new Feature(id, trait,
                                      raw.Number("level", 1),
                                      raw.Dice("amount", problems, id),
                                      raw.Dice("per_level", problems, id),
                                      raw.Damage("damage_type", problems, id),
                                      raw.Condition("condition", problems, id),
                                      raw.Ability("ability", problems, id),
                                      defense,
                                      when,
                                      grants,
                                      raw.Number("flat"),
                                      raw.Number("uses"),
                                      raw.Number("count"),
                                      raw.Number("per_levels", 1),
                                      duration,
                                      skills,
                                      saves,
                                      touches,
                                      progression,
                                      raw.Text("note"));

            Check(feature, owner, problems);

            return feature;
        }

        static void Check(Feature feature, string owner, List<string> problems)
        {
            string where = $"{owner}/{feature.Id}";

            switch (feature.Trait)
            {
                case Trait.Rider:
                    if (feature.Amount.IsNothing && feature.Condition == Condition.None)
                        problems.Add($"{where}: a rider that adds nothing and applies nothing");

                    // no damage_type is the normal case: a Sneak Attack is not its own kind of
                    // damage, it is more of the weapon's
                    break;

                case Trait.Stance:
                    if (feature.Touches == Core.Magic.Sways.None)
                        problems.Add($"{where}: a stance that touches no roll");
                    break;

                case Trait.UnarmoredDefense:
                    if (!feature.Ability.HasValue)
                        problems.Add($"{where}: unarmored defence needs the ability it reads");
                    break;

                case Trait.Resistance:
                    if (feature.DamageType == DamageType.None)
                        problems.Add($"{where}: resistance to what?");
                    break;

                case Trait.Spellcasting:
                    if (!feature.Ability.HasValue)
                        problems.Add($"{where}: spellcasting needs its ability");

                    if (feature.Progression == CasterProgression.None)
                        problems.Add($"{where}: spellcasting needs a progression - it is " +
                                     Vocabulary.Offer<CasterProgression>() +
                                     ", and it is what both the slot table and the point pool " +
                                     "are read from");
                    break;

                case Trait.Recovery:
                    if (feature.Amount.IsNothing && feature.Flat == 0)
                        problems.Add($"{where}: a recovery that heals nothing");
                    break;

                case Trait.Expertise:
                    if (feature.Count <= 0 && feature.Skills.Count == 0)
                        problems.Add($"{where}: expertise in how many skills?");
                    break;

                case Trait.ActionGrant:
                    // the line v1_class_roster.md draws: the hero already has two actions, so a
                    // standing extra one is Extra Attack in disguise. every round is a bonus
                    // action or a reaction; a whole action is a per-rest thing.
                    if (feature.Uses == 0 && feature.Grants == Grants.Action)
                        problems.Add($"{where}: an extra action every round is Extra Attack by " +
                                     "another name. give it 'uses', or grant a bonus_action");
                    break;
            }
        }
    }
}
