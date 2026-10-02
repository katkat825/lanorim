using System;
using System.Collections.Generic;
using Core.Space;

namespace Content.Maps
{
    // ONE THING THAT WOULD MAKE A MAP UNPLAYABLE (MapDraft.Findings): what the map builder says, as a keyed sentence
    // (Said: an argument that is itself a key, a prop's name, is the game's to say), and the square it is about when
    // it has one, so a click on it can show where (cc_task_f, Part 2).
    // `Says` is the same in English for the campaign loader, whose problems are an author's log like every other
    // ContentProblem; the builder never shows it
    public sealed class MapProblem
    {
        public MapProblem(string name, Cell? at, string says, params object[] args)
        {
            Said = new Screens.Said(KeyFor(name), args ?? Array.Empty<object>());
            At = at;
            Says = says ?? "";
        }

        public Screens.Said Said { get; }

        // the square, or null for the map as a whole
        public Cell? At { get; }

        public string Says { get; }

        public static string KeyFor(string name) => Screens.ScreenKeys.Key("map", "problem_" + name);

        // every problem the draft can find, by name: the keys the locale must have
        public static readonly IReadOnlyList<string> Names = new[]
        {
            "start_on_rock", "start_in_prop", "spawn_on_rock", "spawn_in_prop", "spawn_unreachable", "prop_in_wall",
            "nothing_painted",
        };

        public override string ToString() => Says;
    }
}
