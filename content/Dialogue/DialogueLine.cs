using System;
using Content.Campaigns;

namespace Content.Dialogue
{
    // one line of a conversation, as the compiler found it and as the locale will be asked for it.
    // The words are NOT here: the .yarn source holds the author's working text and the campaign's
    // locale/ holds what a player reads, in every language including English (CONVENTIONS section 7).
    public sealed class DialogueLine
    {
        public DialogueLine(string yarnId, string key, string speaker, string node, string file,
                            int line, string source, string campaign = null)
        {
            YarnId = yarnId ?? "";
            Key = key ?? "";
            Speaker = speaker ?? "";
            Node = node ?? "";
            File = file ?? "";
            Line = line;
            Source = source ?? "";
            Campaign = campaign ?? "";
        }

        // what the runtime calls it: "line:the_hinges_are_new"
        public string YarnId { get; }

        // what the localizer calls it: "dialogue.wolf.line.greyhollow.the_hinges_are_new"
        public string Key { get; }

        // the creature saying it - the node's speaker, or this line's own #speaker: override
        public string Speaker { get; }

        public string Node { get; }

        // for a problem to point at, campaign-relative
        public string File { get; }

        public int Line { get; }

        // the author's working text. Never shown: a line that reached the screen from here would be
        // English hardcoded in a data file, which is the one thing the pseudolocale cannot find.
        public string Source { get; }

        // the campaign it was compiled for, which is a segment of every key it can be asked under
        public string Campaign { get; }


        // THE GENERIC COMPANION, AND THE SAME FLOOR THE DM IS FOR A BEAT (Beat.TheDm).
        //
        // A base-game campaign does not know which class the player picked, and five companions
        // is five copies of a line that only needs saying once - "somebody's been through here".
        // So a line may be spoken by 'companion', and the presentation hands it to whichever
        // companion is actually at the table.
        //
        // The override is a LOCALE ROW, not a second yarn line. Yarn wants every #line: tag unique
        // across the program, so the wolf's own version cannot be written beside the generic one
        // in the .yarn; it is written beside it in the CSV instead:
        //
        //   speaker: companion
        //   Somebody's been through here. #line:somebody_passed
        //
        //   dialogue.companion.line.greyhollow.somebody_passed    -> said by any companion
        //   dialogue.bonded_wolf.line.greyhollow.somebody_passed  -> the wolf's own, when it is the wolf
        //
        // The specific row is looked up first and the generic one is the fallback, which is the
        // beat spine's order (the voice you brought, then the DM) and for the same reason: the
        // information never depends on which class you picked; only the flavour does.
        //
        // Nothing here asks what kind of pack the line came from. The alias is for the base game
        // and Workshop was only ruled as not NEEDING it, so a Workshop campaign that writes it is
        // neither refused nor promised anything - the book cannot see the pack kind anyway.
        public const string AnyCompanion = "companion";

        public bool ForAnyCompanion => string.Equals(Speaker, AnyCompanion, StringComparison.Ordinal);

        // the local id the author tagged, which every per-companion key is built from
        public string Local => DialogueKeys.LocalOf(YarnId) ?? "";

        // the key this line would carry in one particular companion's mouth; null when it is not
        // a generic line, or when the id could not be a speaker at all
        public string KeyAs(string companion) =>
            ForAnyCompanion && ContentId.IsLocal(companion) && companion != AnyCompanion
                ? DialogueKeys.Line(companion, Campaign, Local)
                : null;

        // who says it at this table: the companion you brought, where the line was left to any.
        // An empty or unreadable companion leaves it 'companion', for the presentation to decide.
        public string SpeakerFor(string companion) =>
            KeyAs(companion) != null ? companion : Speaker;

        // the key to ask the locale for: this companion's own row if the campaign wrote one, the
        // generic row otherwise. A companion no campaign has heard of - a class pack published
        // after it - simply has no row, and gets the generic line in its own voice.
        public string KeyFor(string companion, Func<string, bool> written)
        {
            string own = KeyAs(companion);

            return own != null && written != null && written(own) ? own : Key;
        }

        public override string ToString() => $"{Key} ({File}:{Line})";
    }
}
