using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Magic;

namespace Content.Spells
{
    // reads a spell file into Spells. the file says which primitives the spell is made of and
    // with what numbers - there is no per-spell code anywhere, and a spell this reader cannot
    // express is one that needs a new primitive or a bounded approximation, not a special case.
    public static class SpellReader
    {
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

        static Spell ReadOne(JsonElement entry, List<string> problems)
        {
            string id = entry.Text("id");

            if (!Json.IsId(id))
            {
                problems.Add($"'{id}' is not a spell id - lowercase a-z, 0-9 and underscore only");
                return null;
            }

            int level = entry.Number("level", -1);

            if (level < 0 || level > 9)
            {
                problems.Add($"{id}: level {level} - a spell is level 0 (a cantrip) to 9");
                return null;
            }

            if (!Schools.TryParse(entry.Text("school", "evocation"), out School school))
                problems.Add($"{id}: '{entry.Text("school")}' is not a school of magic");

            var effects = new List<SpellEffect>();

            foreach (JsonElement raw in entry.Items("effects"))
            {
                SpellEffect effect = ReadEffect(raw, id, level, problems);

                if (effect != null) effects.Add(effect);
            }

            if (effects.Count == 0)
                problems.Add($"{id}: no effects - a spell that does nothing is a reference card, " +
                             "and reference cards are not loaded here");

            bool concentration = entry.Flag("concentration");

            // the two have to agree or the sheet's "concentrating" light lies: a spell marked for
            // concentration must hold something, and something held must be marked
            foreach (SpellEffect effect in effects)
                if (effect.Duration == Duration.Concentration && !concentration)
                    problems.Add($"{id}: an effect lasts for concentration but the spell is not " +
                                 "marked 'concentration: true'");

            return new Spell(id, level, school, effects,
                             entry.Number("range"),
                             concentration,
                             entry.Flag("ritual"),
                             entry.Flag("approximated"),
                             entry.Strings("classes"));
        }

        static SpellEffect ReadEffect(JsonElement raw, string spellId, int level,
                                      List<string> problems)
        {
            string where = spellId;

            if (!Primitives.TryParse(raw.Text("primitive"), out Primitive primitive))
            {
                problems.Add($"{where}: '{raw.Text("primitive")}' is not a primitive - the closed " +
                             "list is in core/Magic/Primitive.cs");
                return null;
            }

            if (!Primitives.TryParse(raw.Text("reach", "creature"), out Reach reach))
                problems.Add($"{where}: '{raw.Text("reach")}' is not a reach");

            if (!Primitives.TryParse(raw.Text("on_save", "none"), out OnSave onSave))
                problems.Add($"{where}: '{raw.Text("on_save")}' is not a save outcome");

            if (!Primitives.TryParse(raw.Text("touches", "none"), out Sways touches))
                problems.Add($"{where}: '{raw.Text("touches")}' is not a list of swayed rolls " +
                             "(attacks|saves|checks|damage|armor_class)");

            if (!Schools.TryParse(raw.Text("duration", "instant"), out Duration duration))
                problems.Add($"{where}: '{raw.Text("duration")}' is not a duration");

            Ability? save = raw.Ability("save", problems, where);
            bool attackRoll = raw.Flag("attack_roll");

            if (attackRoll && save.HasValue)
                problems.Add($"{where}: an effect rolls to hit or calls for a save, never both");

            if (onSave != OnSave.None && !save.HasValue)
                problems.Add($"{where}: 'on_save' is set but there is no save to make");

            var effect = new SpellEffect(
                primitive,
                reach,
                raw.Dice("amount", problems, where),
                raw.Dice("per_extra_level", problems, where),
                raw.Damage("damage_type", problems, where),
                raw.Condition("condition", problems, where),
                save,
                onSave,
                attackRoll,
                duration,
                raw.Number("radius"),
                raw.Number("targets", 1),
                raw.Number("extra_targets_per_level"),
                raw.Number("sway"),
                raw.Dice("sway_dice", problems, where),
                touches,
                raw.Skill("skill", problems, where),
                raw.Flag("cantrip_scaling"),
                raw.Text("note"));

            Check(effect, spellId, level, problems);

            return effect;
        }

        // the per-primitive rules a data file has to obey. this is the only place that knows what
        // each primitive needs, and it is why a typo in a spell file is a load error with a name
        // on it rather than a spell that quietly does nothing.
        static void Check(SpellEffect effect, string spellId, int level, List<string> problems)
        {
            switch (effect.Kind)
            {
                case Primitive.Damage:
                    if (effect.Amount.IsNothing)
                        problems.Add($"{spellId}: a damage effect with no 'amount'");

                    if (effect.DamageType == DamageType.None)
                        problems.Add($"{spellId}: damage with no 'damage_type' - every hit is " +
                                     "typed, because resistance is read off the type");
                    break;

                case Primitive.Heal:
                case Primitive.Ward:
                    if (effect.Amount.IsNothing)
                        problems.Add($"{spellId}: a {effect.Kind.Id()} effect with no 'amount'");
                    break;

                case Primitive.Afflict:
                case Primitive.Relieve:
                    if (effect.Condition == Condition.None)
                        problems.Add($"{spellId}: a {effect.Kind.Id()} effect with no 'condition'");
                    break;

                case Primitive.Sway:
                    if (effect.Touches == Sways.None)
                        problems.Add($"{spellId}: a sway that touches nothing - say which rolls " +
                                     "it reaches with 'touches'");

                    if (effect.Sway == 0 && effect.SwayDice.IsNothing)
                        problems.Add($"{spellId}: a sway of zero - give it 'sway' or 'sway_dice'");
                    break;

                case Primitive.Zone:
                case Primitive.Illuminate:
                    if (effect.Radius <= 0)
                        problems.Add($"{spellId}: a {effect.Kind.Id()} with no 'radius'");
                    break;
            }

            if (effect.Reach.IsArea() && effect.Radius <= 0)
                problems.Add($"{spellId}: a burst needs a 'radius' in squares");

            if (effect.CantripScaling && level != 0)
                problems.Add($"{spellId}: 'cantrip_scaling' on a level {level} spell - only " +
                             "cantrips scale with the caster's level");

            if (!effect.PerExtraLevel.IsNothing && level == 0)
                problems.Add($"{spellId}: a cantrip cannot be upcast; it scales with your level");
        }
    }
}
