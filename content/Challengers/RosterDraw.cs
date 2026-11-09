using System;
using System.Collections.Generic;
using System.Linq;
using Content.Dialogue;
using Content.Monsters;
using Core.Dice;
using Core.Tables;

namespace Content.Challengers
{
    // DRAWING THE ROSTER (cc_task_cg 3.1-3.5), from the run's own dice so a seeded run draws the same one twice.
    //
    // As many as reputation allows (ArenaRules.RosterSize). One the hero lost to or yielded to is kept for a rematch;
    // the rest is drawn fresh. The special fights come first: each one the hero's reputation reaches and hasn't beaten
    // rolls its trigger. Then the generated ones: a type that fits the hero's level (ChallengerFit), or the closest
    // ones when none does; its next name and description from a ShuffleBag each, so neither comes round again until its
    // list runs out; a team drawn to a budget there and then
    public sealed class RosterDraw
    {
        readonly Arena _arena;
        readonly IReadOnlyList<ChallengerType> _types;
        readonly Func<string, Monster> _find;
        readonly IRng _rng;

        public RosterDraw(Arena arena, IReadOnlyList<ChallengerType> types, Func<string, Monster> find, IRng rng)
        {
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            _types = types ?? Array.Empty<ChallengerType>();
            _find = find ?? (_ => null);
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        // keep: the one the hero lost to, kept for a rematch. beaten: the one they just beat, which this draw doesn't
        // deal again even when its name and description have come round
        public void Draw(int level, ArenaRules rules, IEnumerable<EncounterEntry> specials, Challenger keep = null,
                         Challenger beaten = null)
        {
            int size = (rules ?? ArenaRules.Default).RosterSize(_arena.Reputation);

            var roster = new List<Challenger>();

            if (keep != null) roster.Add(keep);

            foreach (EncounterEntry special in Eligible(specials))
            {
                if (roster.Count >= size) break;

                if (roster.Any(c => c.Id == special.Id)) continue;

                Trigger trigger = special.Challenger.Trigger;

                if (trigger.Fires(trigger.IsAlways ? 0 : trigger.Dice.Roll(_rng))) roster.Add(Special(special, level));
            }

            var offered = Offered(level).ToList();

            for (int tries = 0; roster.Count < size && offered.Count > 0 && tries < size * 20; tries++)
            {
                var (type, choices) = offered[_rng.Roll(offered.Count) - 1];

                IReadOnlyList<string> names = type.NameKeys(_types);

                // NO NAME TWICE ON ONE ROSTER: a type whose every name is already up offers no more this time, and the
                // roster is shorter rather than showing two "Grik the Knife"s
                if (roster.Count(c => c.Type == type.Id) >= names.Count)
                {
                    offered.RemoveAll(o => o.Type.Id == type.Id);
                    continue;
                }

                // a single that is one of several ("a single thug or scout"): one of those that fit, this time
                IReadOnlyList<Mustered> group = choices?[_rng.Roll(choices.Count) - 1];

                Challenger drawn = Generated(type, group, names, level, roster);

                if (drawn != null && drawn.Id != beaten?.Id) roster.Add(drawn);
            }

            _arena.Roster.Clear();
            _arena.Roster.AddRange(roster);
            _arena.Drawn = true;
        }

        IEnumerable<EncounterEntry> Eligible(IEnumerable<EncounterEntry> specials) =>
            (specials ?? Enumerable.Empty<EncounterEntry>())
                .Where(e => e.Challenger != null && e.Kind == EntryKind.Fight &&
                            _arena.Reputation >= e.Challenger.Reputation && !_arena.Beaten.Contains(e.Id));

        // THE TYPES THAT FIT THE HERO'S LEVEL, each with the groups it can field: a team none (it is drawn to fit there
        // and then), a group its one, a single with alternatives each one that fits. none fitting, the closest
        IReadOnlyList<(ChallengerType Type, IReadOnlyList<IReadOnlyList<Mustered>> Choices)> Offered(int level)
        {
            var priced = new List<(ChallengerType Type, IReadOnlyList<Mustered> Group, double Off)>();

            foreach (ChallengerType t in _types.Where(t => !t.Shared && t.NameKeys(_types).Count > 0 &&
                                                           t.DescriptionKeys(_types).Count > 0))
            {
                if (t.Shape == ChallengerShape.Team)
                {
                    priced.Add((t, null, 0));
                    continue;
                }

                IEnumerable<IReadOnlyList<Mustered>> groups = t.Shape == ChallengerShape.Single
                    ? t.Monsters.Select(b => (IReadOnlyList<Mustered>)new[] { new Mustered(b.Monster, 1) })
                    : new[] { ChallengerFit.Fixed(t.Monsters) };

                foreach (IReadOnlyList<Mustered> group in groups)
                    priced.Add((t, group, ChallengerFit.Distance(ChallengerFit.XpOf(group, _find), level)));
            }

            if (priced.Count == 0) return Array.Empty<(ChallengerType, IReadOnlyList<IReadOnlyList<Mustered>>)>();

            double closest = priced.Min(p => p.Off);

            return priced.Where(p => p.Off == closest)
                         .GroupBy(p => p.Type)
                         .Select(g => (g.Key, g.Key.Shape == ChallengerShape.Team
                                                  ? null
                                                  : (IReadOnlyList<IReadOnlyList<Mustered>>)g.Select(p => p.Group).ToList()))
                         .ToList();
        }

        Challenger Special(EncounterEntry entry, int level)
        {
            IReadOnlyList<Mustered> group = ChallengerFit.Fixed(entry.Monsters);
            int xp = ChallengerFit.XpOf(group, _find);

            return new Challenger
            {
                Id = entry.Id,
                Entry = entry.Id,
                Shape = group.Sum(m => m.Count) == 1 ? ChallengerShape.Single : ChallengerShape.Group,
                Group = group,
                Xp = xp,
                Difficulty = ChallengerFit.DifficultyOf(xp, level),
            };
        }

        // a type's next name and description; a name already on the roster is passed over, and never put up twice
        // (its names are its own and the shared lists', NameKeys)
        Challenger Generated(ChallengerType type, IReadOnlyList<Mustered> group, IReadOnlyList<string> names, int level,
                             List<Challenger> roster)
        {
            IReadOnlyList<string> descriptions = type.DescriptionKeys(_types);

            int name = Next(type.Id + ".names", names.Count,
                            n => roster.Any(c => c.Type == type.Id && c.Name == n));

            if (roster.Any(c => c.Type == type.Id && c.Name == name)) return null;

            // a description may come up beside another name: any name goes with any description
            int description = Next(type.Id + ".descriptions", descriptions.Count, _ => false);

            string id = Challenger.IdOf(type.Id, name, description);

            if (roster.Any(c => c.Id == id)) return null;

            group ??= ChallengerFit.Team(type.Monsters, _find, level, _rng, out _);

            int xp = ChallengerFit.XpOf(group, _find);

            return new Challenger
            {
                Id = id,
                Type = type.Id,
                Shape = type.Shape,
                Name = name,
                Description = description,
                NameKey = names[name - 1],
                DescriptionKey = descriptions[description - 1],
                Group = group,
                Xp = xp,
                Difficulty = ChallengerFit.DifficultyOf(xp, level),
            };
        }

        // the next from a list's shuffle, the bag picked up where the arena left it and put back after
        int Next(string list, int count, Func<int, bool> taken)
        {
            _arena.Bags.TryGetValue(list, out Arena.Bag kept);

            var bag = kept == null ? new ShuffleBag(count, _rng) : new ShuffleBag(count, _rng, kept.Left, kept.Last);

            int drawn = bag.Next();

            for (int i = 1; i < count && taken(drawn); i++) drawn = bag.Next();

            var left = new Arena.Bag { Last = bag.Last };
            left.Left.AddRange(bag.Unheard);
            _arena.Bags[list] = left;

            return drawn;
        }
    }
}
