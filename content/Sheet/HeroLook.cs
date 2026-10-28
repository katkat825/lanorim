namespace Content.Sheet
{
    // HOW THE HERO'S MINI LOOKS, PAST ITS FIGURE (Kathleen's UI notes, 2026-10-27): the last page of creation offers the
    // class's figure (the suggestion) or another class's body, and a skin tone and a hair colour from the game's lists.
    // A look, not a rule: nothing in play reads it. Body "" is the hero's own class; Skin and Hair are indexes into the
    // game's lists (Game.Board.FigureSkins), -1 the figure's own. A species with a mini of its own (the Beastfolk) keeps
    // its kind's, whatever this says
    public sealed record HeroLook(string Body = "", int Skin = -1, int Hair = -1)
    {
        public static readonly HeroLook Own = new();

        public bool IsOwn => this == Own;
    }
}
