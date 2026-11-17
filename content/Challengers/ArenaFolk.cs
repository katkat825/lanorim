using System;
using System.Collections.Generic;
using System.Linq;
using Content.Monsters;

namespace Content.Challengers
{
    // WHO CAN STEP INTO THE ARENA (cc_task_ca Part 1; Kathleen, 2026-11-16): "v1 won't have non-humanoid challengers",
    // and "goblin is close enough to humanoid. it just needs to be a thinking, intelligent enough being that can
    // voluntarily decide to try to prove it's the best warrior in the universe." A challenger's stat blocks each carry
    // one of these tags. The SRD types the Goblin Warrior as Fey (Goblinoid), and the stat blocks stay SRD-faithful, so
    // the rule is a set of tags rather than a retag: a later stat block that passes her test joins with one word here.
    // tools/import_challengers.py reads this list too, so it lives in one place
    public static class ArenaFolk
    {
        public static readonly IReadOnlyList<string> Tags = new[] { "humanoid", "goblinoid" };

        public static bool Allows(Monster monster) => monster != null && monster.Tags.Any(t => Tags.Contains(t));

        // "ogre isn't a humanoid: v1's challengers are humanoids only", or null when it may fight
        public static string Refusal(Monster monster) =>
            monster == null || Allows(monster)
                ? null
                : $"{monster.Id} isn't a humanoid: v1's challengers are humanoids only (a stat block tagged " +
                  string.Join(" or ", Tags) + ")";
    }
}
