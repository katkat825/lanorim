using System;
using System.Collections.Generic;

namespace Content.Workshop
{
    // ONE UNDO AND REDO FOR A WHOLE CAMPAIGN PAGE (cc_task_m Part 6): Ctrl+Z and Ctrl+Y across its tabs, one stack per
    // campaign. A step is what to do to go back and what to do to go forward again; the campaign page's forms push
    // snapshots of the draft (CampaignEditor.Edit), and the table editors and the story editor (N, O, P) push their own
    // steps here too, so one Ctrl+Z walks back through all of it in the order it was done.
    //
    // The map builder keeps its own (MapDraft.Undo): a map is opened at the table, away from the campaign page, and its
    // steps are a map's, not the campaign's
    public sealed class UndoStack
    {
        public const int Depth = 100;

        sealed record Step(string What, Action Back, Action Forward);

        readonly LinkedList<Step> _undo = new();
        readonly Stack<Step> _redo = new();

        // after every push, undo or redo, so a page can redraw and an autosave can start counting
        public event Action Changed;

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        // what Ctrl+Z would take back, for a hint or a test: the step's own name ("chapter", "about")
        public string Next => _undo.Last?.Value.What;

        // a step already done: Back undoes it, Forward does it again. `merge`: typing into one field is one step, not
        // one a letter - a step with the same name as the last one done (and nothing undone since) folds into it,
        // keeping the last one's Back and taking this one's Forward
        public void Push(string what, Action back, Action forward, bool merge = false)
        {
            what ??= "";

            if (merge && _redo.Count == 0 && _undo.Last is { } last && last.Value.What == what)
            {
                _undo.RemoveLast();
                back = last.Value.Back;
            }

            _undo.AddLast(new Step(what, back, forward));
            _redo.Clear();

            // a hundred steps is more than anybody reaches for, and a page's worth of snapshots each
            while (_undo.Count > Depth) _undo.RemoveFirst();

            Changed?.Invoke();
        }

        public bool Undo()
        {
            if (!CanUndo) return false;

            Step step = _undo.Last.Value;
            _undo.RemoveLast();

            step.Back();
            _redo.Push(step);

            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo) return false;

            Step step = _redo.Pop();

            step.Forward();
            _undo.AddLast(step);

            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            Changed?.Invoke();
        }
    }
}
