using System;
using System.Collections.Generic;
using System.Linq;
using Content.Sheet;
using Yarn;
using Yarn.Compiler;

namespace Content.Dialogue
{
    // WHAT A STORY CAN READ ABOUT THE HERO (cc_task_i 4.1; the beginner tutorial's asks A2, A4, A6): declared to Yarn
    // for every campaign, like RequestVariables, and read-only - the hero's own, read live as the story asks, never
    // stored, never saved. And one function, has_item("rope"), for what's in the pack or worn
    public static class HeroVariables
    {
        // the class id: "fighter"
        public const string Class = "$hero_class";

        public const string Level = "$hero_level";

        // true when the hero has any spells
        public const string Casts = "$hero_casts";

        // which of the class's two minis: 1 or 2
        public const string Figure = "$hero_figure";

        // the name the player gave
        public const string Name = "$hero_name";

        // has_item("rope"): in the pack, or worn
        public const string HasItem = "has_item";

        public static readonly IReadOnlyList<string> Names = new[] { Class, Level, Casts, Figure, Name };

        public static bool Is(string variable) => Names.Contains(variable, StringComparer.Ordinal);

        public static IConvertible Read(Hero hero, string variable) => hero == null ? null : variable switch
        {
            Class => hero.Class.Id,
            Level => (float)hero.Level,
            Casts => hero.Spells.Any(),
            Figure => (float)(hero.Figure + 1),
            Name => hero.Name ?? "",
            _ => null,
        };

        public static bool Has(Hero hero, string itemId) =>
            hero != null && itemId != null && (hero.Pack.CountOf(itemId) > 0 || hero.Equipment.IsEquipped(itemId));

        public static readonly IReadOnlyList<Declaration> Declarations = new[]
        {
            Declare(Class, Types.String, ""),
            Declare(Level, Types.Number, 1f),
            Declare(Casts, Types.Boolean, false),
            Declare(Figure, Types.Number, 1f),
            Declare(Name, Types.String, ""),
        };

        static Declaration Declare(string name, IType type, IConvertible value) =>
            new DeclarationBuilder().WithName(name).WithType(type).WithDefaultValue(value)
                                    .WithDescription("the hero, read-only (cc_task_i 4.1)")
                                    .Declaration;

        // the function as the compiler sees it; a running conversation is given the hero's (Conversation.Lets)
        public static void Declare(Library library) => library.RegisterFunction<string, bool>(HasItem, _ => false);
    }
}
