using System;
using Core.Space;

namespace Content.Maps
{
    // a prop on the map: a barrel, a brazier, a bookcase, a house. the editor places these; the board
    // layer turns each into a Quaternius model. one that blocks (the palette says) is walked round, over
    // every square of its footprint (MapDraft.Covers); the rest are scenery.
    //
    // A VALUE: two props are the same prop when they are the same thing on the same square at the same
    // turn, so one picked up in the builder is still found after an undo rebuilt the list (cc_task_l)
    public sealed class Prop : IEquatable<Prop>
    {
        public Prop(string id, Cell cell, int turn = 0)
        {
            Id = id;
            Cell = cell;
            Turn = ((turn % 4) + 4) % 4;
        }

        public string Id { get; }

        // where it stands: the top-left square of its footprint, as turned
        public Cell Cell { get; }

        // quarter turns, 0 to 3. the only transform the editor offers, because a free rotation is
        // a thing you can only judge by looking - and the person doing the looking is in Godot
        public int Turn { get; }

        public bool Equals(Prop other) => other is not null && Id == other.Id && Cell == other.Cell && Turn == other.Turn;

        public override bool Equals(object obj) => obj is Prop other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Id, Cell, Turn);

        public override string ToString() => $"{Id} at {Cell}" + (Turn > 0 ? $" turned {Turn}" : "");
    }
}
