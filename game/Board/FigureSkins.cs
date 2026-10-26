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

        public static Color For(string figure)
        {
            int sum = 0;
            foreach (char c in figure ?? "") sum = sum * 31 + c;
            return Tones[(sum % Tones.Length + Tones.Length) % Tones.Length];
        }
    }
}
