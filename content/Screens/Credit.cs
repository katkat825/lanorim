using System.Collections.Generic;
using Core.Words;

namespace Content.Screens
{
    // what a line of the credits is (content/srd/credits/credits.json)
    public enum CreditKind
    {
        Ruleset,
        MadeBy,
        Asset,
        Font,
        Code,

        // a campaign's authors: none yet, and the campaign format can add them
        Contributor,
    }

    // what it was used for, said through the locale
    public enum CreditRole
    {
        Rules,
        Game,
        Models,
        Textures,
        Art,
        Ui,
        Sounds,
        Fonts,
        Code,
    }

    public enum CreditLicence
    {
        Cc0,
        [Word("cc_by_4")] CcBy4,
        [Word("cc_by_3")] CcBy3,

        // the Quaternius Asset License
        Qal,

        // the SIL Open Font License
        Ofl,
        Mit,
        Bsd,

        // its own terms (Admurin's)
        Custom,

        // free to use, its own short terms, credit optional
        Free,

        // all rights reserved (Kathleen's own)
        Proprietary,
    }

    // ONE LINE OF THE CREDITS (cc_task_f Part 3). Name, by and packs are names, never translated; role and licence are
    // words. Ships false: nothing of it is in game/ yet, so the screen leaves it out; Files is what of game/ it covers
    public sealed record Credit
    {
        public string Id { get; init; } = "";
        public CreditKind Kind { get; init; }
        public string Name { get; init; } = "";
        public string By { get; init; } = "";
        public CreditRole Role { get; init; }
        public CreditLicence? Licence { get; init; }
        public string Url { get; init; } = "";
        public bool Ships { get; init; } = true;
        public IReadOnlyList<string> Packs { get; init; } = System.Array.Empty<string>();
        public IReadOnlyList<string> Files { get; init; } = System.Array.Empty<string>();

        // the SRD's attribution, verbatim; nothing else has one
        public string Text { get; init; } = "";
    }
}
