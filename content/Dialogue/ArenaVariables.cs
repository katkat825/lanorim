using System;
using System.Collections.Generic;
using System.Linq;
using Content.Challengers;
using Yarn;
using Yarn.Compiler;

namespace Content.Dialogue
{
    // WHAT A STORY CAN READ ABOUT THE ARENA (cc_task_cg Parts 2 and 5), the way it reads the hero (HeroVariables):
    // declared to Yarn for every campaign, read-only, read live from the run's arena as the story asks. The arena
    // itself is saved with the run (SaveGame.Arena), so these are never stored as story variables
    public static class ArenaVariables
    {
        // the hero's reputation: 0 and up
        public const string Reputation = "$reputation";

        // who <<challengers>> picked: what <<fight {$challenger}>> fights. "" when the player went back to the story
        public const string Challenger = "$challenger";

        // the picked challenger's name, as a locale key: a line that says {$challenger_name} shows the name itself
        // (Conversation.Said resolves a substitution that is a key)
        public const string Name = "$challenger_name";

        // the picked challenger is a hand-written special fight
        public const string Special = "$challenger_special";

        public static readonly IReadOnlyList<string> Names = new[] { Reputation, Challenger, Name, Special };

        public static IConvertible Read(Arena arena, string variable) => arena == null ? null : variable switch
        {
            Reputation => (float)arena.Reputation,
            Challenger => arena.Picked?.Id ?? "",
            Name => arena.Picked?.NameKey ?? "",
            Special => arena.Picked?.Special ?? false,
            _ => null,
        };

        public static readonly IReadOnlyList<Declaration> Declarations = new[]
        {
            Declare(Reputation, Types.Number, 0f),
            Declare(Challenger, Types.String, ""),
            Declare(Name, Types.String, ""),
            Declare(Special, Types.Boolean, false),
        };

        static Declaration Declare(string name, IType type, IConvertible value) =>
            new DeclarationBuilder().WithName(name).WithType(type).WithDefaultValue(value)
                                    .WithDescription("the arena, read-only (cc_task_cg)")
                                    .Declaration;
    }
}
