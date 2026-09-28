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
            if (!Contains(cell) || !At(cell).IsPassable()) return false;

            Cell was = Start;

            Do(() => Start = cell, () => Start = was);

            return true;
        }

        public const int FirstSpawn = 1;
        public const int LastSpawn = 9;

        public bool PlaceSpawn(int slot, Cell cell)
        {
            if (slot < FirstSpawn || slot > LastSpawn) return false;

            if (!Contains(cell) || !At(cell).IsPassable()) return false;

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

        public bool PlaceProp(string id, Cell cell, int turn = 0)
        {
            if (!Json.IsId(id) || !Contains(cell) || !At(cell).IsPassable()) return false;

            var prop = new Prop(id, cell, turn);

            Do(() => _props.Add(prop), () => _props.Remove(prop));

            return true;
        }

        public int ClearProps(Cell cell)
        {
            List<Prop> here = _props.Where(p => p.Cell == cell).ToList();

            if (here.Count == 0) return 0;

            Do(() => _props.RemoveAll(p => p.Cell == cell),
               () => _props.AddRange(here));

            return here.Count;
        }
    }
}
