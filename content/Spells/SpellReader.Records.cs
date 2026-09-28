using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Magic;

namespace Content.Spells
{
    public static partial class SpellReader
    {
        // "upcast": {"amount": "1d6", "targets": 1, "radius": 4, "blocks_spells_up_to": 1}
        static Upcast ReadUpcast(JsonElement raw, string where, List<string> problems)
        {
            if (!Record(raw, "upcast", UpcastKeys, where, problems, out JsonElement upcast)) return Upcast.Nothing;

            return new Upcast
            {
                Amount = upcast.Dice("amount", problems, where),
                Targets = upcast.Number("targets"),
                Radius = upcast.Number("radius"),
                BlocksSpellsUpTo = upcast.Number("blocks_spells_up_to"),
            };
        }

        // "hit_points": {"at_or_below": 150} or {"above": 150}
        static HitPointGate ReadHitPoints(JsonElement raw, string where, List<string> problems)
        {
            if (!Record(raw, "hit_points", HitPointKeys, where, problems, out JsonElement gate)) return null;

            bool below = gate.Has("at_or_below");

            if (below == gate.Has("above"))
            {
                problems.Add($"{where}: 'hit_points' is 'at_or_below' a line or 'above' it - one of them");
                return null;
            }

            return below ? new HitPointGate(gate.Number("at_or_below"), false)
                         : new HitPointGate(gate.Number("above"), true);
        }

        // "extra_dice": {"dice": "1d8", "tag_rules": {"fiend": "only", "undead": "only"}} or
        // {..., "setting": "storm"}
        static ExtraDice ReadExtraDice(JsonElement raw, string where, List<string> problems)
        {
            if (!Record(raw, "extra_dice", ExtraDiceKeys, where, problems, out JsonElement extra)) return null;

            IReadOnlyList<TagRule> rules = extra.TagRuleList("tag_rules", problems, where);

            if (rules.Any(r => r.Outcome != TagOutcome.Only))
                problems.Add($"{where}: extra dice's tag rules are all 'only' - the tags it is against");

            return new ExtraDice(extra.Dice("dice", problems, where), rules, extra.Text("setting") ?? "");
        }

        // "raises": {"as": "zombie", "tag_rules": {"humanoid": "only"}}
        static Raises ReadRaises(JsonElement raw, string where, List<string> problems)
        {
            if (!Record(raw, "raises", RaisesKeys, where, problems, out JsonElement raises)) return null;

            IReadOnlyList<TagRule> rules = raises.TagRuleList("tag_rules", problems, where);

            if (rules.Any(r => r.Outcome != TagOutcome.Only))
                problems.Add($"{where}: a 'raises' record's tag rules are all 'only'");

            return new Raises(raises.Text("as") ?? "", rules);
        }

        // "item": {"id": "goodberry", "count": 10}
        static ConjuredItem ReadItem(JsonElement raw, string where, List<string> problems)
        {
            if (!Record(raw, "item", ItemKeys, where, problems, out JsonElement item)) return null;

            return new ConjuredItem(item.Text("id") ?? "", Math.Max(1, item.Number("count", 1)));
        }

        static bool Record(JsonElement raw, string name, IReadOnlyList<string> keys, string where,
                           List<string> problems, out JsonElement record)
        {
            record = default;

            if (!raw.Has(name)) return false;

            record = raw.GetProperty(name);

            if (record.ValueKind != JsonValueKind.Object)
            {
                problems.Add($"{where}: '{name}' is a record, {{...}}");
                return false;
            }

            Keyed.OnlyKnown(record, keys, $"{where} {name}", problems);
            return true;
        }
    }
}
