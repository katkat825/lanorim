using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Magic;
using Core.Words;

namespace Content.Spells
{
    // WHAT A SPELL DOES, IN A FEW WORDS, for the sheet's spell rows (character_sheet_decisions.md, spells: "effect
    // (damage or condition or whatever is applicable)"). Read off the spell's own effects, never written: its damage
    // and type ("8d6 Fire"), else its healing ("Heals 2d8"), else the condition it puts on ("Paralyzed"), else the
    // kind of thing its first effect is ("Protection"). The card's full text is the description's
    public static class SpellCardEffect
    {
        public static readonly string DamageKey = Ui("damage");

        // the damage of a type the caster picks as they cast: Dragon's Breath, Chromatic Orb
        public static readonly string ChosenDamageKey = Ui("damage_chosen");

        public static readonly string HealKey = Ui("heal");

        public static readonly string ConditionKey = Ui("condition");

        // the plain word for a primitive with no number or condition to show
        public static string KindKey(Primitive primitive) =>
            KeyConventions.Key(KeyConventions.UiNs, "spell_kind", primitive.Id(), "name");

        // the key, the dice at this caster level (nothing when there are none) and the damage type or condition it names
        public static (string Key, DiceRoll Dice, string WordKey) Of(Spell spell, int casterLevel)
        {
            SpellEffect hurt = spell.Effects.FirstOrDefault(e => e.Kind is Primitive.Damage or Primitive.Strike &&
                                                                 !e.Amount.IsNothing);

            if (hurt != null)
                return (hurt.ChosenDamageType ? ChosenDamageKey : DamageKey,
                        hurt.AmountAt(spell.Level, spell.Level, casterLevel),
                        hurt.ChosenDamageType ? null : hurt.DamageType.NameKey());

            SpellEffect heal = spell.Effects.FirstOrDefault(e => e.Kind == Primitive.Heal && !e.Amount.IsNothing);

            if (heal != null) return (HealKey, heal.AmountAt(spell.Level, spell.Level, casterLevel), null);

            SpellEffect afflict = spell.Effects.FirstOrDefault(e => e.Condition != Condition.None);

            if (afflict != null) return (ConditionKey, DiceRoll.None, afflict.Condition.NameKey());

            SpellEffect first = spell.Effects.FirstOrDefault();

            return first == null ? (null, DiceRoll.None, null) : (KindKey(first.Kind), DiceRoll.None, null);
        }

        static string Ui(string aspect) => KeyConventions.Key(KeyConventions.UiNs, "spell_effect", aspect, "name");

        public static IEnumerable<string> Keys() =>
            new[] { DamageKey, ChosenDamageKey, HealKey, ConditionKey }
                .Concat(System.Enum.GetValues<Primitive>().Select(KindKey));
    }
}
