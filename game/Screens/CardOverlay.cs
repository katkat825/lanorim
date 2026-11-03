using System;
using System.Collections.Generic;
using Content.Screens;
using Content.Sheet;
using Content.Spells;
using Game.Access;
using Godot;

namespace Game.Screens
{
    // A CARD YOU CAN READ (cc_task_cs Part 2): a spell's, or a feature's in the same frame. The name, a band with the
    // level and the school (or what using the feature takes), the card's facts label by label, and its words, in EB
    // Garamond (the theme's CardTitle and CardLabel) on the theme's parchment. Every word is the card's
    // (content/Spells/SpellCard.cs) or the sheet's row (SpecialRow), so the sheet, creation, the HUD and the spells page
    // say the same thing about one spell.
    //
    // Shown over whatever is up, on a layer of its own, so the sheet, creation's spell page, the HUD and the book's
    // Spells page all open it the same way (Show). Esc or Close puts it away. How it looks is Kathleen's
    public partial class CardOverlay : Overlay
    {
        readonly string _title;
        readonly IReadOnlyList<string> _band;
        readonly IReadOnlyList<(string Label, string Value)> _facts;
        readonly IReadOnlyList<string> _words;

        CardOverlay(string title, IReadOnlyList<string> band, IReadOnlyList<(string, string)> facts, IReadOnlyList<string> words)
        {
            _title = title;
            _band = band;
            _facts = facts;
            _words = words;
            Width = 470;
        }

        // the layer every card goes up on: over the table's own UI and the launch screen, under the access desk's toast
        const int Layer = 60;

        public static CardOverlay Show(Node anywhere, CardOverlay card)
        {
            var layer = new CanvasLayer { Name = "Card", Layer = Layer };
            layer.AddChild(card);
            card.Closed += () => layer.QueueFree();
            anywhere.GetTree().Root.AddChild(layer);

            Narrate(card);
            return card;
        }

        public static CardOverlay Show(Node anywhere, SpellCard spell) => Show(anywhere, Of(spell));

        public static CardOverlay Show(Node anywhere, SpecialRow feature) => Show(anywhere, Of(feature));

        // --- a spell's card ------------------------------------------------------------------------------------

        public static CardOverlay Of(SpellCard card)
        {
            string range = card.RangeKind == RangeKind.Feet ? Ui.Say(card.RangeKey, card.RangeFeet) : Ui.Say(card.RangeKey);

            if (card.AreaKey != null) range += $" ({Ui.Say(card.AreaKey, card.AreaFeet)})";

            var facts = new List<(string, string)>
            {
                (Ui.Say(SheetWords.CardCastingTimeKey), Ui.Say(card.CastingTimeKey)),
                (Ui.Say(SheetWords.CardRangeKey), range),
            };

            // a campaign's own spell may have no components written; the SRD's all do
            if (Ui.Text.Has(card.ComponentsKey))
                facts.Add((Ui.Say(SheetWords.CardComponentsKey), Ui.Say(card.ComponentsKey)));

            facts.Add((Ui.Say(SheetWords.CardDurationKey), Ui.Say(card.DurationKey)));
            facts.Add((Ui.Say(SheetWords.CardRollKey), AccessDesk.Words(SheetReading.Roll(card))));
            facts.Add((Ui.Say(SheetWords.CardEffectKey), AccessDesk.Words(SheetReading.Effect(card))));

            if (card.CostKey != null) facts.Add((Ui.Say(SheetWords.CardCostKey), Ui.Say(card.CostKey, card.CostAmount)));

            var band = new List<string> { Ui.Say(card.LevelKey, card.Level), Ui.Say(card.SchoolKey) };

            if (card.Concentration) band.Add(Ui.Say(SheetWords.NeedsConcentrationKey));
            if (card.Ritual) band.Add(Ui.Say(SheetWords.CardRitualKey));

            var words = new List<string> { Ui.Say(card.DescriptionKey) };

            // what the spell is to a party of one, and that it's a Lanorim version: the rules' own notes
            foreach (string key in card.SoloKeys) words.Add(Ui.Say(key));

            if (card.Adapted) words.Add(Ui.Say(SpellCard.AdaptedKey));

            if (card.NotCastableKey != null) words.Add(Ui.Say(card.NotCastableKey));

            return new CardOverlay(Ui.Say(card.NameKey), band, facts, words);
        }

        // --- a feature's card, in the same frame --------------------------------------------------------------

        public static CardOverlay Of(SpecialRow row)
        {
            var facts = new List<(string, string)>
            {
                (Ui.Say(SheetWords.CardUseKey), Ui.Say(row.UseTimeKey)),
                (Ui.Say(SheetWords.CardUsesKey), AccessDesk.Words(SheetReading.Uses(row))),
            };

            if (row.RechargeKey != null) facts.Add((Ui.Say(SheetWords.CardRechargeKey), Ui.Say(row.RechargeKey)));

            return new CardOverlay(Ui.Say(row.NameKey), new[] { Ui.Say(row.UseTimeKey) }, facts,
                                   new[] { Ui.Say(row.DescriptionKey) });
        }

        // --- drawn -----------------------------------------------------------------------------------------------

        protected override void Draw()
        {
            Label title = Ui.Plain(_title);
            title.ThemeTypeVariation = "CardTitle";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            Body.AddChild(title);

            // the band: the level and the school, dark across the card
            var band = new Label
            {
                Text = string.Join("  ·  ", _band),
                ThemeTypeVariation = "HudLabel",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            Body.AddChild(Ui.Panel(band, "DarkPanel"));

            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 16);
            grid.AddThemeConstantOverride("v_separation", 4);

            foreach ((string label, string value) in _facts)
            {
                Label name = Ui.Plain(label);
                name.ThemeTypeVariation = "CardLabel";
                name.AutowrapMode = TextServer.AutowrapMode.Off;
                name.Modulate = new Color(1, 1, 1, 0.7f);

                Label says = Ui.Plain(value);
                says.ThemeTypeVariation = "CardLabel";
                says.SizeFlagsHorizontal = SizeFlags.ExpandFill;

                grid.AddChild(name);
                grid.AddChild(says);
            }

            Body.AddChild(grid);
            Body.AddChild(new HSeparator());

            foreach (string line in _words)
            {
                Label words = Ui.Plain(line);
                words.ThemeTypeVariation = "CardLabel";
                Body.AddChild(words);
            }

            Body.AddChild(Ui.Button(SheetWords.CloseKey, Close));
        }

        // the card, read out when the reader is on: its name, the band, each fact with its label, its words
        static void Narrate(CardOverlay card)
        {
            var lines = new List<string> { card._title, string.Join(", ", card._band) };

            foreach ((string label, string value) in card._facts) lines.Add($"{label}: {value}");

            lines.AddRange(card._words);

            AccessDesk.Instance?.Narrator.Say(Spoken.Of(lines.ToArray()));
        }

        // what a probe reads back: the title and every line of the card
        public string Text => string.Join("\n", new[] { _title, string.Join(" · ", _band) }) + "\n" +
                              string.Join("\n", System.Linq.Enumerable.Select(_facts, f => $"{f.Label}: {f.Value}")) + "\n" +
                              string.Join("\n", _words);
    }
}
