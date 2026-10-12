using System;
using System.Collections.Generic;
using System.Linq;
using Core.Space;

namespace Content.Maps
{
    public sealed partial class MapDraft
    {
        // --- undo ---------------------------------------------------------------------------------

        // how deep a Change is: the edits inside one are a single undo step, kept whole (Change)
        int _changing;

        void Do(Action forward, Action back)
        {
            forward();

            if (_changing == 0) Remember(back);
        }

        void Remember(Action back)
        {
            _undo.Push(back);
            _redo.Clear();

            // an editor that remembers forever is an editor that eats a map's worth of memory per
            // drag; a hundred steps is more than anybody reaches for
            if (_undo.Count > UndoDepth)
            {
                var kept = _undo.ToArray().Take(UndoDepth).Reverse().ToArray();

                _undo.Clear();

                foreach (Action step in kept) _undo.Push(step);
            }
        }

        // MANY EDITS AS ONE UNDO STEP (cc_task_l Part 4: a paste, a move, a fill, a scatter of trees): the map as it was
        // is kept whole and put back whole, so the edits inside need no undo of their own. true when anything changed
        public bool Change(Action edits)
        {
            string before = Save();

            _changing++;

            try
            {
                edits();
            }
            finally
            {
                _changing--;
            }

            if (Save() == before) return false;

            if (_changing == 0) Remember(() => Restore(before));

            return true;
        }

        public const int UndoDepth = 100;

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        public bool Undo()
        {
            if (!CanUndo) return false;

            // the redo is built by replaying: capturing both directions per step would double
            // every closure above, and a map is cheap to snapshot
            string before = Save();

            _undo.Pop()();

            string after = Save();

            _redo.Push(() => Restore(before));

            return before != after;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;

            string before = Save();

            _redo.Pop()();

            _undo.Push(() => Restore(before));

            return true;
        }

        void Restore(string saved)
        {
            if (!TryRead(saved, out MapDraft draft, out _)) return;

            Array.Copy(draft._tiles, _tiles, Math.Min(draft._tiles.Length, _tiles.Length));
            Array.Copy(draft._ground, _ground, Math.Min(draft._ground.Length, _ground.Length));

            _edges.Clear();
            foreach (KeyValuePair<Border, Edge> edge in draft._edges) _edges[edge.Key] = edge.Value;

            _styles.Clear();
            foreach (KeyValuePair<Border, WallStyle> style in draft._styles) _styles[style.Key] = style.Value;

            _spawns.Clear();
            foreach (KeyValuePair<int, Cell> spawn in draft._spawns) _spawns[spawn.Key] = spawn.Value;

            _props.Clear();
            _props.AddRange(draft._props);

            Start = draft.Start;
        }
    }
}
