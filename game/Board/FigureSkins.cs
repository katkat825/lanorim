using Godot;

namespace Game.Board
{
    // THE MAP'S PEOPLE'S SKIN, AND THE HUMAN MINIS' (Kathleen, 2026-10-23: "a variety of natural-ish skin tone colors").
    // The Ultimate Animated Character Pack paints every figure's Skin surface near-black; each figure wears one of these
    // instead, picked by its id so a figure always looks the same (an NPC by its figure, a bandit in a fight by its
    // creature, MiniModels). Kathleen's to change (#5)
    public static class FigureSkins
    {
        // light to deep
        public static readonly Color[] Tones =
        {
            new("#E6BC98"), new("#D9A47F"), new("#C98D66"), new("#A9734E"), new("#87583A"), new("#5F3B27"),
        };

        // THE HERO'S HAIR (Kathleen's UI notes, 2026-10-27): black, browns, auburn, ginger, blondes, grey, white
        public static readonly Color[] Hair =
        {
            new("#1E1A18"), new("#3B2A20"), new("#6B4A2F"), new("#8C3B22"), new("#C2652A"), new("#D9B26A"), new("#EFD9A0"),
            new("#8E8A86"), new("#E8E4DC"),
        };

        // a list's colour by the hero's pick, or null for the figure's own (-1, or one past the list)
        public static Color? Pick(Color[] list, int at) => at >= 0 && at < list.Length ? list[at] : null;

        public static Color For(string figure)
        {
            int sum = 0;
            foreach (char c in figure ?? "") sum = sum * 31 + c;
            return Tones[(sum % Tones.Length + Tones.Length) % Tones.Length];
        }
    }
}
