using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Content.Campaigns;
using Core.Combat;

namespace Content.Workshop
{
    // A CAMPAIGN AS THE WORKSHOP EDITS IT (cc_task_m Parts 3 and 4): what its pack.json says (CampaignRules, the GM
    // screen, the chapters and where it starts), the words its locale holds for it (its name, its description, each
    // chapter's title, each character's name) and the Workshop's own notes (workshop.json). Nothing here is a second
    // format: CampaignFiles reads it out of a campaign folder and writes it back into the same files a hand-made
    // campaign has (WORKSHOP_DESIGN.md principle 1).
    //
    // A plain bag of what the forms change, so a whole draft can be kept as a snapshot - the undo stack's step, and the
    // autosave in .draft/ - and put back whole (Snapshot, FromSnapshot)
    public sealed class CampaignDraft
    {
        // the id is the folder's and every key's; it changes only by a rename (CampaignRename), never in a form
        public string Id { get; set; } = "";

        // --- about (the wizard's first page) ------------------------------------------------------------------------

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public string Author { get; set; } = "";

        public List<string> Tags { get; set; } = new();

        // MATURE CONTENT is a tag, not a new key (#9): pack.json's tags are already what a storefront filters on, and
        // Steam's mature flag (task R) reads it from there
        public const string MatureTag = "mature";

        [JsonIgnore]
        public bool Mature
        {
            get => Tags.Contains(MatureTag);
            set
            {
                Tags.Remove(MatureTag);
                if (value) Tags.Add(MatureTag);
            }
        }

        // the cover: a file in the campaign folder (preview.png), or "" for the GM screen's picture
        public string Preview { get; set; } = "";

        // --- how it plays (the wizard's second page): CampaignRules and the GM screen ---------------------------------

        public Defeat Defeat { get; set; } = CampaignRules.Default.Defeat;

        public Leveling Leveling { get; set; } = CampaignRules.Default.Leveling;

        public int Characters { get; set; } = CampaignRules.Default.Characters;

        public bool StartingShop { get; set; } = CampaignRules.Default.StartingShop;

        public Layering Consequences { get; set; } = CampaignRules.Default.Consequences;

        public string GmScreen { get; set; } = Manifest.GmScreens[0];

        // --- chapters and maps --------------------------------------------------------------------------------------

        public List<ChapterDraft> Chapters { get; set; } = new();

        // the chapter the campaign starts at; "" is the first
        public string Start { get; set; } = "";

        // --- characters: the named people who speak or appear -------------------------------------------------------

        public List<Person> People { get; set; } = new();

        // --- what the Workshop keeps as it found it -----------------------------------------------------------------

        public PackKind Kind { get; set; } = PackKind.Campaign;

        public int Format { get; set; } = Schema.ContentFormat.Current;

        public string Engine { get; set; } = Core.EngineVersion.Current.ToString();

        public List<string> Dependencies { get; set; } = new();

        // false when pack.json couldn't be read: the form shows what it could, and Save leaves pack.json alone rather
        // than write over the author's file with defaults (principle 4, nothing is lost)
        public bool PackRead { get; set; } = true;

        // --- reading it -------------------------------------------------------------------------------------------

        public CampaignRules Rules => new()
        {
            Defeat = Defeat,
            Leveling = Leveling,
            Characters = Characters,
            StartingShop = StartingShop,
            Consequences = Consequences,
        };

        public string StartsAt => Chapters.Any(c => c.Id == Start) ? Start : Chapters.FirstOrDefault()?.Id ?? "";

        public ChapterDraft Chapter(string id) => Chapters.FirstOrDefault(c => c.Id == id);

        public Person Character(string id) => People.FirstOrDefault(p => p.Id == id);

        // the manifest these say, for ManifestWriter (and for an Advanced look at the JSON a form writes)
        public Manifest ToManifest() =>
            new(Id, Kind, Format, Version.TryParse(Engine, out Version engine) ? engine : Core.EngineVersion.Current, Author,
                Tags, Preview, Dependencies,
                Chapters.Select(c => new Campaigns.Chapter(c.Id, c.Maps.ToList())).ToList(), StartsAt)
            {
                GmScreen = Manifest.GmScreens.Contains(GmScreen) ? GmScreen : Manifest.GmScreens[0],
                Rules = Rules,
            };

        // the draft as it was read from a manifest: everything pack.json says, the words left for the locale to fill
        public static CampaignDraft Of(Manifest manifest) => new()
        {
            Id = manifest.Id,
            Author = manifest.Author,
            Tags = manifest.Tags.ToList(),
            Preview = manifest.Preview,
            Defeat = manifest.Rules.Defeat,
            Leveling = manifest.Rules.Leveling,
            Characters = manifest.Rules.Characters,
            StartingShop = manifest.Rules.StartingShop,
            Consequences = manifest.Rules.Consequences,
            GmScreen = manifest.GmScreen,
            Chapters = manifest.Chapters.Select(c => new ChapterDraft { Id = c.Id, Maps = c.Maps.ToList() }).ToList(),
            Start = manifest.Start,
            Kind = manifest.Kind,
            Format = manifest.Format,
            Engine = manifest.Engine?.ToString() ?? Core.EngineVersion.Current.ToString(),
            Dependencies = manifest.Dependencies.ToList(),
        };

        // --- a snapshot: the undo stack's step and the autosave ---------------------------------------------------------

        static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        public string Snapshot() => JsonSerializer.Serialize(this, Options);

        // null when the text isn't a draft (an autosave from a build that wrote something else)
        public static CampaignDraft FromSnapshot(string text)
        {
            try
            {
                return JsonSerializer.Deserialize<CampaignDraft>(text ?? "", Options);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public CampaignDraft Copy() => FromSnapshot(Snapshot());

        public override string ToString() => $"{Id}: {Chapters.Count} chapters, {People.Count} characters";
    }

    // A CHAPTER: its id (made from its first title, and the story's node for it), its title (the locale's, ChapterTitle)
    // and its maps in order
    public sealed class ChapterDraft
    {
        public string Id { get; set; } = "";

        public string Title { get; set; } = "";

        public List<string> Maps { get; set; } = new();

        public override string ToString() => $"{Id} '{Title}' on {Maps.Count} maps";
    }

    // A NAMED PERSON who speaks or appears: their id (the story's speaker), their name (the locale's actor.<id>.name, as
    // sample_millbrook's Oda), and the Workshop's own note to self and portrait (workshop.json)
    public sealed class Person
    {
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";

        public string Note { get; set; } = "";

        // a picture in the campaign folder (assets/portraits/<id>.png), or ""
        public string Portrait { get; set; } = "";

        public override string ToString() => Id;
    }
}
