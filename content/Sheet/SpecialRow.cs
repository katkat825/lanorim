using System;
using System.Collections.Generic;
using System.Linq;
using Content.Classes;
using Content.Spells;
using Core.Localization;

namespace Content.Sheet
{
    // ONE ROW OF THE SHEET'S SPECIAL SECTION, and the feature card it opens (cc_task_cs 2.2): a feature with a button
    // on it that is neither a weapon nor a spell. Was in SheetView.cs; out on its own when the card's words came
    public sealed class SpecialRow
    {
        // `usesMost`: at the hero's level, from the pool it spends (Sacred Weapon spends Channel Divinity's); it was
        // the feature's level-1 number, so a fighter's two Second Winds at level 4 read as one
        public SpecialRow(Feature feature, int usesLeft, int usesMost)
        {
            Feature = feature;
            NameKey = feature.NameKey;
            DescriptionKey = feature.DescriptionKey;
            UsesMost = Math.Max(0, usesMost);
            UsesLeft = UsesMost > 0 ? Math.Clamp(usesLeft, 0, UsesMost) : 0;
            RechargeKey = UsesMost > 0 ? feature.Recharge.UiNameKey(RechargeSubject) : null;
            UseTimeKey = SpellCard.SpendKeyOf(feature.UseTime);
        }

        public Feature Feature { get; }

        public string NameKey { get; }

        public string DescriptionKey { get; }

        // zero is "no limit" - a stance you can switch on whenever
        public int UsesMost { get; }

        public int UsesLeft { get; }

        public bool Available => UsesMost == 0 || UsesLeft > 0;

        // what brings the uses back - "Every use on a short or long rest" - or null when it has no limit
        public string RechargeKey { get; }

        // an action, a bonus action, a reaction or free: the spell card's own words for it
        public string UseTimeKey { get; }

        const string RechargeSubject = "recharge";

        public static IEnumerable<string> Keys() => Enum.GetValues<Recharge>().Select(r => r.UiNameKey(RechargeSubject));
    }
}
