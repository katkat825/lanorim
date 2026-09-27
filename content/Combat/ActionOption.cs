using System;
using System.Collections.Generic;
using Content.Classes;
using Content.Items;
using Core.Characters;
using Core.Combat;
using Core.Magic;

namespace Content.Combat
{
    // ONE BUTTON ON THE ACTION BAR: what it is, what it costs, whether it can be pressed now and,
    // when it cannot, why - as a key the tooltip shows
    public sealed class ActionOption
    {
        public string Id { get; init; } = "";

        public OptionKind Kind { get; init; }

        public string NameKey { get; init; } = "";

        public Spend Cost { get; init; } = Spend.Action;

        public bool Enabled { get; init; }

        public string WhyNotKey { get; init; }

        // 1 to 9 on the keyboard, 0 for the ones past nine (reached by Tab and the mouse)
        public int Hotkey { get; set; }

        public Targeting Targeting { get; init; }

        public int MaxTargets { get; init; } = 1;

        public IReadOnlyList<string> Modes { get; init; } = Array.Empty<string>();

        public IReadOnlyList<DamageType> DamageChoices { get; init; } = Array.Empty<DamageType>();

        public Attack Attack { get; init; }

        // a Shove's choice: "push" five feet, or "prone"
        public string ShoveMode { get; init; } = "";

        public Spell Spell { get; init; }

        public Feature Feature { get; init; }

        public Form Form { get; init; }

        public Item Item { get; init; }

        public override string ToString() =>
            $"{Hotkey}: {Id} ({Cost.ToString().ToLowerInvariant()})" + (Enabled ? "" : $" - {WhyNotKey}");
    }
}
