using System.Collections.Generic;
using System.Linq;
using Core.Space;

namespace Content.Maps
{
    public sealed partial class MapDraft
    {
        // --- what it makes ------------------------------------------------------------------------

        // the rules' map: the squares, the lines (a fence's or a hedge's wall as bars, which stop a walker and not an
        // eye: WallStyles.Rule), the start and spawns, and every square a blocking prop covers
        public MapLayout Layout() => Layout(asDrawn: false);

        // asDrawn: a fence's wall left a wall, as the map text writes it (the "walls" layer says it's a fence)
        MapLayout Layout(bool asDrawn)
        {
            var vertical = new Edge[Extent.VerticalCount];
            var horizontal = new Edge[Extent.HorizontalCount];

            foreach (KeyValuePair<Border, Edge> edge in _edges)
            {
                Edge[] layer = edge.Key.Vertical ? vertical : horizontal;
                int i = Extent.Index(edge.Key);

                if (i >= 0 && i < layer.Length) layer[i] = asDrawn ? edge.Value : StyleAt(edge.Key).Rule(edge.Value);
            }

            return new MapLayout(Columns, Rows, (Tile[])_tiles.Clone(), Start,
                                 vertical, horizontal, _spawns,
                                 _props.Where(Blocks).SelectMany(Covers));
        }

        // how it looks, for the board: each square's ground and each line's style (cc_task_l Part 2)
        public MapLook Look() => new MapLook(Extent, _ground, _styles);

        // everything that would make the map unplayable: what the map builder shows, live, each with its square where it
        // has one (MapProblem, cc_task_f Part 2). checked on save, so an author finds out in the editor rather than the
        // player finding out mid-fight; a campaign won't load a map that has any
        public IReadOnlyList<MapProblem> Findings()
        {
            var problems = new List<MapProblem>();

            if (!At(Start).IsPassable())
                problems.Add(new MapProblem("start_on_rock", Start,
                    "the hero starts on solid rock - put the start somewhere you can stand"));
            else if (IsBlocked(Start))
                problems.Add(new MapProblem("start_in_prop", Start,
                    "the hero starts inside a prop that blocks - move the start or the prop"));

            MapLayout map = Layout();

            // a shut door is a way through: the hero opens it in play (Battlefield.OpenDoor). before cc_task_l a spawn in
            // the next room of a house was "unreachable"
            MapLayout opened = map;
            foreach (Border door in _edges.Where(e => e.Value == Edge.Door).Select(e => e.Key)) opened = opened.With(door, Edge.None);

            foreach (KeyValuePair<int, Cell> spawn in _spawns.OrderBy(s => s.Key))
            {
                if (!At(spawn.Value).IsPassable())
                {
                    problems.Add(new MapProblem("spawn_on_rock", spawn.Value, $"spawn {spawn.Key} is on solid rock", spawn.Key));
                    continue;
                }

                if (IsBlocked(spawn.Value))
                {
                    problems.Add(new MapProblem("spawn_in_prop", spawn.Value,
                        $"spawn {spawn.Key} is inside a prop that blocks", spawn.Key));
                    continue;
                }

                if (!Route.Exists(opened, Start, spawn.Value, _ => false))
                    problems.Add(new MapProblem("spawn_unreachable", spawn.Value,
                        $"spawn {spawn.Key} cannot be walked to from the start, even through the doors - " +
                        "a wall, a gap of rock or a prop that blocks is in the way", spawn.Key));
            }

            PropFindings(problems);

            int floor = map.Cells.Count(c => map.At(c).IsPassable());

            if (floor == 0) problems.Add(new MapProblem("nothing_painted", null, "nothing has been painted yet"));

            return problems;
        }

        // a prop off the map's edge, in a wall, or two that block on one square (cc_task_l Part 3.3). the builder won't
        // place one like that, but a map file can say so, or the palette change a prop's size under it
        void PropFindings(List<MapProblem> problems)
        {
            var taken = new Dictionary<Cell, Prop>();

            foreach (Prop prop in _props)
            {
                IReadOnlyList<Cell> covers = Covers(prop);
                string name = Palette.Find(prop.Id)?.NameKey ?? prop.Id;

                if (covers.Any(c => !Contains(c)))
                    problems.Add(new MapProblem("prop_off_map", prop.Cell, $"the {prop.Id} at {prop.Cell} runs off the map", name));
                else if (Palette.IsFigure(prop.Id) && !At(prop.Cell).IsPassable())
                    problems.Add(new MapProblem("npc_cant_stand", prop.Cell, $"the {prop.Id} at {prop.Cell} stands where nobody could", name)
                    {
                        Warning = true,
                    });
                else if (covers.Any(c => !At(c).IsPassable()))
                    problems.Add(new MapProblem("prop_in_wall", prop.Cell, $"the {prop.Id} at {prop.Cell} is inside a wall", name));

                if (!Blocks(prop)) continue;

                foreach (Cell cell in covers)
                {
                    if (taken.TryGetValue(cell, out Prop other))
                    {
                        problems.Add(new MapProblem("props_overlap", cell,
                            $"the {prop.Id} at {prop.Cell} stands on the {other.Id} at {other.Cell}", name));
                        break;
                    }

                    taken[cell] = prop;
                }
            }
        }

        // the same, in English, for the campaign loader's log
        public IReadOnlyList<string> Problems() => Findings().Where(p => !p.Warning).Select(p => p.Says).ToList();

        // a map plays with warnings, not with faults (MapProblem.Warning)
        public bool Sound => Findings().All(p => p.Warning);
    }
}
