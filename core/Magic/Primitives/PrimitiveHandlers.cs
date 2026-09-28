using System;
using System.Collections.Generic;
using System.Linq;
using Core.Words;

namespace Core.Magic
{
    // THE ONE TABLE of primitive handlers. a new primitive is a new handler file and one line here
    public static class PrimitiveHandlers
    {
        static readonly Dictionary<Primitive, IPrimitiveHandler> ByKind = new IPrimitiveHandler[]
        {
            new DamageHandler(), new HealHandler(), new WardHandler(), new AfflictHandler(),
            new RelieveHandler(), new SwayHandler(), new ShiftHandler(), new ZoneHandler(),
            new IlluminateHandler(), new RevealHandler(), new DispelHandler(), new CounterHandler(),
            new SummonHandler(), new StabilizeHandler(), new StrikeHandler(), new DirectHandler(),
            new DisarmHandler(), new ConjureHandler(), new NarrateHandler(),
        }.ToDictionary(h => h.Kind);

        public static IEnumerable<IPrimitiveHandler> All => Primitives.All.Select(For);

        public static IPrimitiveHandler For(Primitive kind) =>
            ByKind.TryGetValue(kind, out IPrimitiveHandler handler)
                ? handler
                : throw new InvalidOperationException($"no handler for the {kind.Id()} primitive");
    }
}
