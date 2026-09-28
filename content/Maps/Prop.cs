using Core.Space;

namespace Content.Maps
{
    // a prop on a square: a barrel, a brazier, a bookcase. the editor paints these; the board
    // layer turns each into a Quaternius model. purely decorative to the rules - anything that
    // blocks movement is a tile or a wall, not a prop, so the rules never have to ask a model.
    public sealed class Prop
    {
        public Prop(string id, Cell cell, int turn = 0)
        {
            Id = id;
            Cell = cell;
            Turn = ((turn % 4) + 4) % 4;
        }

        public string Id { get; }

        public Cell Cell { get; }

        // quarter turns, 0 to 3. the only transform the editor offers, because a free rotation is
        // a thing you can only judge by looking - and the person doing the looking is in Godot
        public int Turn { get; }

        public override string ToString() => $"{Id} at {Cell}" + (Turn > 0 ? $" turned {Turn}" : "");
    }
}
