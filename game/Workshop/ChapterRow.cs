using System;
using Godot;

namespace Game.Workshop
{
    // A CHAPTER ON THE CHAPTERS TAB THAT CAN BE DRAGGED (cc_task_m 4.2: "reorder by drag"): picked up by its panel or its
    // words (its buttons and boxes keep their own clicks) and dropped on another chapter to take its place. Up and Down
    // do the same from the keyboard
    public partial class ChapterRow : PanelContainer
    {
        const string Dragging = "workshop_chapter";

        public int Index { get; init; }

        public string Title { get; init; } = "";

        // from, to
        public event Action<int, int> Moved;

        public override Variant _GetDragData(Vector2 atPosition)
        {
            SetDragPreview(new Label { Text = Title });

            return new Godot.Collections.Dictionary { [Dragging] = Index };
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data) =>
            data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().ContainsKey(Dragging);

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            int from = data.AsGodotDictionary()[Dragging].AsInt32();

            if (from != Index) Moved?.Invoke(from, Index);
        }
    }
}
