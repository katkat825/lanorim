using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Schema;
using Core.Magic;

namespace Content.Spells
{
    // reads a spell file into Spells. the file says which primitives the spell is made of and
    // with what numbers - there is no per-spell code anywhere, and a spell this reader cannot
    // express is one that needs a new primitive or a bounded approximation, not a special case.
    //
    // WHICH KEYS AN EFFECT MAY CARRY: the common ones below, and the ones its primitive's handler
    // takes (core/Magic/Primitives) - a sway's include every BoonSpec key. anything else is refused
    // with the nearest known key named, so a typo or a setting on the wrong primitive is a load
    // error rather than a spell that quietly does nothing (cc_task_dedupe-effects.md, Phases 5-6).
    // docs/spell_effect_reference.md lists them all, and a test holds the two together.
    public static partial class SpellReader
    {
        // on the spell itself
        public static readonly IReadOnlyList<string> SpellKeys = new[]
        {
            "id", "level", "school", "range", "concentration", "ritual", "approximated", "classes",
            "casting_time", "trigger", "repeat", "duration", "shapes", "curse", "range_scales",
            "not_in_srd", "answers_spell", "out_of_combat", "concentration_below", "ends_previous",
            "moves_when_down", "force_creation", "dc_ability", "effects",
        };

        // on any effect, whatever its primitive
        public static readonly IReadOnlyList<string> CommonKeys = new[]
        {
            "primitive", "note", "mode", "aim", "targets", "radius", "length", "width", "points",
            "up_to", "affects", "tag_rules", "hit_points", "needs_sight", "max_size", "follows",
            "pulses", "core_only", "switches", "dispels_darkness", "amount", "upcast",
            "damage_type", "damage_choices", "condition", "save", "on_save", "same_save",
            "save_if_unwilling", "advantage_if_fought", "breaks_concentration", "attack_roll",
            "duration", "lands", "cantrip_growth", "add_modifier",
        };

        // inside the records the common keys hold
        public static readonly IReadOnlyList<string> UpcastKeys = new[] { "amount", "targets", "radius", "blocks_spells_up_to" };

        public static readonly IReadOnlyList<string> HitPointKeys = new[] { "at_or_below", "above" };

        public static readonly IReadOnlyList<string> ExtraDiceKeys = new[] { "dice", "tag_rules", "setting" };

        public static readonly IReadOnlyList<string> RaisesKeys = new[] { "as", "tag_rules" };

        public static readonly IReadOnlyList<string> ItemKeys = new[] { "id", "count" };

        // every key an effect of this primitive may carry
        public static IReadOnlyCollection<string> KeysFor(Primitive primitive)
        {
            var keys = new HashSet<string>(CommonKeys, StringComparer.Ordinal);

            keys.UnionWith(PrimitiveHandlers.For(primitive).Keys);

            if (primitive == Primitive.Sway) keys.UnionWith(BoonSpecReader.Keys);

            return keys;
        }

        public static bool TryRead(string text, out IReadOnlyList<Spell> spells,
                                   out IReadOnlyList<string> problems)
        {
            var found = new List<Spell>();
            var trouble = new List<string>();

            spells = found;
            problems = trouble;

            if (!Json.TryParse(text, out JsonDocument document, out string bad))
            {
                trouble.Add(bad);
                return false;
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                IReadOnlyList<JsonElement> entries =
                    root.ValueKind == JsonValueKind.Array
                        ? ToList(root)
                        : root.Items("spells");

                if (entries.Count == 0)
                    trouble.Add("no spells in it - the file is an array, or an object with a " +
                                "'spells' array");

                foreach (JsonElement entry in entries)
                {
                    Spell spell = ReadOne(entry, trouble);

                    if (spell != null) found.Add(spell);
                }
            }

            return trouble.Count == 0;
        }

        static IReadOnlyList<JsonElement> ToList(JsonElement array)
        {
            var list = new List<JsonElement>();

            foreach (JsonElement item in array.EnumerateArray()) list.Add(item);

            return list;
        }

        // one spell object on its own - a statblock's special action is one, inline
        public static Spell ReadEntry(JsonElement entry, List<string> problems) =>
            ReadOne(entry, problems ?? new List<string>());
    }
}
