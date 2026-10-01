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
            "moves_when_down", "force_creation", "dc_ability", "solo", "effects",
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

            if (PrimitiveHandlers.For(primitive).CarriesABoon) keys.UnionWith(BoonSpecReader.Keys);

            return keys;
        }

        public static bool TryRead(string text, out IReadOnlyList<Spell> spells,
                                   out IReadOnlyList<string> problems) =>
            Entries.TryRead(text, out spells, out problems);

        // the file: { "spells": [ ... ] }, or a bare array of spells, each read by ReadOne (EntryList).
        // made on first use: SpellKeys is in this file and ReadOne in another
        static EntryList<Spell> _entries;

        public static EntryList<Spell> Entries =>
            _entries ??= new EntryList<Spell>("spells", "spell", SpellKeys, ReadOne, bareArray: true);

        // one spell object on its own - a statblock's special action is one, inline
        public static Spell ReadEntry(JsonElement entry, List<string> problems) =>
            Entries.ReadEntry(entry, problems ?? new List<string>());
    }
}
