using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Core.Characters;
using Core.Dice;
using Core.Rules;
using Core.Words;

namespace Content.Schema
{
    // AN ATTACK, wherever one is written: an item's weapon, a statblock's attack, a Wild Shape
    // form's. three readers used to read the same keys three ways - the form's took no on-hit and
    // called its extra damage a 'rider' (cc_task_dedupe-leftovers.md #6). what differs is only
    // what holds the attack: an item names it and says which hand, a statblock names it inside.
    public static class AttackReader
    {
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "damage", "damage_type", "ability", "reach", "range", "long_range", "finesse", "thrown",
            "attack_bonus", "damage_bonus", "adds_ability", "on_hit",
        };

        // what a hit adds: a feature rider's words - 'amount', 'duration' (Strike.Rider)
        public static readonly IReadOnlyList<string> OnHitKeys = new[]
        {
            "amount", "damage_type", "condition", "save", "dc", "max_size", "tag_rules", "duration",
        };

        // the keys are the caller's to check (Keys plus its own); this reads what they say. a
        // basis is the weapon a statblock's attack names ("weapon": "shortbow"): what the attack
        // doesn't write is the weapon's, so a statblock gives only what differs
        public static Attack Read(JsonElement raw, string name, Hand hand, string where, List<string> problems,
                                  Attack basis = null)
        {
            DiceRoll damage = raw.Has("damage") || basis == null ? raw.Dice("damage", problems, where) : basis.Damage;

            if (damage.IsNothing) problems.Add($"{where}: an attack with no damage");

            DamageType type = raw.Has("damage_type") || basis == null
                ? raw.Damage("damage_type", problems, where)
                : basis.DamageType;

            if (type == DamageType.None) problems.Add($"{where}: an attack with no damage_type");

            Ability ability = raw.Ability("ability", problems, where) ?? basis?.Ability ?? Ability.Strength;

            return new Attack(name, damage, type,
                              ability,
                              true,
                              raw.Number("reach", basis?.Reach ?? 1),
                              raw.Number("range", basis?.Range ?? 0),
                              raw.Number("long_range", basis?.LongRange ?? 0),
                              hand,
                              raw.Flag("finesse", basis?.Finesse ?? false),
                              raw.Number("attack_bonus", basis?.AttackBonus ?? 0),
                              raw.Number("damage_bonus", basis?.DamageBonus ?? 0),
                              // a flat roll, like a priest's Radiant Flame: 11 (2d10)
                              raw.Flag("adds_ability", basis?.AddsAbilityToDamage ?? true))
            {
                OnHit = raw.Has("on_hit") || basis == null ? OnHit(raw, name, where, problems) : basis.OnHit,
                // "Melee or Ranged Attack Roll": melee within reach, thrown past it
                Thrown = raw.Flag("thrown", basis?.Thrown ?? false),

                // a weapon's own properties (ItemReader.WeaponKeys); nothing on a claw
                Light = raw.Flag("light", basis?.Light ?? false),
                Heavy = raw.Flag("heavy", basis?.Heavy ?? false),
                Versatile = raw.Has("versatile") ? raw.Dice("versatile", problems, where) : basis?.Versatile ?? default,
                Category = raw.Text("category") ?? basis?.Category ?? "",
            };
        }

        static IReadOnlyList<Rider> OnHit(JsonElement raw, string name, string where, List<string> problems)
        {
            if (!raw.Has("on_hit")) return System.Array.Empty<Rider>();

            JsonElement hit = raw.GetProperty("on_hit");
            Size? maxSize = null;

            Keyed.OnlyKnown(hit, OnHitKeys, $"{where} on_hit", problems);

            if (!string.IsNullOrEmpty(hit.Text("max_size")))
            {
                if (EnumWords.TryParse(hit.Text("max_size"), out Size read)) maxSize = read;
                else problems.Add($"{where}: '{hit.Text("max_size")}' is not a size");
            }

            if (!EnumWords.TryParse(hit.Text("duration", "instant"), out Duration lasts) ||
                lasts != Duration.Instant && lasts != Duration.NextTurnEnd)
                problems.Add($"{where}: an on-hit condition's 'duration' is 'next_turn_end' " +
                             "(the end of the target's next turn), or nothing for until it ends");

            var rider = new Rider(name + "_hit", hit.Dice("amount", problems, where),
                                  hit.Damage("damage_type", problems, where),
                                  hit.Condition("condition", problems, where))
            {
                Save = hit.Ability("save", problems, where),
                Dc = hit.Number("dc"),
                MaxSize = maxSize,
                TagRules = hit.TagRuleList("tag_rules", problems, where),
                Duration = lasts,
            };

            if (rider.UntilTargetsNextTurn && rider.Condition == Condition.None)
                problems.Add($"{where}: an on-hit 'duration' needs a condition to end");

            if (rider.TagRules.Any(r => r.Outcome != TagOutcome.Only && r.Outcome != TagOutcome.Untouched))
                problems.Add($"{where}: an on-hit rider's 'tag_rules' are 'only' or 'untouched'");

            if (rider.Save.HasValue && rider.Dc <= 0)
                problems.Add($"{where}: an on-hit save with no 'dc'");

            if (rider.Amount.IsNothing && rider.Condition == Condition.None)
                problems.Add($"{where}: an 'on_hit' that adds nothing");

            return new[] { rider };
        }
    }
}
