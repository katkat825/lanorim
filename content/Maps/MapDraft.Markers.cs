using System.Collections.Generic;
using System.Linq;
using Content.Schema;
using Core.Space;

namespace Content.Maps
{
    public sealed partial class MapDraft
    {
        // --- markers -----------------------------------------------------------------------------

        public bool PlaceStart(Cell cell)
        {
            if (!Contains(cell) || !At(cell).IsPassable() || IsBlocked(cell)) return false;

            Cell was = Start;

            Do(() => Start = cell, () => Start = was);

            return true;
        }

        public const int FirstSpawn = 1;
        public const int LastSpawn = 9;

        public bool PlaceSpawn(int slot, Cell cell)
        {
            if (slot < FirstSpawn || slot > LastSpawn) return false;

            if (!Contains(cell) || !At(cell).IsPassable() || IsBlocked(cell)) return false;

            // two monsters on one square would both be standing in the same place at round one
            if (_spawns.Any(s => s.Key != slot && s.Value == cell)) return false;

            bool had = _spawns.TryGetValue(slot, out Cell was);

            Do(() => _spawns[slot] = cell,
               () =>
               {
                   if (had) _spawns[slot] = was;
                   else _spawns.Remove(slot);
               });

            return true;
        }

        public bool ClearSpawn(int slot)
        {
            if (!_spawns.TryGetValue(slot, out Cell was)) return false;

            Do(() => _spawns.Remove(slot), () => _spawns[slot] = was);

            return true;
        }

        // --- props -------------------------------------------------------------------------------

        public bool PlaceProp(string id, Cell cell, int turn = 0, string actor = null)
        {
            if (!Json.IsId(id)) return false;

            var prop = new Prop(id, cell, turn, actor);

            if (!Fits(prop)) return false;

            Do(() => _props.Add(prop), () => _props.Remove(prop));

            return true;
        }

        // WHERE A PROP MAY STAND (cc_task_l Part 3.3): every square of its footprint on the map and standing ground; and
        // one that blocks may not cover the start, a spawn or another blocking prop. `moving` is the prop being picked
        // up and put down again, which can't be in its own way
        public bool Fits(Prop prop, Prop moving = null)
        {
            IReadOnlyList<Cell> covers = Covers(prop);

            if (covers.Any(c => !Contains(c) || !At(c).IsPassable())) return false;

            if (!Blocks(prop)) return true;

            // a crate on the hero's start or a monster's spawn would leave it standing inside the crate
            if (covers.Any(c => c == Start || _spawns.ContainsValue(c))) return false;

            return !_props.Any(other => !Equals(other, moving) && Blocks(other) && Covers(other).Intersect(covers).Any());
        }

        public int ClearProps(Cell cell)
        {
            List<Prop> here = _props.Where(p => p.Cell == cell).ToList();

            if (here.Count == 0) return 0;

            Do(() => _props.RemoveAll(p => p.Cell == cell),
               () => _props.AddRange(here));

            return here.Count;
        }

        // A PLACED PROP, PICKED UP (cc_task_l Part 4.4): the one standing over this square, the last put down first, so a
        // click on a building's far corner finds the building
        public Prop PropAt(Cell cell) => _props.LastOrDefault(p => Covers(p).Contains(cell));

        public bool RemoveProp(Prop prop)
        {
            int at = _props.IndexOf(prop);

            if (at < 0) return false;

            Do(() => _props.Remove(prop), () => _props.Insert(System.Math.Min(at, _props.Count), prop));

            return true;
        }

        // the prop put down again somewhere else, or turned where it stands: refused, and left as it was, when it
        // wouldn't fit there. the prop it becomes, or null
        public Prop MoveProp(Prop prop, Cell to, int turn)
        {
            int at = _props.IndexOf(prop);

            if (at < 0) return null;

            var moved = new Prop(prop.Id, to, turn, prop.Actor);

            if (!Fits(moved, prop)) return null;

            Do(() => _props[at] = moved, () => _props[at] = prop);

            return moved;
        }

        public Prop TurnProp(Prop prop, int quarters) => MoveProp(prop, prop?.Cell ?? default, (prop?.Turn ?? 0) + quarters);

        // WHO A FIGURE IS (cc_task_m3 Part 10): one of the campaign's characters by its actor id, or "" for someone. A
        // figure only; the prop it becomes, or null
        public Prop NameProp(Prop prop, string actor)
        {
            int at = _props.IndexOf(prop);

            if (at < 0 || !Palette.IsFigure(prop.Id) || actor != "" && !Json.IsId(actor)) return null;

            Prop named = prop.Named(actor);

            Do(() => _props[at] = named, () => _props[at] = prop);

            return named;
        }
    }
}
