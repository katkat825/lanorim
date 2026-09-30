using System.Collections.Generic;
using Content.Spells;

namespace Game.Screens
{
    // ONE SPELL CARD, IN WORDS: its level and school; its casting time, range (and area) and how long
    // it lasts; what it does; and whether it is a Lanorim version of the SRD's spell. Every word comes
    // from the card (content/Spells/SpellCard.cs), so creation's description and the sheet say the
    // same thing about a spell (cc_task_ui-issues-9-30.md 3.2)
    public static class SpellCardText
    {
        const string Dot = "  ·  ";

        public static string Of(SpellCard card)
        {
            if (card == null) return "";

            string range = card.RangeKind == RangeKind.Feet ? Ui.Say(card.RangeKey, card.RangeFeet) : Ui.Say(card.RangeKey);

            if (card.AreaKey != null) range += $" ({Ui.Say(card.AreaKey, card.AreaFeet)})";

            var lines = new List<string>
            {
                Ui.Say(card.LevelKey, card.Level) + Dot + Ui.Say(card.SchoolKey),
                Ui.Say(card.CastingTimeKey) + Dot + range + Dot + Ui.Say(card.DurationKey),
                Ui.Say(card.DescriptionKey),
            };

            if (card.Adapted) lines.Add(Ui.Say(SpellCard.AdaptedKey));

            return string.Join("\n", lines);
        }
    }
}
