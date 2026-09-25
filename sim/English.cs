using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Content.Classes;
using Content.Items;
using Content.Monsters;
using Content.Schema;
using Content.Species;
using Content.Spells;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Magic;
using Core.Resolution;

namespace Sim
{
    // scaffolds the English locale: one row per key the engine emits, with a first-pass string.
    // this is a dev tool, not a runtime path - it writes game/locale/game.csv once and the CSV is
    // the source of truth from then on. rows that already have text are kept exactly as they are,
    // so running it again only adds what is missing.
    public static class English
    {
        public static string Draft(string existingCsv)
        {
            SpellBook spells = SpellBook.Srd();

            IReadOnlyDictionary<string, string> already = Locale.Read(existingCsv ?? "");

            // EVERY ROW THAT IS ALREADY THERE STAYS, whether this tool knows how to write it or
            // not. The Godot layer's keys - the captions, what each key on the keyboard does -
            // are hand-written, because sim cannot see them: it does not reference Godot, and that
            // is the whole point of sim. A scaffolder that only wrote what it understood would eat
            // them on every run.
            var lines = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> row in already)
                if (!string.IsNullOrWhiteSpace(row.Value))
                    lines[row.Key] = row.Value;

            foreach (string key in EngineKeys.Sorted(spells))
                if (!lines.ContainsKey(key))
                    lines[key] = For(key, spells);

            return Locale.Write(lines.Select(r => (r.Key, r.Value)));
        }

        static string For(string key, SpellBook spells)
        {
            string[] parts = key.Split('.');

            string ns = parts[0];
            string subject = parts.Length > 1 ? parts[1] : "";
            string aspect = parts.Length > 2 ? parts[2] : "";

            bool description = aspect == "description";

            switch (ns)
            {
                case KeyConventions.AbilityNs:
                    return aspect == "short" ? Short(subject) : Ability(subject);

                case KeyConventions.SkillNs:
                case KeyConventions.DamageNs:
                case KeyConventions.DifficultyNs:
                    return Title(subject);

                case KeyConventions.ConditionNs:
                    return description ? ConditionText(subject) : Title(subject);

                case KeyConventions.SpellNs:
                    return description ? Summary(spells.Find(subject)) : Title(subject);

                case KeyConventions.ItemNs:
                    return description
                        ? ItemText(subject)
                        : Title(subject.StartsWith("armor_") ? subject.Substring(6) : subject);

                case KeyConventions.FeatureNs:
                    return description ? FeatureText(subject) : Title(subject);

                case KeyConventions.ClassNs:
                    return description ? ClassText(subject) : Title(subject);

                case KeyConventions.SpeciesNs:
                    return description ? SpeciesText(subject) : Title(subject);

                case KeyConventions.BackgroundNs:
                    return description ? BackgroundText(subject) : Title(subject);

                case KeyConventions.ConsequenceNs:
                    return aspect == "line" ? ConsequenceLine(subject) : Title(subject);

                case KeyConventions.MonsterNs:
                    return description ? MonsterText(subject) : Title(subject);

                case KeyConventions.MerchantNs:
                    return MerchantLine(subject);

                default:
                    return Title(subject);
            }
        }

        static readonly Library Shelf = Library.Srd();

        static string MerchantLine(string which) => which switch
        {
            "greeting" => "Have a look. Everything you see is for sale.",
            "not_stocked" => "I don't carry that.",
            "no_gold" => "Come back when you can pay for it.",

            // inventory_decisions.md asks for this line by name, and a test holds it to existing
            "pack_full" => "You couldn't carry another thing. Make some room first.",
            "not_for_you" => "That's no use to someone like you.",
            "nothing_to_sell" => "You haven't got one of those.",
            "equipped" => "You're wearing that. Sure you want to part with it?",
            _ => Title(which),
        };

        // the narrator's line when the pool is drawn from. these are the only strings in the
        // scaffolder that are prose rather than a summary, because a consequence IS its line
        static string ConsequenceLine(string id) => id switch
        {
            "stubbed_toe" => "You stub your toe on something you did not see. It hurts more than it should.",
            "wrenched_shoulder" => "Something in your shoulder gives with an unpleasant click.",
            "that_took_it_out_of_you" => "That took more out of you than it looked like it would.",
            "rattled" => "You are rattled, and it shows.",
            "aching_arms" => "Your arms ache. They will keep aching until you rest.",
            "a_nasty_landing" => "Your footing goes, and you land badly.",
            "something_in_the_air" => "There is something in the air here, and you have breathed it.",
            "dropped_your_purse" => "Your purse is lighter than it was. You do not hear it fall.",
            "a_bad_feeling" => "Nothing happens. You have a bad feeling about that all the same.",

            "a_pouch_on_the_floor" => "As you turn away, your eye catches a pouch on the floor. Nobody claims it.",
            "that_went_well" => "That went well, and you carry it in your shoulders.",
            "steady_hands" => "Your hands have not felt this steady in weeks.",
            "clear_headed" => "Something clicks, and the rest of it looks simpler than it did.",
            "second_breath" => "You find a second breath you did not know you had.",
            "back_on_your_feet" => "You come up out of it already standing.",
            "a_good_omen" => "Nothing happens. It feels, somehow, like a good sign.",

            _ => Title(id) + ".",
        };

        static string MonsterText(string id)
        {
            Monster monster = Shelf.Bestiary.Find(id);

            if (monster == null) return "";

            var said = new List<string>
            {
                $"{monster.HitPoints} hit points",
                $"Armor Class {monster.ArmorClass}",
                $"speed {monster.Speed} feet",
            };

            foreach (Attack attack in monster.Attacks)
                said.Add($"{Title(attack.Id)} for {attack.Damage} {attack.DamageType.Id()}");

            if (monster.Multiattack > 1) said.Add($"attacks {monster.Multiattack} times a turn");

            foreach (var defense in monster.Defenses)
                said.Add(defense.Value switch
                {
                    Defense.Immune => $"takes no {defense.Key.Id()} damage",
                    Defense.Resistant => $"halves {defense.Key.Id()} damage",
                    Defense.Vulnerable => $"takes double {defense.Key.Id()} damage",
                    _ => "",
                });

            return Sentence(said.Where(s => s.Length > 0).ToList());
        }

        static string ItemText(string id)
        {
            Item item = Shelf.Items.Find(id);

            if (item == null) return "";

            var said = new List<string>();

            if (item.Attack != null)
                said.Add($"{item.Attack.Damage} {item.Attack.DamageType.Id()} damage" +
                         (item.Attack.IsRanged
                              ? $", range {item.Attack.Range}"
                              : item.Attack.Reach > 1 ? ", reach 2" : "") +
                         (item.Attack.Finesse ? ", finesse" : ""));

            if (item.Armor.HasValue)
            {
                ArmorProfile armor = item.Armor.Value;

                said.Add($"Armor Class {armor.BaseArmorClass}" +
                         (armor.Weight == ArmorWeight.Heavy ? ", no Dexterity"
                          : armor.Weight == ArmorWeight.Medium ? ", up to +2 Dexterity"
                          : ", plus Dexterity"));

                if (armor.StealthDisadvantage) said.Add("noisy: Stealth is at disadvantage");
            }

            if (!item.Heals.IsNothing) said.Add($"restores {item.Heals} hit points");

            foreach (Boon boon in item.Boons) said.Add(BoonText(boon));

            if (item.Classes.Count > 0)
                said.Add("for a " + string.Join(" or ", item.Classes.Select(Title)));

            if (item.MinimumLevel > 1) said.Add($"level {item.MinimumLevel} and above");

            return said.Count == 0 ? $"{item.Cost} gold." : Sentence(said) + $" {item.Cost} gold.";
        }

        static string BoonText(Boon boon)
        {
            var rolls = new List<string>();

            if (boon.Attacks) rolls.Add("attack rolls");
            if (boon.Saves) rolls.Add("saving throws");
            if (boon.Checks)
                rolls.Add(boon.Skill == Skill.None ? "ability checks"
                                                   : Title(boon.Skill.Id()) + " checks");
            if (boon.Damage) rolls.Add("damage");
            if (boon.ArmorClass != 0) rolls.Add("Armor Class");

            string amount = boon.Dice.IsNothing ? $"{boon.Flat:+0;-0}" : "+" + boon.Dice;

            return rolls.Count == 0 ? amount : $"{amount} to {string.Join(" and ", rolls)}";
        }

        static string FeatureText(string id)
        {
            Feature feature = Shelf.Classes.SelectMany(c => c.Features)
                                   .Concat(Shelf.Species.SelectMany(s => s.Features))
                                   .FirstOrDefault(f => f.Id == id);

            if (feature == null) return "";

            if (feature.Note.Length > 0 && feature.Trait == Trait.Narrate)
                return Capitalised(feature.Note) + ".";

            string what = feature.Trait switch
            {
                Trait.Rider =>
                    (feature.Amount.IsNothing ? "" : $"{feature.AmountAt(feature.Level)} extra " +
                        (feature.DamageType == DamageType.None
                             ? "damage" : feature.DamageType.Id() + " damage")) +
                    (feature.Condition == Condition.None
                         ? "" : (feature.Amount.IsNothing ? "" : ", and ") +
                                $"leaves the target {feature.Condition.Id()}") +
                    feature.When switch
                    {
                        When.WithAdvantage => " when you attack with the advantage",
                        When.OnCritical => " on a critical hit",
                        When.WhenSpent => " when you spend the power to do it",
                        _ => " on a hit",
                    },

                Trait.Stance => "while it is up, " + BoonText(feature.BoonFor(feature.Level)),

                Trait.Recovery =>
                    $"restores {feature.AmountAt(feature.Level)}" +
                    (feature.Flat != 0 ? $" plus {feature.Flat}" : "") + " hit points",

                Trait.ActionGrant =>
                    feature.Uses > 0
                        ? "one extra action, once between rests"
                        : "an extra action every round",

                Trait.Nimble =>
                    "your bonus action can " +
                    string.Join(", ", Manoeuvres.All.Where(m => feature.Manoeuvres.HasFlag(m))
                                                .Select(m => m.Id())
                                                .Select(Title)) +
                    ", as well as whatever else it does",

                Trait.UnarmoredDefense =>
                    $"with no armor on, your Armor Class is 10 plus Dexterity plus " +
                    $"{Ability(feature.Ability.Value.Id())}",

                Trait.Expertise => $"double your proficiency on {feature.Count} skills",

                Trait.Resistance =>
                    feature.Defense == Defense.Immune
                        ? $"{feature.DamageType.Id()} damage does not touch you"
                        : $"{feature.DamageType.Id()} damage is halved",

                Trait.Spellcasting =>
                    $"you cast with {Ability(feature.Ability.Value.Id())}, paying with " +
                    (feature.Progression == Core.Magic.CasterProgression.Half
                        ? "half a caster's spell slots or spell points"
                        : "spell slots or spell points") +
                    ", whichever you chose, and both come back on a long rest",

                Trait.Speed => $"{feature.Flat} more feet of movement",

                Trait.Training => TrainingText(feature),

                Trait.DeathIntercept =>
                    $"once between rests, when you would fall, stay up on {Math.Max(1, feature.Flat)} hit point",

                Trait.Shape => $"{feature.Count} shapes you can take, {feature.Uses} times between rests",

                _ => "the narrator decides what this does",
            };

            string text = Capitalised(what) + ".";

            if (feature.Uses > 0 && feature.Trait != Trait.ActionGrant &&
                feature.Trait != Trait.DeathIntercept && feature.Trait != Trait.Shape)
                text += $" {feature.Uses} times between rests.";

            return text;
        }

        static string TrainingText(Feature feature)
        {
            var said = new List<string>();

            foreach (Skill skill in feature.Skills)
                said.Add(feature.Flat != 0
                             ? $"{feature.Flat:+0;-0} to {Title(skill.Id())} checks"
                             : $"you are trained in {Title(skill.Id())}");

            foreach (Ability save in feature.Saves)
                said.Add(feature.Flat != 0
                             ? $"{feature.Flat:+0;-0} to {Ability(save.Id())} saving throws"
                             : $"you are trained in {Ability(save.Id())} saving throws");

            return said.Count == 0 ? "a small edge" : string.Join(", ", said);
        }

        static string ClassText(string id)
        {
            CharacterClass cls = Shelf.Class(id);

            if (cls == null) return "";

            var said = new List<string>
            {
                $"hit die {cls.HitDie.Label()}",
                "saves with " + string.Join(" and ", cls.Saves.Select(a => Ability(a.Id()))),
            };

            if (cls.ArmorTraining.Count > 0)
                said.Add("trained in " +
                         string.Join(", ", cls.ArmorTraining.Select(w => w.Id())) + " armor" +
                         (cls.Shields ? " and shields" : ""));

            if (cls.Casts) said.Add($"casts with {Ability(cls.CastingAbility.Value.Id())}");

            said.Add($"{cls.SkillPicks} skills of your choosing");

            return Sentence(said);
        }

        static string SpeciesText(string id)
        {
            Kind kind = Shelf.Kind(id);

            if (kind == null) return "";

            var said = new List<string> { $"speed {kind.Speed} feet" };

            foreach (Feature feature in kind.Features.Take(4)) said.Add(Title(feature.Id));

            if (kind.Lineages.Count > 0)
                said.Add("choose a lineage: " +
                         string.Join(", ", kind.Lineages.Select(Title)));

            return Sentence(said);
        }

        static string BackgroundText(string id)
        {
            Background background = Shelf.Background(id);

            if (background == null) return "";

            return Sentence(new List<string>
            {
                "trained in " + string.Join(" and ",
                                            background.Skills.Select(s => Title(s.Id()))),
                "+2 and +1 to spend on " +
                string.Join(", ", background.Abilities.Select(a => Ability(a.Id()))),
                $"{background.Gold} gold to start",
            });
        }

        static string Sentence(List<string> parts)
        {
            if (parts.Count == 0) return "";

            return Capitalised(string.Join("; ", parts)) + ".";
        }

        static string Capitalised(string text) =>
            text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        static string Ability(string id) => id switch
        {
            "str" => "Strength",
            "dex" => "Dexterity",
            "con" => "Constitution",
            "int" => "Intelligence",
            "wis" => "Wisdom",
            "cha" => "Charisma",
            _ => Title(id),
        };

        static string Short(string id) => id.ToUpperInvariant();

        static string ConditionText(string id) => id switch
        {
            "prone" => "You are on the ground. Your attacks at anything further than arm's reach " +
                       "are at disadvantage, and anything that reaches you has the advantage. " +
                       "Standing up costs half your movement.",
            "poisoned" => "Your attacks and your ability checks are at disadvantage.",
            "stunned" => "You cannot act. Strength and Dexterity saving throws fail automatically.",
            "frightened" => "Your attacks and your ability checks are at disadvantage.",
            "restrained" => "You cannot move. Your attacks are at disadvantage and attacks " +
                            "against you have the advantage.",
            "grappled" => "You cannot move.",
            "unconscious" => "You are down. You cannot act, and attacks against you have the " +
                             "advantage.",
            _ => Title(id) + ".",
        };

        // a one-line mechanical summary built from the spell's own primitives, so a card is never
        // blank. Kathleen replaces these with SRD prose - they are a scaffold, not the copy.
        static string Summary(Spell spell)
        {
            if (spell == null) return "";

            var said = new List<string>();

            foreach (SpellEffect effect in spell.Effects)
            {
                string plus = effect.AddsModifier ? " plus your spellcasting modifier" : "";

                string one = effect.Kind switch
                {
                    Primitive.Damage when effect.SlaysAtOrBelow > 0 =>
                        $"a creature with {effect.SlaysAtOrBelow} hit points or fewer dies; " +
                        $"anything else takes {effect.Amount} {effect.DamageType.Id()} damage",

                    Primitive.Damage =>
                        $"{effect.Amount}{plus} {effect.DamageType.Id()} damage" +
                        (effect.Beams ? ", more beams as you level" : "") +
                        (effect.Repeats ? ", and again on later turns while you hold it" : ""),

                    Primitive.Heal => $"restores {effect.Amount}{plus} hit points",

                    Primitive.Ward => $"grants {effect.Amount} temporary hit points",

                    Primitive.Afflict => $"leaves the target {effect.Condition.Id()}",

                    Primitive.Relieve => $"ends the {effect.Condition.Id()} condition",

                    Primitive.Sway => Swayed(effect),

                    Primitive.Zone when effect.Reach == Reach.Square =>
                        $"fills a {effect.Length * 5}-foot square",

                    Primitive.Zone when effect.Reach == Reach.Around =>
                        $"surrounds you for {effect.Radius * 5} feet",

                    Primitive.Zone => $"fills {effect.Radius * 5} feet around the point",

                    Primitive.Shift when effect.Reach == Reach.Zone =>
                        effect.Length > 0 ? $"moves it up to {effect.Length * 5} feet on a later turn"
                                          : "moves it on a later turn",

                    Primitive.Shift => "moves you elsewhere",

                    Primitive.Illuminate => $"lights {effect.Radius} squares",

                    Primitive.Reveal => "shows you what is hidden",

                    Primitive.Dispel => "ends the spells on the target",

                    Primitive.Counter => "stops a spell as it is cast",

                    Primitive.Summon => "calls something to your side",

                    _ => "its effect is told by the narrator",
                };

                // a zone already said how big it is
                if (effect.Kind != Primitive.Zone) one += Shape(effect);

                if (effect.Pulses != Pulses.None)
                    one += " " + Pulsed(effect.Pulses);

                if (effect.Kind == Primitive.Zone && effect.Rough) one += ", difficult terrain";

                if (effect.Escape.HasValue)
                    one += $", {Ability(effect.Escape.Value.Id())} check as an action to break free";

                if (effect.RepeatSave) one += ", saving again at the end of each turn";

                if (one.Trim().Length == 0) continue;

                if (effect.Save.HasValue && effect.SameSave)
                    one += effect.OnSave == OnSave.Half
                        ? ", the same save for half"
                        : ", on the same failed save";
                else if (effect.Save.HasValue)
                    one += $", {effect.Save.Value.Id()} save " +
                           (effect.OnSave == OnSave.Half ? "for half" : "to avoid it");

                if (effect.AttackRoll) one += ", on a hit";

                said.Add(one);
            }

            if (said.Count == 0) said.Add("its effect is told by the narrator");

            var text = new StringBuilder();

            text.Append(spell.IsCantrip ? "Cantrip. " : $"Level {spell.Level}. ");

            text.Append(spell.CastingTime switch
            {
                CastingTime.BonusAction => "Bonus action. ",
                CastingTime.Reaction => spell.Trigger switch
                {
                    Core.Combat.Trigger.Hit => "Reaction, when an attack hits you. ",
                    Core.Combat.Trigger.Cast => "Reaction, when you see a spell cast. ",
                    Core.Combat.Trigger.Damaged => "Reaction, when you are hurt. ",
                    _ => "Reaction. ",
                },
                _ => "",
            });

            text.Append(char.ToUpperInvariant(said[0][0])).Append(said[0].Substring(1));

            for (int i = 1; i < said.Count; i++) text.Append("; ").Append(said[i]);

            text.Append('.');

            if (spell.Concentration) text.Append(" Requires concentration.");

            if (spell.Approximated)
                text.Append(" (v1 ships a bounded version of this spell.)");

            return text.ToString();
        }

        static string Pulsed(Pulses pulses)
        {
            var when = new List<string>();

            if ((pulses & Pulses.Appear) != 0) when.Add("when it appears");
            if ((pulses & Pulses.Enter) != 0) when.Add("when a creature enters it");
            if ((pulses & Pulses.StartTurn) != 0) when.Add("when a creature starts its turn there");
            if ((pulses & Pulses.EndTurn) != 0) when.Add("when a creature ends its turn there");
            if ((pulses & Pulses.EachSquare) != 0) when.Add("for every 5 feet moved through it");

            return string.Join(", ", when);
        }

        // where an area lands, in feet, because feet are what a player reads on a card
        static string Shape(SpellEffect effect) => effect.Reach switch
        {
            Reach.Line => $" in a {effect.Length * 5}-foot line",
            Reach.Cone => $" in a {effect.Length * 5}-foot cone",
            Reach.Cube => $" in a {effect.Length * 5}-foot cube",
            Reach.Square => $" in a {effect.Length * 5}-foot square",
            Reach.Burst when effect.Kind == Primitive.Damage || effect.Kind == Primitive.Afflict =>
                effect.Points > 1
                    ? $" in {effect.Points} {effect.Radius * 5}-foot bursts"
                    : $" in a {effect.Radius * 5}-foot burst",
            Reach.Around when effect.Kind == Primitive.Damage =>
                $" to everything within {effect.Radius * 5} feet of you",
            _ => "",
        };

        static string Swayed(SpellEffect effect)
        {
            var said = new List<string>();

            if (effect.Sway != 0 || !effect.SwayDice.IsNothing) said.Add(Numbers(effect));

            if ((effect.Leans & Leans.AdvantageOnAttacks) != 0) said.Add("advantage on attack rolls");
            if ((effect.Leans & Leans.DisadvantageOnAttacks) != 0)
                said.Add(effect.Once ? "disadvantage on its next attack roll"
                                     : "disadvantage on attack rolls");
            if ((effect.Leans & Leans.AdvantageAgainst) != 0)
                said.Add(effect.Once ? "the next attack roll against it has advantage"
                                     : "attack rolls against it have advantage");
            if ((effect.Leans & Leans.DisadvantageAgainst) != 0)
                said.Add("attack rolls against it have disadvantage");
            if ((effect.Leans & Leans.AdvantageOnChecks) != 0) said.Add("advantage on ability checks");
            if ((effect.Leans & Leans.DisadvantageOnChecks) != 0)
                said.Add(effect.ChosenAbility ? "disadvantage on checks with an ability you choose"
                                              : "disadvantage on ability checks");

            if (!effect.Mark.IsNothing)
                said.Add($"{effect.Mark} {effect.DamageType.Id()} damage whenever you hit it " +
                         "with an attack roll");

            if (effect.UnarmoredBase > 0)
                said.Add($"Armor Class {effect.UnarmoredBase} plus Dexterity while wearing no armor");

            if (effect.Resists.Count > 0)
                said.Add("resistance to " + string.Join(", ", effect.Resists.Select(t => t.Id())) +
                         " damage");

            if (effect.Speed != 0) said.Add($"{effect.Speed:+0;-0} feet of speed");

            if (effect.NoOpportunityAttacks) said.Add("it can't make opportunity attacks");

            if (effect.Exposes) said.Add("it gains nothing from being unseen");

            if (effect.EndsOnAttack) said.Add("until it attacks or casts");

            return string.Join(", ", said);
        }

        static string Numbers(SpellEffect effect)
        {
            string amount = effect.SwayDice.IsNothing
                ? $"{effect.Sway:+0;-0}"
                : (effect.Sway == 0 ? "+" : $"{effect.Sway:+0;-0} and ") + effect.SwayDice;

            var rolls = new List<string>();

            if ((effect.Touches & Sways.Attacks) != 0) rolls.Add("attack rolls");
            if ((effect.Touches & Sways.Saves) != 0) rolls.Add("saving throws");
            if ((effect.Touches & Sways.Checks) != 0)
                rolls.Add(effect.ChosenSkill ? "checks with a skill you choose"
                          : effect.Skill == Skill.None
                              ? "ability checks"
                              : Title(effect.Skill.Id()) + " checks");
            if ((effect.Touches & Sways.Damage) != 0) rolls.Add("damage");
            if ((effect.Touches & Sways.ArmorClass) != 0) rolls.Add("Armor Class");

            return $"{amount} to {string.Join(" and ", rolls)}";
        }

        static readonly HashSet<string> Small = new HashSet<string>
        {
            "of", "the", "a", "an", "and", "to", "in", "on", "at", "for",
        };

        static string Title(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";

            string[] words = id.Split('_');

            var text = new StringBuilder();

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];

                if (word.Length == 0) continue;

                if (text.Length > 0) text.Append(' ');

                if (i > 0 && Small.Contains(word))
                {
                    text.Append(word);
                    continue;
                }

                text.Append(char.ToUpperInvariant(word[0])).Append(word.Substring(1));
            }

            // "hunters mark" reads wrong; the apostrophe is the one thing an id cannot carry
            return text.ToString()
                       .Replace("Hunters Mark", "Hunter's Mark")
                       .Replace("Nearly Impossible", "Nearly Impossible");
        }
    }
}
