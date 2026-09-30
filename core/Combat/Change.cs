using System.Collections.Generic;
using System.Linq;
using Core.Characters;

namespace Core.Combat
{
    // ONE NUMBER ON ONE CREATURE, BEFORE AND AFTER a spell: what the log and the tray's caption say
    // after a cast, so a Mage Armor that changes nothing on the board still says what it did
    // (cc_task_ui-issues-9-30.md 1.2). Taken by comparing a snapshot, so no primitive has to report it
    public sealed class Change
    {
        public Change(Actor who, Stat what, int from, int to)
        {
            Who = who;
            What = what;
            From = from;
            To = to;
        }

        public Actor Who { get; }

        public Stat What { get; }

        public int From { get; }

        public int To { get; }

        static int Read(Actor actor, Stat stat) => stat switch
        {
            Stat.ArmorClass => actor.ArmorClass,
            Stat.HitPoints => actor.Health.Current,
            Stat.TemporaryHitPoints => actor.Health.Temporary,
            Stat.MaxHitPoints => actor.Health.Maximum,
            Stat.Speed => actor.Speed,
            _ => 0,
        };

        static readonly Stat[] Watched = System.Enum.GetValues<Stat>();

        public static Dictionary<(Actor, Stat), int> Snapshot(IEnumerable<Actor> actors) =>
            actors.Distinct().SelectMany(a => Watched.Select(s => (a, s)))
                  .ToDictionary(k => k, k => Read(k.a, k.s));

        // what moved since the snapshot. hit points going down aren't here: the damage line says those
        public static IReadOnlyList<Change> Since(Dictionary<(Actor, Stat), int> before) =>
            before.Select(b => new Change(b.Key.Item1, b.Key.Item2, b.Value, Read(b.Key.Item1, b.Key.Item2)))
                  .Where(c => c.To != c.From && !(c.What == Stat.HitPoints && c.To < c.From))
                  .ToList();

        public override string ToString() => $"{Who?.Id} {What} {From} -> {To}";
    }
}
