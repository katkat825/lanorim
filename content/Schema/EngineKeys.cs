using System.Collections.Generic;
using System.Linq;
using Content.Spells;
using Core.Characters;
using Core.Localization;
using Core.Magic;
using Core.Resolution;

namespace Content.Schema
{
    // every localization key the engine itself emits - derived from the domain types, never
    // listed. a listed copy is a second description of the same thing and free to drift; this way
    // adding a condition adds its keys, and the locale audit fails until English exists for them.
    public static class EngineKeys
    {
        public static IEnumerable<string> All(SpellBook spells = null)
        {
            foreach (Ability ability in Abilities.All)
            {
                yield return ability.NameKey();
                yield return ability.ShortKey();
            }

            foreach (Skill skill in Skills.All) yield return skill.NameKey();

            foreach (Condition condition in Conditions.All)
            {
                yield return condition.NameKey();
                yield return condition.DescriptionKey();
            }

            foreach (DamageType type in DamageTypes.All) yield return type.NameKey();

            foreach (Difficulty difficulty in Difficulties.Ladder)
                yield return difficulty.NameKey();

            foreach (ArmorWeight weight in new[]
                     { ArmorWeight.None, ArmorWeight.Light, ArmorWeight.Medium, ArmorWeight.Heavy })
                yield return weight.NameKey();

            // the two ways of paying for a spell, as the creation screen offers them. Derived from
            // the enum, so a third mode would be owed its words the day it existed
            foreach (string key in Content.Creation.Creation.ResourceKeys()) yield return key;

            // everything the SRD data ships: spells, items, classes, species, backgrounds and
            // the merchant's refusals
            foreach (string key in Library.Srd().Keys()) yield return key;

            if (spells == null) yield break;

            foreach (string key in spells.Keys()) yield return key;
        }

        // every key, deduplicated and in a stable order, which is what the CSV is written in
        public static IReadOnlyList<string> Sorted(SpellBook spells = null) =>
            All(spells).Distinct().OrderBy(k => k, System.StringComparer.Ordinal).ToList();

        // the grammar check, run over the whole set at once
        public static IEnumerable<string> Malformed(SpellBook spells = null) =>
            All(spells).Distinct()
                       .Where(k => !KeyConventions.IsWellFormed(k))
                       .Select(KeyConventions.Explain);
    }
}
