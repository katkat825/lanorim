using System;
using System.Collections.Generic;
using Core.Space;
using Core.Tables;

namespace Content.Play
{
    // A FIGHT THE STORY CALLED FOR: who, where, and what it leaves behind
    public sealed class FightCall
    {
        public EncounterEntry Entry { get; init; }

        public IReadOnlyList<Mustered> Group { get; init; } = Array.Empty<Mustered>();

        public string MapId { get; init; } = "";

        public MapLayout Map { get; init; }

        public string Loot => Entry?.Loot ?? "";
    }
}
