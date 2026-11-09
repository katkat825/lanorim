using System;
using System.Collections.Generic;
using System.Linq;
using Core.Tables;

namespace Content.Challengers
{
    // ONE CHALLENGER ON THE ROSTER (cc_task_cg Part 3): a type, a name and a description, its monsters already fitted
    // to the hero's level (ChallengerFit), and the difficulty that came to - or a hand-written special fight, by its
    // encounter entry. Ids and numbers only, so the save keeps it as it is and a reload doesn't reroll it
    public sealed class Challenger
    {
        // what the story fights: <<fight {$challenger}>>. a generated one's is its type, name and description,
        // "goblin_warrior_002_007"; a special's is its encounter entry's id
        public string Id { get; init; } = "";

        // the challenger type, or "" for a special fight
        public string Type { get; init; } = "";

        // the special fight's encounter entry, or "" for a generated one
        public string Entry { get; init; } = "";

        public bool Special => Entry.Length > 0;

        public ChallengerShape Shape { get; init; }

        // which of the type's names and descriptions (1-based, its own first, then the shared lists'); 0 for a special
        public int Name { get; init; }

        public int Description { get; init; }

        // who steps into the arena, as fitted: a special's bands at the most their dice come to
        public IReadOnlyList<Mustered> Group { get; init; } = Array.Empty<Mustered>();

        public EncounterDifficulty Difficulty { get; init; }

        // the group's XP, the SRD's by challenge rating (FightBudget.Xp): what its difficulty came to
        public int Xp { get; init; }

        public int Enemies => Group.Sum(m => m.Count);

        // one opponent, so the fight names it as the challenger ("Grik the Knife" in the turn banner and the log)
        public bool Alone => Enemies == 1;

        // the name and the description as keys: the type's own or a shared list's (set when it's drawn, and saved);
        // a special's are its own two
        public string NameKey
        {
            get => _nameKey ?? (Special ? SpecialKey(Entry, "name") : ChallengerType.Key(Type, "name", Name));
            init => _nameKey = string.IsNullOrEmpty(value) ? null : value;
        }

        public string DescriptionKey
        {
            get => _descriptionKey ??
                   (Special ? SpecialKey(Entry, "description") : ChallengerType.Key(Type, "description", Description));
            init => _descriptionKey = string.IsNullOrEmpty(value) ? null : value;
        }

        readonly string _nameKey;
        readonly string _descriptionKey;

        // a special fight's own keys (cc_task_cg 3.4): challenger.<entry id>.name and .description
        public static string SpecialKey(string entry, string what) =>
            Core.Localization.KeyConventions.Key(Core.Localization.KeyConventions.ChallengerNs, entry, what);

        public static string IdOf(string type, int name, int description) => $"{type}_{name:000}_{description:000}";

        public override string ToString() =>
            $"{Id} ({Difficulty.ToString().ToLowerInvariant()}, {Xp} XP): {string.Join(", ", Group)}";
    }
}
