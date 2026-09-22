using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Game.Access
{
    // WHAT A SCREEN READER IS TOLD (AX2).
    //
    // This is the differentiator and it is nearly free, because the storytelling is already text: a
    // game whose DM narrates in writing has the words for a blind player before anybody asks for
    // them. What it does not have without this is the rest of the table - which object your hand is
    // on, what state it is in, that a die came up short.
    //
    // A LIST OF WHOLE SENTENCES, NEVER A SENTENCE ASSEMBLED FROM PARTS. That is the one rule here
    // and it is the localization rule arriving somewhere new: "{0} is
    // Winded" is one key, and reading "Winded" then "on your mini" aloud would be exactly the
    // fragment-joining the key grammar exists to forbid. So a Spoken holds finished sentences that
    // each came from their own key, in the order they should be heard, and the only thing it does to
    // them is put them one after another.
    //
    // Pure. Whether anything is listening, and how, is the narrator's business.
    public sealed class Spoken
    {
        Spoken(IReadOnlyList<string> lines, bool interrupts)
        {
            Lines = lines ?? Array.Empty<string>();
            Interrupts = interrupts;
        }

        public static readonly Spoken Nothing = new Spoken(null, false);

        public IReadOnlyList<string> Lines { get; }

        // said over whatever is still being said. A thing you just did outranks a thing you were
        // being told about, or the reader is always a sentence behind your hands
        public bool Interrupts { get; }

        public bool Any => Lines.Count > 0;

        // one after another, with the pause a full stop already implies. Nothing is glued together
        public string Aloud => string.Join(" ", Lines);

        public static Spoken Of(params string[] lines) => Made(lines, false);

        public static Spoken Urgently(params string[] lines) => Made(lines, true);

        // an empty or whitespace line is dropped rather than read as a pause: a blank sentence in a
        // reader is a silence the listener has to decide the meaning of
        static Spoken Made(IEnumerable<string> lines, bool interrupts)
        {
            string[] said = (lines ?? Array.Empty<string>())
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .Select(l => l.Trim())
                            .ToArray();

            return said.Length == 0 ? Nothing : new Spoken(said, interrupts);
        }

        public Spoken And(Spoken more) =>
            more == null || !more.Any
                ? this
                : Made(Lines.Concat(more.Lines), Interrupts || more.Interrupts);

        // WHAT THE THING UNDER THE CURSOR IS. Its name, then whatever else is true of it - the
        // same two things a sighted player gets from the thing lighting up and from the words on
        // it. The old build took a Reachable, which was an object in a room you could put a hand
        // on; the table has no such thing, so it takes the words directly and whoever knows what
        // is under the cursor supplies them.
        public static Spoken Reading(string called, params string[] also) =>
            string.IsNullOrEmpty(called)
                ? Nothing
                : Of(new[] { called }.Concat(also ?? Array.Empty<string>()).ToArray());

        // A LINE THAT IS STILL A KEY. The default localizer hands back the key itself so a missing
        // string is impossible to miss on screen - and impossible to notice in a reader, where
        // "ui.door.name" is just a noise. Cheap to detect, because a sentence in any language is
        // not a well-formed key: it has spaces, or capitals, or no dots.
        public static bool IsAKey(string line) =>
            !string.IsNullOrWhiteSpace(line) && KeyConventions.IsWellFormed(line.Trim());

        public IEnumerable<string> Keys() => Lines.Where(IsAKey);

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            !Any ? "nothing to say" : (Interrupts ? "! " : "") + Aloud;
    }
}
