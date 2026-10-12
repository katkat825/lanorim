using System;
using System.Collections.Generic;

namespace Content.Maps
{
    // WHAT A MAP HAS BESIDES ITS RULES, for the board to draw: the props standing on it, and its look (each square's
    // ground, each wall's style: cc_task_l Part 2). A MapLayout (the rules' map) has no use for either, so a campaign
    // keeps them beside it, by map id (Package.Scenery)
    public sealed record MapScenery(IReadOnlyList<Prop> Props, MapLook Look)
    {
        public static MapScenery Of(MapDraft draft) => new(new List<Prop>(draft.Props), draft.Look());

        public static readonly MapScenery None = new(Array.Empty<Prop>(), null);
    }
}
