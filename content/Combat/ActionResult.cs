using System;
using System.Collections.Generic;
using Core.Magic;
using Core.Resolution;
using Core.Rules;
using Core.Space;

namespace Content.Combat
{
    public sealed class ActionResult
    {
        public bool Done { get; init; }

        public string WhyNotKey { get; init; }

        public Blow Blow { get; init; }

        public Casting Casting { get; init; }

        public IReadOnlyList<Cell> Walked { get; init; } = Array.Empty<Cell>();

        public Attempt Attempt { get; init; }

        public static ActionResult No(string why) => new ActionResult { Done = false, WhyNotKey = why };
    }
}
