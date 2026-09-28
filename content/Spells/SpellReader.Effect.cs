using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Magic;
using Core.Words;

namespace Content.Spells
{
    public static partial class SpellReader
    {
        static SpellEffect ReadEffect(JsonElement raw, string spellId, int level,
                                      List<string> problems)
        {
            string where = spellId;

            if (!EnumWords.TryParse(raw.Text("primitive"), out Primitive primitive))
            {
                problems.Add($"{where}: '{raw.Text("primitive")}' is not a primitive - the closed " +
                             "list is in core/Magic/Primitive.cs");
                return null;
            }

            // a boon's key anywhere but a sway says so plainly; any other stray key names the
            // nearest one a <primitive> takes
            foreach (string key in BoonSpecReader.Keys.Where(k => primitive != Primitive.Sway && raw.Has(k)))
                problems.Add($"{where}: '{key}' is what a boon is - it belongs on a sway");

            Keyed.OnlyKnown(raw, KeysFor(primitive).Concat(primitive == Primitive.Sway ? Array.Empty<string>() : BoonSpecReader.Keys),
                            $"{where} ({primitive.Id()})", problems);

            SpellEffect effect = Build(raw, primitive, EffectWords.Read(raw, where, problems), where, problems);

            if (!string.IsNullOrEmpty(raw.Text("encloses")) &&
                raw.Text("encloses") != "bars" && raw.Text("encloses") != "solid")
                problems.Add($"{where}: 'encloses' is 'bars' or 'solid'");

            if (primitive == Primitive.Sway)
                BoonSpecReader.Check(effect.Boon, where, problems, mustDoSomething: false);

            Check(effect, spellId, level, raw.Has("radius"), problems);

            foreach (string wrong in PrimitiveHandlers.For(primitive).Check(effect, level))
                problems.Add($"{spellId}: {wrong}");

            return effect;
        }

        // the effect itself: the common settings, then each primitive's (SpellEffect's partials, one
        // per primitive, beside the handler that declares the keys)
        static SpellEffect Build(JsonElement raw, Primitive primitive, EffectWords words, string where,
                                 List<string> problems)
        {
            (AimKind aim, OnSave onSave, Duration duration, Ability? save, bool attackRoll, Lands lands,
             CantripGrowth growth, Affects affects, Obscurement obscures, Command command, Size? maxSize,
             List<DiceRoll> extraTiers, Pulses pulses) = words;

            return new SpellEffect(
                primitive,
                aim,
                raw.Dice("amount", problems, where),
                raw.Damage("damage_type", problems, where),
                raw.Condition("condition", problems, where),
                save,
                onSave,
                attackRoll,
                duration,
                raw.Number("radius"),
                raw.Number("targets", 1),
                raw.Text("note"),
                raw.Number("length"),
                raw.Number("width"))
            {
                // the common settings
                Upcast = ReadUpcast(raw, where, problems),
                DamageChoices = raw.DamageTypeList("damage_choices", problems, where),
                Mode = raw.Text("mode") ?? "",
                Boon = primitive == Primitive.Sway
                    ? BoonSpecReader.Read(raw, duration, where, problems)
                    : BoonSpec.Nothing,
                Linger = LingerSpecReader.Read(raw, where, problems),
                Lands = lands,
                CantripGrowth = growth,
                AddsModifier = raw.Flag("add_modifier"),
                Points = Math.Max(1, raw.Number("points", 1)),
                UpTo = raw.Number("up_to"),
                Affects = affects,
                TagRules = raw.TagRuleList("tag_rules", problems, where),
                HitPoints = ReadHitPoints(raw, where, problems),
                NeedsSight = raw.Flag("needs_sight"),
                MaxSize = maxSize,
                Follows = raw.Flag("follows"),
                Pulses = pulses,
                CoreOnly = raw.Flag("core_only"),
                Switches = raw.Flag("switches"),
                DispelsDarkness = raw.Flag("dispels_darkness"),
                SameSave = raw.Flag("same_save"),
                SaveIfUnwilling = raw.Flag("save_if_unwilling"),
                AdvantageIfFought = raw.Flag("advantage_if_fought"),
                BreaksConcentration = raw.Flag("breaks_concentration"),

                // damage's
                SlaysAtOrBelow = raw.Number("slays_at_or_below"),
                Dust = raw.Flag("dust"),
                Raises = ReadRaises(raw, where, problems),
                Leaps = raw.Number("leaps"),
                ExtraDice = ReadExtraDice(raw, where, problems),
                RevertsShape = raw.Flag("reverts_shape"),
                ReactionFlee = raw.Flag("reaction_flee"),
                NearFirst = raw.Number("near_first"),
                NearZone = raw.Number("near_zone"),
                WithinZone = raw.Flag("within_zone"),

                // a zone's
                Rough = raw.Flag("rough"),
                Ground = raw.Flag("ground"),
                EachTime = raw.Flag("each_time"),
                Cover = raw.Number("cover"),
                Encloses = raw.Text("encloses") switch
                {
                    "bars" => Core.Space.Edge.Bars,
                    "solid" => Core.Space.Edge.Wall,
                    _ => Core.Space.Edge.None,
                },
                RingSize = raw.Number("ring_size"),
                Beside = raw.Number("beside"),
                Drifts = raw.Number("drifts"),
                Obscures = obscures,
                BlocksSpellsUpTo = raw.Number("blocks_spells_up_to"),
                OnCaster = raw.Flag("on_caster"),
                Unoccupied = raw.Flag("unoccupied"),

                // a shift's
                Push = raw.Number("push"),
                Teleports = raw.Flag("teleports"),
                Passenger = raw.Flag("passenger"),
                Unseen = raw.Flag("unseen"),
                Rams = raw.Flag("rams"),

                // the rest, one or two each
                Revives = raw.Flag("revives"),
                RestoresAbilities = raw.Flag("restores_abilities"),
                Curses = raw.Flag("curses"),
                EndsForce = raw.Flag("ends_force"),
                Command = command,
                Item = ReadItem(raw, where, problems),
                ExtraTiers = extraTiers,
                Disarms = raw.Flag("disarms"),
                Pinned = raw.Flag("pinned"),
                Banishes = raw.Flag("banishes"),
                RaisesMaximum = raw.Flag("raises_maximum"),
            };
        }

        // one word of the data, or its default when the key is missing; a word that isn't one is
        // refused with `why` and read as the enum's first value
        internal static T Word<T>(JsonElement raw, string key, string otherwise, List<string> problems, string why)
            where T : struct, Enum
        {
            if (!EnumWords.TryParse(raw.Text(key, otherwise), out T value)) problems.Add(why);

            return value;
        }
    }
}
